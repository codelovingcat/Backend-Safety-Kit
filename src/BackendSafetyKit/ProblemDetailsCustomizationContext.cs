namespace BackendSafetyKit;

/// <summary>
/// Provides request and error data to ProblemDetails customization callbacks.
/// </summary>
public sealed class ProblemDetailsCustomizationContext
{
    /// <summary>
    /// Gets the original exception.
    /// </summary>
    public required Exception Exception { get; init; }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the request HTTP method.
    /// </summary>
    public required string RequestMethod { get; init; }

    /// <summary>
    /// Gets the request path.
    /// </summary>
    public required string RequestPath { get; init; }

    /// <summary>
    /// Gets the current request trace identifier.
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// Gets or sets the response type.
    /// </summary>
    public string Type { get; set; } = "about:blank";

    /// <summary>
    /// Gets or sets the response title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets a safe response detail.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Gets or sets the response instance.
    /// </summary>
    public string? Instance { get; set; }

    /// <summary>
    /// Gets or sets the application-specific error code.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets additional ProblemDetails extension values.
    /// </summary>
    public IDictionary<string, object?> Extensions { get; } =
        new Dictionary<string, object?>();
}
