using System.Diagnostics;
using BackendSafetyKit;
using BackendSafetyKit.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed partial class RequestTimingMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyOptions> options,
    ILogger<RequestTimingMiddleware> logger,
    IEnumerable<IBackendSafetyDiagnosticSink>? diagnosticSinks = null)
{
    internal static readonly object TimingItemKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var timingOptions = options.Value.RequestTiming;
        timingOptions.Validate();

        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            var duration = Stopwatch.GetElapsedTime(startTimestamp);
            var timing = new RequestTimingContext
            {
                Method = context.Request.Method,
                Path = context.Request.Path.Value ?? "/",
                StatusCode = context.Response.StatusCode,
                CorrelationId = context.TraceIdentifier,
                Duration = duration,
                IsSlow = duration >= timingOptions.SlowRequestThreshold
            };

            context.Items[TimingItemKey] = timing;

            if (timing.IsSlow &&
                timingOptions.EnableSlowRequestLogging &&
                timingOptions.SlowRequestLogLevel != RequestTimingLogLevel.None)
            {
                var level = ToLogLevel(timingOptions.SlowRequestLogLevel);

                if (logger.IsEnabled(level))
                {
                    LogSlowRequest(
                        logger,
                        level,
                        timing.Method,
                        timing.Path,
                        timing.StatusCode,
                        timing.Duration.TotalMilliseconds,
                        timing.CorrelationId);
                }
            }

            RecordMetrics(timing);
            InvokeCompletionHook(timingOptions, timing);
            WriteDiagnosticEvent(timing);
        }
    }

    private void RecordMetrics(RequestTimingContext timing)
    {
        try
        {
            BackendSafetyMetrics.RecordHttpRequest(timing);
        }
        catch (Exception exception)
        {
            LogMetricsFailure(logger, exception);
        }
    }

    private void InvokeCompletionHook(
        RequestTimingOptions timingOptions,
        RequestTimingContext timing)
    {
        if (timingOptions.OnCompleted is null)
        {
            return;
        }

        try
        {
            timingOptions.OnCompleted(timing);
        }
        catch (Exception exception)
        {
            LogCompletionHookFailure(logger, exception);
        }
    }

    private void WriteDiagnosticEvent(RequestTimingContext timing)
    {
        var eventName = timing.StatusCode >= 500
            ? BackendSafetyDiagnosticEventNames.HttpRequestServerError
            : timing.StatusCode >= 400
                ? BackendSafetyDiagnosticEventNames.HttpRequestClientError
                : BackendSafetyDiagnosticEventNames.HttpRequestCompleted;

        var severity = timing.StatusCode >= 500
            ? BackendSafetyDiagnosticSeverity.Error
            : timing.StatusCode >= 400 || timing.IsSlow
                ? BackendSafetyDiagnosticSeverity.Warning
                : BackendSafetyDiagnosticSeverity.Information;

        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BackendSafetyDiagnosticPropertyNames.HttpRequestMethod] = NormalizeMethod(timing.Method),
            [BackendSafetyDiagnosticPropertyNames.HttpResponseStatusCode] = timing.StatusCode,
            [BackendSafetyDiagnosticPropertyNames.HttpServerDurationMilliseconds] =
                Math.Round(Math.Max(0, timing.Duration.TotalMilliseconds), 3),
            [BackendSafetyDiagnosticPropertyNames.RequestIsSlow] = timing.IsSlow
        };

        var diagnosticEvent = new BackendSafetyDiagnosticEvent(
            eventName,
            DateTimeOffset.UtcNow,
            severity,
            properties);

        foreach (var diagnosticSink in diagnosticSinks ?? Array.Empty<IBackendSafetyDiagnosticSink>())
        {
            try
            {
                diagnosticSink.Write(diagnosticEvent);
            }
            catch (Exception)
            {
                // A custom sink must never interrupt or fail an HTTP request.
                LogDiagnosticSinkFailure(logger);
            }
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

    private static LogLevel ToLogLevel(RequestTimingLogLevel logLevel) =>
        logLevel switch
        {
            RequestTimingLogLevel.Trace => LogLevel.Trace,
            RequestTimingLogLevel.Debug => LogLevel.Debug,
            RequestTimingLogLevel.Information => LogLevel.Information,
            RequestTimingLogLevel.Warning => LogLevel.Warning,
            RequestTimingLogLevel.Error => LogLevel.Error,
            RequestTimingLogLevel.Critical => LogLevel.Critical,
            _ => LogLevel.None
        };

    [LoggerMessage(
        EventId = 3001,
        Message = "Slow HTTP request {HttpMethod} {RequestPath} completed with status {StatusCode} in {DurationMs} ms. CorrelationId={CorrelationId}.")]
    private static partial void LogSlowRequest(
        ILogger logger,
        LogLevel level,
        string httpMethod,
        string requestPath,
        int statusCode,
        double durationMs,
        string correlationId);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Error,
        Message = "The request metrics recorder failed.")]
    private static partial void LogMetricsFailure(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Error,
        Message = "The request timing completion hook failed.")]
    private static partial void LogCompletionHookFailure(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Error,
        Message = "A diagnostic event sink failed.")]
    private static partial void LogDiagnosticSinkFailure(ILogger logger);
}
