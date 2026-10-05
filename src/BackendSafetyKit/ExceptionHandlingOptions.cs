namespace BackendSafetyKit;

/// <summary>
/// Configures global exception handling behavior.
/// </summary>
public sealed class ExceptionHandlingOptions
{
    /// <summary>
    /// Gets or sets the HTTP status code used when no exception mapping matches.
    /// </summary>
    public int DefaultStatusCode { get; set; } = 500;

    /// <summary>
    /// Gets the configured exception-to-status-code mappings.
    /// </summary>
    public IDictionary<Type, int> StatusCodeMappings { get; } = new Dictionary<Type, int>();

    /// <summary>
    /// Maps an exception type to an HTTP status code.
    /// </summary>
    /// <typeparam name="TException">The exception type to map.</typeparam>
    /// <param name="statusCode">The HTTP status code returned when the exception is handled.</param>
    /// <returns>The same options instance for chaining.</returns>
    public ExceptionHandlingOptions Map<TException>(int statusCode)
        where TException : Exception
    {
        ValidateStatusCode(statusCode);
        StatusCodeMappings[typeof(TException)] = statusCode;
        return this;
    }

    internal int GetStatusCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var exceptionType = exception.GetType();

        if (StatusCodeMappings.TryGetValue(exceptionType, out var exactStatusCode))
        {
            return exactStatusCode;
        }

        foreach (var mapping in StatusCodeMappings)
        {
            if (mapping.Key.IsAssignableFrom(exceptionType))
            {
                return mapping.Value;
            }
        }

        return DefaultStatusCode;
    }

    internal void Validate()
    {
        ValidateStatusCode(DefaultStatusCode);

        foreach (var mapping in StatusCodeMappings)
        {
            ValidateStatusCode(mapping.Value);
        }
    }

    private static void ValidateStatusCode(int statusCode)
    {
        if (statusCode is < 400 or > 599)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusCode),
                statusCode,
                "Exception handling status codes must be between 400 and 599.");
        }
    }
}
