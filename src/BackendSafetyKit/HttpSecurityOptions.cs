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
    /// Gets or sets a value indicating whether Content-Security-Policy-Report-Only is added.
    /// This allows applications to evaluate a policy before enforcing it.
    /// </summary>
    public bool EnableContentSecurityPolicyReportOnlyHeader { get; set; }

    /// <summary>
    /// Gets or sets the report-only Content Security Policy. The policy is not enforced by browsers.
    /// </summary>
    public string ContentSecurityPolicyReportOnly { get; set; } =
        "default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";

    /// <summary>
    /// Gets or sets a value indicating whether HTTP requests are redirected to HTTPS.
    /// Disabled by default because HTTPS ports and proxy topology belong to the host application.
    /// </summary>
    public bool EnableHttpsRedirection { get; set; }

    /// <summary>
    /// Gets or sets the HTTPS port used for redirects. Required when HTTPS redirection is enabled.
    /// </summary>
    public int? HttpsPort { get; set; }

    /// <summary>
    /// Gets or sets the status code used for HTTPS redirects. Only 307 and 308 are supported
    /// so the original HTTP method and body semantics are preserved.
    /// </summary>
    public int HttpsRedirectStatusCode { get; set; } = 307;

    /// <summary>
    /// Gets or sets a value indicating whether the Strict-Transport-Security header is added
    /// to HTTPS responses. Disabled by default.
    /// </summary>
    public bool EnableHsts { get; set; }

    /// <summary>
    /// Gets or sets the HSTS max-age duration. Defaults to one year.
    /// </summary>
    public TimeSpan HstsMaxAge { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Gets or sets a value indicating whether the HSTS policy applies to all subdomains.
    /// </summary>
    public bool HstsIncludeSubDomains { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the HSTS policy includes the preload directive.
    /// Preload requires includeSubDomains and a max-age of at least one year.
    /// </summary>
    public bool HstsPreload { get; set; }

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

        if (EnableContentSecurityPolicyReportOnlyHeader)
        {
            ValidateHeaderValue(
                ContentSecurityPolicyReportOnly,
                nameof(ContentSecurityPolicyReportOnly),
                "A report-only Content-Security-Policy value is required when the report-only header is enabled.");
        }

        if (MaxRequestBodySize is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxRequestBodySize),
                MaxRequestBodySize,
                "The maximum request body size must be greater than zero.");
        }
        if (EnableHttpsRedirection)
        {
            if (HttpsPort is null or < 1 or > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(HttpsPort),
                    HttpsPort,
                    "HttpsPort must be between 1 and 65535 when HTTPS redirection is enabled.");
            }

            if (HttpsRedirectStatusCode is not 307 and not 308)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(HttpsRedirectStatusCode),
                    HttpsRedirectStatusCode,
                    "HTTPS redirects must use status code 307 or 308 to preserve request semantics.");
            }
        }

        if (EnableHsts)
        {
            if (HstsMaxAge < TimeSpan.FromSeconds(1) ||
                HstsMaxAge > TimeSpan.FromDays(3650))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(HstsMaxAge),
                    HstsMaxAge,
                    "HSTS max-age must be between one second and ten years.");
            }

            if (HstsPreload &&
                (!HstsIncludeSubDomains || HstsMaxAge < TimeSpan.FromDays(365)))
            {
                throw new ArgumentException(
                    "HSTS preload requires includeSubDomains and a max-age of at least one year.",
                    nameof(HstsPreload));
            }
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
