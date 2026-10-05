namespace BackendSafetyKit.AspNetCore.Correlation;

/// <summary>
/// Provides access to the effective correlation identifier for the current request.
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    /// Gets the effective correlation identifier for the current request.
    /// </summary>
    string? CorrelationId { get; }
}
