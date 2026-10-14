using System.Collections.ObjectModel;

namespace BackendSafetyKit;

/// <summary>
/// Represents one structured diagnostic event emitted by Backend Safety Kit.
/// </summary>
/// <remarks>
/// The property dictionary is copied and exposed as read-only. Producers should include only
/// operational metadata and must not include secrets, request/response bodies, query strings,
/// headers, or other sensitive values.
/// </remarks>
public sealed class BackendSafetyDiagnosticEvent
{
    /// <summary>
    /// Initializes a new diagnostic event.
    /// </summary>
    /// <param name="name">The stable event name.</param>
    /// <param name="timestamp">The time at which the event occurred.</param>
    /// <param name="severity">The severity assigned to the event.</param>
    /// <param name="properties">Optional structured event properties.</param>
    public BackendSafetyDiagnosticEvent(
        string name,
        DateTimeOffset timestamp,
        BackendSafetyDiagnosticSeverity severity,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An event name is required.", nameof(name));
        }

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
        }

        var propertyCopy = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (properties is not null)
        {
            foreach (var property in properties)
            {
                if (string.IsNullOrWhiteSpace(property.Key))
                {
                    throw new ArgumentException(
                        "Diagnostic property names must not be empty.",
                        nameof(properties));
                }

                propertyCopy.Add(property.Key, property.Value);
            }
        }

        Name = name.Trim();
        Timestamp = timestamp;
        Severity = severity;
        Properties = new ReadOnlyDictionary<string, object?>(propertyCopy);
    }

    /// <summary>
    /// Gets the stable name of the event.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the timestamp at which the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the event severity.
    /// </summary>
    public BackendSafetyDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the structured event properties as a read-only snapshot.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; }
}
