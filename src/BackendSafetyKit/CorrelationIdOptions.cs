namespace BackendSafetyKit;

/// <summary>
/// Configures correlation and request identifier behavior.
/// </summary>
public sealed class CorrelationIdOptions
{
    /// <summary>
    /// Gets or sets the primary incoming correlation ID header name.
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Gets or sets the fallback incoming request ID header name.
    /// </summary>
    public string RequestIdHeaderName { get; set; } = "X-Request-ID";

    /// <summary>
    /// Gets or sets the response header name used to return the effective identifier.
    /// </summary>
    public string ResponseHeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Gets or sets a value indicating whether the effective identifier is returned in the response header.
    /// </summary>
    public bool IncludeResponseHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum accepted identifier length.
    /// </summary>
    public int MaxLength { get; set; } = 128;

    /// <summary>
    /// Gets or sets the identifier generator used when no valid incoming identifier is available.
    /// </summary>
    public Func<string> Generator { get; set; } = static () =>
        Guid.NewGuid().ToString("N");

    internal void Validate()
    {
        ValidateHeaderName(CorrelationIdHeaderName, nameof(CorrelationIdHeaderName));
        ValidateHeaderName(RequestIdHeaderName, nameof(RequestIdHeaderName));
        ValidateHeaderName(ResponseHeaderName, nameof(ResponseHeaderName));

        if (MaxLength is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxLength),
                MaxLength,
                "Correlation ID maximum length must be between 1 and 1024.");
        }

        ArgumentNullException.ThrowIfNull(Generator);

        var generated = Generator();

        if (string.IsNullOrWhiteSpace(generated) || generated.Length > MaxLength)
        {
            throw new ArgumentException(
                "The configured correlation ID generator must return a non-empty identifier within the configured maximum length.",
                nameof(Generator));
        }
    }

    private static void ValidateHeaderName(string headerName, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            throw new ArgumentException("A header name is required.", parameterName);
        }

        if (headerName.Length > 256 || headerName.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "The header name must be non-empty, contain no whitespace, and be at most 256 characters.",
                parameterName);
        }
    }
}
