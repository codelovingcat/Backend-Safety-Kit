namespace BackendSafetyKit;

/// <summary>
/// Describes the importance of a Backend Safety Kit diagnostic event.
/// </summary>
public enum BackendSafetyDiagnosticSeverity
{
    /// <summary>Detailed diagnostic information.</summary>
    Trace = 0,

    /// <summary>Debugging information.</summary>
    Debug = 1,

    /// <summary>Normal operational information.</summary>
    Information = 2,

    /// <summary>A condition that may need attention.</summary>
    Warning = 3,

    /// <summary>An operation failed.</summary>
    Error = 4,

    /// <summary>A critical failure requires immediate attention.</summary>
    Critical = 5
}
