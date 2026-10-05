namespace BackendSafetyKit.AspNetCore.Correlation;

internal sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    public string? CorrelationId { get; internal set; }
}
