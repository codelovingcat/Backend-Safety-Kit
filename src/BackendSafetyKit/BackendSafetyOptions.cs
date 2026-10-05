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
}
