namespace BackendSafetyKit;

/// <summary>
/// Specifies how a sensitive data rule matches a field name.
/// </summary>
public enum SensitiveDataMatchMode
{
    /// <summary>
    /// Matches the complete field name.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// Matches when the rule name occurs anywhere in the field name.
    /// </summary>
    Contains = 1,

    /// <summary>
    /// Matches when the field name starts with the rule name.
    /// </summary>
    StartsWith = 2,

    /// <summary>
    /// Matches when the field name ends with the rule name.
    /// </summary>
    EndsWith = 3
}
