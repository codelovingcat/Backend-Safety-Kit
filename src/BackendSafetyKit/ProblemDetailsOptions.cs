namespace BackendSafetyKit;

/// <summary>
/// Configures standardized ProblemDetails responses.
/// </summary>
public sealed class ProblemDetailsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the request trace identifier is included.
    /// </summary>
    public bool IncludeTraceId { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the request path is included as the ProblemDetails instance.
    /// </summary>
    public bool IncludeInstance { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the exception message is included when the application is in development.
    /// </summary>
    public bool IncludeExceptionDetailInDevelopment { get; set; }

    /// <summary>
    /// Gets or sets the default RFC ProblemDetails type.
    /// </summary>
    public string DefaultType { get; set; } = "about:blank";

    /// <summary>
    /// Gets the configured exception-to-error-code mappings.
    /// </summary>
    public IDictionary<Type, string> ErrorCodeMappings { get; } =
        new Dictionary<Type, string>();

    /// <summary>
    /// Gets the configured exception-to-title mappings.
    /// </summary>
    public IDictionary<Type, string> TitleMappings { get; } =
        new Dictionary<Type, string>();

    /// <summary>
    /// Gets or sets a callback used to customize the generated ProblemDetails data.
    /// </summary>
    public Action<ProblemDetailsCustomizationContext>? Customize { get; set; }

    /// <summary>
    /// Maps an exception type to an application-specific error code.
    /// </summary>
    /// <typeparam name="TException">The exception type to map.</typeparam>
    /// <param name="errorCode">The application-specific error code.</param>
    /// <returns>The same options instance for chaining.</returns>
    public ProblemDetailsOptions MapErrorCode<TException>(string errorCode)
        where TException : Exception
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException(
                "An error code is required.",
                nameof(errorCode));
        }

        ErrorCodeMappings[typeof(TException)] = errorCode.Trim();
        return this;
    }

    /// <summary>
    /// Maps an exception type to a response title.
    /// </summary>
    /// <typeparam name="TException">The exception type to map.</typeparam>
    /// <param name="title">The safe response title.</param>
    /// <returns>The same options instance for chaining.</returns>
    public ProblemDetailsOptions MapTitle<TException>(string title)
        where TException : Exception
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "A title is required.",
                nameof(title));
        }

        TitleMappings[typeof(TException)] = title.Trim();
        return this;
    }

    internal string? GetErrorCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return GetMappedValue(ErrorCodeMappings, exception.GetType());
    }

    internal string? GetTitle(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return GetMappedValue(TitleMappings, exception.GetType());
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(DefaultType))
        {
            throw new ArgumentException(
                "A default ProblemDetails type is required.",
                nameof(DefaultType));
        }

        ValidateMappings(ErrorCodeMappings, "error code");
        ValidateMappings(TitleMappings, "title");
    }

    private static string? GetMappedValue(
        IDictionary<Type, string> mappings,
        Type exceptionType)
    {
        if (mappings.TryGetValue(exceptionType, out var exactValue))
        {
            return exactValue;
        }

        var bestMapping = mappings
            .Where(mapping => mapping.Key.IsAssignableFrom(exceptionType))
            .OrderByDescending(mapping => GetInheritanceDepth(mapping.Key))
            .ThenBy(mapping => mapping.Key.FullName, StringComparer.Ordinal)
            .FirstOrDefault();

        return bestMapping.Equals(default(KeyValuePair<Type, string>))
            ? null
            : bestMapping.Value;
    }

    private static int GetInheritanceDepth(Type type)
    {
        var depth = 0;
        var current = type;

        while (current.BaseType is not null)
        {
            depth++;
            current = current.BaseType;
        }

        return depth;
    }

    private static void ValidateMappings(
        IDictionary<Type, string> mappings,
        string valueName)
    {
        foreach (var mapping in mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.Value))
            {
                throw new ArgumentException(
                    $"A {valueName} cannot be empty.");
            }
        }
    }
}
