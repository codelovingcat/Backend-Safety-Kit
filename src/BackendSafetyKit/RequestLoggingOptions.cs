namespace BackendSafetyKit;

/// <summary>
/// Configures structured HTTP request completion logging.
/// </summary>
public sealed class RequestLoggingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the request host is included in completion logs.
    /// </summary>
    public bool IncludeHost { get; set; } = true;

    /// <summary>
    /// Gets the request header names that are explicitly allowed in completion logs.
    /// </summary>
    public ISet<string> AllowedRequestHeaders { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the request header names that are excluded from completion logs.
    /// </summary>
    public ISet<string> DeniedRequestHeaders { get; } =
        new HashSet<string>(
            new[]
            {
                "Authorization",
                "Cookie",
                "Proxy-Authorization"
            },
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the response header names that are explicitly allowed in completion logs.
    /// </summary>
    public ISet<string> AllowedResponseHeaders { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the response header names that are excluded from completion logs.
    /// </summary>
    public ISet<string> DeniedResponseHeaders { get; } =
        new HashSet<string>(
            new[]
            {
                "Set-Cookie"
            },
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the log level used for successful request completion events.
    /// Set to <see cref="RequestLoggingLogLevel.None"/> to disable these events.
    /// </summary>
    public RequestLoggingLogLevel SuccessfulRequestLogLevel { get; set; } =
        RequestLoggingLogLevel.Information;

    /// <summary>
    /// Gets or sets the log level used for client-error completion events (HTTP 400-499).
    /// Set to <see cref="RequestLoggingLogLevel.None"/> to disable these events.
    /// </summary>
    public RequestLoggingLogLevel ClientErrorLogLevel { get; set; } =
        RequestLoggingLogLevel.Warning;

    /// <summary>
    /// Gets or sets the log level used for server-error completion events (HTTP 500+) and unhandled exceptions.
    /// Set to <see cref="RequestLoggingLogLevel.None"/> to disable these events.
    /// </summary>
    public RequestLoggingLogLevel ServerErrorLogLevel { get; set; } =
        RequestLoggingLogLevel.Error;

    /// <summary>
    /// Gets or sets the maximum length of an individual logged metadata value.
    /// </summary>
    public int MaxMetadataValueLength { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum number of request or response headers captured per log entry.
    /// </summary>
    public int MaxHeaderCount { get; set; } = 16;

    internal void Validate()
    {
        if (MaxMetadataValueLength is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxMetadataValueLength),
                MaxMetadataValueLength,
                "Metadata value length must be between 1 and 4096.");
        }

        if (MaxHeaderCount is < 0 or > 128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxHeaderCount),
                MaxHeaderCount,
                "Header count must be between 0 and 128.");
        }

        ValidateHeaderNames(AllowedRequestHeaders);
        ValidateHeaderNames(DeniedRequestHeaders);
        ValidateHeaderNames(AllowedResponseHeaders);
        ValidateHeaderNames(DeniedResponseHeaders);

        ValidateLogLevel(
            nameof(SuccessfulRequestLogLevel),
            SuccessfulRequestLogLevel);

        ValidateLogLevel(
            nameof(ClientErrorLogLevel),
            ClientErrorLogLevel);

        ValidateLogLevel(
            nameof(ServerErrorLogLevel),
            ServerErrorLogLevel);
    }

    private static void ValidateLogLevel(
        string optionName,
        RequestLoggingLogLevel logLevel)
    {
        if (!Enum.IsDefined(logLevel))
        {
            throw new ArgumentOutOfRangeException(
                optionName,
                logLevel,
                "The request logging log level is invalid.");
        }
    }

    private static void ValidateHeaderNames(IEnumerable<string> headerNames)
    {
        foreach (var headerName in headerNames)
        {
            ArgumentNullException.ThrowIfNull(headerName);

            if (string.IsNullOrWhiteSpace(headerName) || headerName.Length > 256)
            {
                throw new ArgumentException(
                    "Header names must be non-empty and at most 256 characters.");
            }

            foreach (var character in headerName)
            {
                if (IsTokenCharacter(character))
                {
                    continue;
                }

                throw new ArgumentException(
                    "Header names must contain valid HTTP token characters.");
            }
        }
    }

    private static bool IsTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) ||
        character is
            '!' or '#' or '$' or '%' or '&' or '*' or '+' or '-' or '.' or
            '^' or '_' or '|' or '~' ||
        character == (char)39 ||
        character == (char)96;
}
