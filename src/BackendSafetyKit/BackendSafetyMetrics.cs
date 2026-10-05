using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BackendSafetyKit;

/// <summary>
/// Provides the dependency-free metrics surface exposed by Backend Safety Kit.
/// </summary>
public static class BackendSafetyMetrics
{
    /// <summary>
    /// Gets the meter name used by Backend Safety Kit metrics.
    /// </summary>
    public const string MeterName = "BackendSafetyKit";

    /// <summary>
    /// Gets the meter used by Backend Safety Kit diagnostics.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName);

    internal const string RequestCountInstrumentName =
        "backend_safety_kit.http.server.request.count";

    internal const string RequestDurationInstrumentName =
        "backend_safety_kit.http.server.request.duration";

    internal const string RequestErrorCountInstrumentName =
        "backend_safety_kit.http.server.request.error.count";

    internal const string SlowRequestCountInstrumentName =
        "backend_safety_kit.http.server.request.slow.count";

    private static readonly Counter<long> RequestCount =
        Meter.CreateCounter<long>(
            RequestCountInstrumentName,
            unit: "{request}",
            description: "Number of completed HTTP requests.");

    private static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>(
            RequestDurationInstrumentName,
            unit: "s",
            description: "Completed HTTP request duration in seconds.");

    private static readonly Counter<long> RequestErrorCount =
        Meter.CreateCounter<long>(
            RequestErrorCountInstrumentName,
            unit: "{request}",
            description: "Number of completed HTTP requests with a 5xx status code.");

    private static readonly Counter<long> SlowRequestCount =
        Meter.CreateCounter<long>(
            SlowRequestCountInstrumentName,
            unit: "{request}",
            description: "Number of completed HTTP requests at or above the slow threshold.");

    internal static void RecordHttpRequest(RequestTimingContext timing)
    {
        ArgumentNullException.ThrowIfNull(timing);

        var tags = new TagList
        {
            { "http.request.method", NormalizeMethod(timing.Method) },
            { "http.response.status_code", timing.StatusCode }
        };

        RequestCount.Add(1, tags);
        RequestDuration.Record(
            Math.Max(0, timing.Duration.TotalSeconds),
            tags);

        if (timing.StatusCode >= 500)
        {
            RequestErrorCount.Add(1, tags);
        }

        if (timing.IsSlow)
        {
            SlowRequestCount.Add(1, tags);
        }
    }

    private static string NormalizeMethod(string method) =>
        method.ToUpperInvariant() switch
        {
            "CONNECT" => "CONNECT",
            "DELETE" => "DELETE",
            "GET" => "GET",
            "HEAD" => "HEAD",
            "OPTIONS" => "OPTIONS",
            "PATCH" => "PATCH",
            "POST" => "POST",
            "PUT" => "PUT",
            "TRACE" => "TRACE",
            _ => "OTHER"
        };
}
