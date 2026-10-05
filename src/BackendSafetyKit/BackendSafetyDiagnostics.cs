using System.Diagnostics;

namespace BackendSafetyKit;

/// <summary>
/// Provides diagnostic primitives exposed by Backend Safety Kit.
/// </summary>
public static class BackendSafetyDiagnostics
{
    /// <summary>
    /// Gets the activity source used by Backend Safety Kit middleware.
    /// </summary>
    public static ActivitySource ActivitySource { get; } =
        new("BackendSafetyKit");
}
