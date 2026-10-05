namespace BackendSafetyKit;

/// <summary>
/// Provides configuration options for Backend Safety Kit.
/// </summary>
/// <remarks>
/// Configure these options during application startup and treat the resulting
/// configuration as immutable after the application has started. Backend Safety Kit
/// runtime services are designed to read startup configuration concurrently without
/// request-specific shared mutable state.
/// </remarks>
public sealed class BackendSafetyOptions
{
    /// <summary>
    /// Gets the feature-level switches controlling which ASP.NET Core middleware is added.
    /// </summary>
    public BackendSafetyFeatureOptions Features { get; } = new();

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

    /// <summary>
    /// Gets the configuration for secure HTTP defaults.
    /// </summary>
    public HttpSecurityOptions HttpSecurity { get; } = new();
}
