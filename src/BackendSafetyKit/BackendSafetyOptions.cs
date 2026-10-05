namespace BackendSafetyKit;

/// <summary>
/// Provides configuration options for Backend Safety Kit.
/// </summary>
public sealed class BackendSafetyOptions
{
    /// <summary>
    /// Gets the configuration for global exception handling.
    /// </summary>
    public ExceptionHandlingOptions ExceptionHandling { get; } = new();

    /// <summary>
    /// Gets the configuration for correlation and request identifiers.
    /// </summary>
    public CorrelationIdOptions Correlation { get; } = new();

    /// <summary>
    /// Gets the configuration for standardized ProblemDetails responses.
    /// </summary>
    public ProblemDetailsOptions ProblemDetails { get; } = new();

    /// <summary>
    /// Gets the configuration for structured HTTP request completion logging.
    /// </summary>
    public RequestLoggingOptions RequestLogging { get; } = new();

    /// <summary>
    /// Gets the configuration for sensitive data masking and redaction.
    /// </summary>
    public SensitiveDataMaskingOptions SensitiveDataMasking { get; } = new();

    /// <summary>
    /// Gets the configuration for request timing and slow-request diagnostics.
    /// </summary>
    public RequestTimingOptions RequestTiming { get; } = new();
}
