namespace BackendSafetyKit.OpenTelemetry;

/// <summary>
/// Configures the optional OpenTelemetry integration for Backend Safety Kit.
/// </summary>
public sealed class BackendSafetyOpenTelemetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the Backend Safety Kit activity source
    /// is registered with the OpenTelemetry tracing pipeline.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Backend Safety Kit meter
    /// is registered with the OpenTelemetry metrics pipeline.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}
