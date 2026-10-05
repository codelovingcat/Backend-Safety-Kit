namespace BackendSafetyKit;

/// <summary>
/// Provides non-mutating sensitive data masking for scalar and structured values.
/// </summary>
public interface ISensitiveDataMasker
{
    /// <summary>
    /// Masks a value according to the field name and configured masking rules.
    /// </summary>
    /// <param name="value">The value to mask.</param>
    /// <param name="fieldName">The optional field or header name associated with the value.</param>
    /// <returns>A masked copy or scalar value. The input object is never mutated.</returns>
    object? Mask(object? value, string? fieldName = null);

    /// <summary>
    /// Masks a string according to the field name and configured masking rules.
    /// </summary>
    /// <param name="value">The string to mask.</param>
    /// <param name="fieldName">The field or header name associated with the value.</param>
    /// <returns>The masked string.</returns>
    string? MaskString(string? value, string? fieldName = null);
}
