namespace BackendSafetyKit;

/// <summary>
/// Controls which ASP.NET Core middleware features are added to the request pipeline.
/// </summary>
public sealed class BackendSafetyFeatureOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether correlation/request ID handling is enabled.
    /// </summary>
    public bool EnableCorrelationId { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether structured request logging is enabled.
    /// </summary>
    public bool EnableRequestLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether distributed tracing is enabled.
    /// </summary>
    public bool EnableDistributedTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether request timing diagnostics are enabled.
    /// </summary>
    public bool EnableRequestTiming { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether secure HTTP defaults are enabled.
    /// </summary>
    public bool EnableHttpSecurity { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the global exception boundary is enabled.
    /// </summary>
    public bool EnableExceptionHandling { get; set; } = true;
}
