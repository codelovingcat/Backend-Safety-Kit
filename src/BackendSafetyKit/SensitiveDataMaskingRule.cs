namespace BackendSafetyKit;

/// <summary>
/// Defines how a sensitive field name is matched and masked.
/// </summary>
public sealed class SensitiveDataMaskingRule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveDataMaskingRule"/> class.
    /// </summary>
    /// <param name="name">The field name or pattern to match.</param>
    public SensitiveDataMaskingRule(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets or sets the field name or pattern to match.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets how <see cref="Name"/> is matched.
    /// </summary>
    public SensitiveDataMatchMode MatchMode { get; set; } = SensitiveDataMatchMode.Exact;

    /// <summary>
    /// Gets or sets how a matching value is masked.
    /// </summary>
    public SensitiveDataMaskMode MaskMode { get; set; } = SensitiveDataMaskMode.Full;

    /// <summary>
    /// Gets or sets the number of leading characters kept visible for partial masking.
    /// </summary>
    public int VisiblePrefixLength { get; set; }

    /// <summary>
    /// Gets or sets the number of trailing characters kept visible for partial masking.
    /// </summary>
    public int VisibleSuffixLength { get; set; } = 4;

    /// <summary>
    /// Gets or sets the character used between the visible prefix and suffix for partial masking.
    /// </summary>
    public char MaskCharacter { get; set; } = '*';

    internal bool Matches(string fieldName)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;

        return MatchMode switch
        {
            SensitiveDataMatchMode.Exact =>
                string.Equals(Name, fieldName, comparison),
            SensitiveDataMatchMode.Contains =>
                fieldName.Contains(Name, comparison),
            SensitiveDataMatchMode.StartsWith =>
                fieldName.StartsWith(Name, comparison),
            SensitiveDataMatchMode.EndsWith =>
                fieldName.EndsWith(Name, comparison),
            _ => false
        };
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 256)
        {
            throw new ArgumentException(
                "Sensitive data rule names must be non-empty and at most 256 characters.",
                nameof(Name));
        }

        if (!Enum.IsDefined(MatchMode))
        {
            throw new ArgumentOutOfRangeException(nameof(MatchMode), MatchMode, null);
        }

        if (!Enum.IsDefined(MaskMode))
        {
            throw new ArgumentOutOfRangeException(nameof(MaskMode), MaskMode, null);
        }

        if (VisiblePrefixLength is < 0 or > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VisiblePrefixLength),
                VisiblePrefixLength,
                "Visible prefix length must be between 0 and 64.");
        }

        if (VisibleSuffixLength is < 0 or > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VisibleSuffixLength),
                VisibleSuffixLength,
                "Visible suffix length must be between 0 and 64.");
        }

        if (char.IsControl(MaskCharacter))
        {
            throw new ArgumentException(
                "The masking character must not be a control character.",
                nameof(MaskCharacter));
        }
    }
}
