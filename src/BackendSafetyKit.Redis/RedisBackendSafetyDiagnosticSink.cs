using BackendSafetyKit;
using BackendSafetyKit.Abstractions;

namespace BackendSafetyKit.Redis;

internal sealed class RedisBackendSafetyDiagnosticSink(
    RedisDiagnosticQueue queue) : IBackendSafetyDiagnosticSink
{
    public void Write(BackendSafetyDiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);
        _ = queue.TryWrite(diagnosticEvent);
    }
}
