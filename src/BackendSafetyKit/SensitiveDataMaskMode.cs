namespace BackendSafetyKit;

/// <summary>
/// Specifies how a matched sensitive value is masked.
/// </summary>
public enum SensitiveDataMaskMode
{
    /// <summary>
    /// Replaces the entire value with the configured redaction marker.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Keeps the explicitly configured prefix and suffix visible.
    /// </summary>
    Partial = 1
}
