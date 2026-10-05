namespace BackendSafetyKit;

/// <summary>
/// Configures secure HTTP defaults that can be applied to an ASP.NET Core application.
/// </summary>
public sealed class HttpSecurityOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the X-Content-Type-Options header is added.
    /// </summary>
    public bool EnableNoSniffHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the X-Frame-Options header is added.
    /// </summary>
    public bool EnableFrameOptionsHeader { get; set; }

    /// <summary>
    /// Gets or sets the X-Frame-Options value when the feature is enabled.
    /// </summary>
    public string FrameOptions { get; set; } = "DENY";

    /// <summary>
    /// Gets or sets a value indicating whether the Referrer-Policy header is added.
    /// </summary>
    public bool EnableReferrerPolicyHeader { get; set; }

    /// <summary>
    /// Gets or sets the Referrer-Policy value when the feature is enabled.
    /// </summary>
    public string ReferrerPolicy { get; set; } = "no-referrer";

    /// <summary>
    /// Gets or sets a value indicating whether the Content-Security-Policy header is added.
    /// </summary>
    public bool EnableContentSecurityPolicyHeader { get; set; }

    /// <summary>
    /// Gets or sets the Content-Security-Policy value when the feature is enabled.
    /// </summary>
    public string ContentSecurityPolicy { get; set; } =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

    /// <summary>
    /// Gets or sets the maximum request body size in bytes. A null value leaves the server limit unchanged.
    /// </summary>
    public long? MaxRequestBodySize { get; set; }

    internal void Validate()
    {
        if (EnableFrameOptionsHeader &&
            !string.Equals(FrameOptions, "DENY", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(FrameOptions, "SAMEORIGIN", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "FrameOptions must be DENY or SAMEORIGIN.",
                nameof(FrameOptions));
        }

        if (EnableReferrerPolicyHeader)
        {
            ValidateHeaderValue(
                ReferrerPolicy,
                nameof(ReferrerPolicy),
                "A referrer policy value is required when Referrer-Policy is enabled.");
        }

        if (EnableContentSecurityPolicyHeader)
        {
            ValidateHeaderValue(
                ContentSecurityPolicy,
                nameof(ContentSecurityPolicy),
                "A Content-Security-Policy value is required when Content-Security-Policy is enabled.");
        }

        if (MaxRequestBodySize is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxRequestBodySize),
                MaxRequestBodySize,
                "The maximum request body size must be greater than zero.");
        }
    }

    private static void ValidateHeaderValue(
        string? value,
        string parameterName,
        string requiredMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(requiredMessage, parameterName);
        }

        if (value.Contains('\r') ||
            value.Contains('\n'))
        {
            throw new ArgumentException(
                "Header values cannot contain carriage return or line feed characters.",
                parameterName);
        }
    }
}
