using BackendSafetyKit;

namespace BackendSafetyKit.Abstractions;

/// <summary>
/// Represents an optional destination for safe, structured Backend Safety Kit diagnostic events.
/// </summary>
public interface IBackendSafetyDiagnosticSink
{
    /// <summary>
    /// Writes a diagnostic event.
    /// </summary>
    /// <param name="diagnosticEvent">The event to write.</param>
    /// <remarks>
    /// The default implementation intentionally does nothing to preserve compatibility with
    /// implementations of the original marker interface. Implement this method to receive events.
    /// Sink failures are isolated and do not interrupt HTTP request processing.
    /// </remarks>
    void Write(BackendSafetyDiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);
    }
}
