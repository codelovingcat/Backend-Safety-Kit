namespace BackendSafetyKit;

/// <summary>
/// Contains safe diagnostic information for a completed HTTP request.
/// </summary>
public sealed class RequestTimingContext
{
    /// <summary>
    /// Gets the HTTP method.
    /// </summary>
    public string Method { get; internal init; } = string.Empty;

    /// <summary>
    /// Gets the request path without the query string.
    /// </summary>
    public string Path { get; internal init; } = string.Empty;

    /// <summary>
    /// Gets the final HTTP status code.
    /// </summary>
    public int StatusCode { get; internal init; }

    /// <summary>
    /// Gets the effective correlation/request identifier.
    /// </summary>
    public string CorrelationId { get; internal init; } = string.Empty;

    /// <summary>
    /// Gets the elapsed request duration.
    /// </summary>
    public TimeSpan Duration { get; internal init; }

    /// <summary>
    /// Gets a value indicating whether the request exceeded the configured slow-request threshold.
    /// </summary>
    public bool IsSlow { get; internal init; }
}
