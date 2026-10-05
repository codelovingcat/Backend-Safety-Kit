namespace BackendSafetyKit;

/// <summary>
/// Configures sensitive data masking and redaction behavior.
/// </summary>
public sealed class SensitiveDataMaskingOptions
{
    /// <summary>
    /// Gets the ordered masking rules. The first matching rule wins.
    /// </summary>
    public IList<SensitiveDataMaskingRule> Rules { get; } =
        CreateDefaultRules();

    /// <summary>
    /// Gets or sets the marker used for full redaction.
    /// </summary>
    public string MaskValue { get; set; } = "[REDACTED]";

    /// <summary>
    /// Gets or sets a value indicating whether rules using partial masking are allowed.
    /// </summary>
    public bool AllowPartialMasking { get; set; }

    /// <summary>
    /// Gets or sets the maximum recursion depth used while masking nested structured data.
    /// </summary>
    public int MaxDepth { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum number of collection items copied from a structured value.
    /// </summary>
    public int MaxCollectionItems { get; set; } = 256;

    /// <summary>
    /// Adds a new masking rule.
    /// </summary>
    /// <param name="name">The field name or pattern to match.</param>
    /// <param name="matchMode">The rule matching mode.</param>
    /// <param name="maskMode">The masking mode.</param>
    /// <param name="visiblePrefixLength">The visible prefix length for partial masking.</param>
    /// <param name="visibleSuffixLength">The visible suffix length for partial masking.</param>
    /// <returns>The same options instance.</returns>
    public SensitiveDataMaskingOptions AddRule(
        string name,
        SensitiveDataMatchMode matchMode = SensitiveDataMatchMode.Exact,
        SensitiveDataMaskMode maskMode = SensitiveDataMaskMode.Full,
        int visiblePrefixLength = 0,
        int visibleSuffixLength = 4)
    {
        Rules.Add(
            new SensitiveDataMaskingRule(name)
            {
                MatchMode = matchMode,
                MaskMode = maskMode,
                VisiblePrefixLength = visiblePrefixLength,
                VisibleSuffixLength = visibleSuffixLength
            });

        return this;
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(MaskValue) || MaskValue.Length > 64)
        {
            throw new ArgumentException(
                "The mask value must be non-empty and at most 64 characters.",
                nameof(MaskValue));
        }

        if (MaxDepth is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxDepth),
                MaxDepth,
                "Maximum masking depth must be between 1 and 32.");
        }

        if (MaxCollectionItems is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxCollectionItems),
                MaxCollectionItems,
                "Maximum collection items must be between 1 and 4096.");
        }

        if (Rules.Count > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Rules),
                "A maximum of 256 masking rules is supported.");
        }

        foreach (var rule in Rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            rule.Validate();
        }
    }

    private static IList<SensitiveDataMaskingRule> CreateDefaultRules()
    {
        return new List<SensitiveDataMaskingRule>
        {
            new("password"),
            new("passwd"),
            new("pwd"),
            new("apiKey"),
            new("api_key"),
            new("api-key"),
            new("accessToken"),
            new("access_token"),
            new("refreshToken"),
            new("refresh_token"),
            new("clientSecret"),
            new("client_secret"),
            new("secret"),
            new("token"),
            new("authorization"),
            new("proxy-authorization"),
            new("cookie"),
            new("set-cookie"),
            new("x-api-key"),
            new("x-auth-token")
        };
    }
}
