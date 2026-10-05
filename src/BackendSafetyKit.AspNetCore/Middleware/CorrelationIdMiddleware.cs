using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyOptions> options,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICorrelationIdAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(accessor);

        var correlationOptions = options.Value.Correlation;
        correlationOptions.Validate();

        var correlationId =
            GetIncomingValue(
                context.Request.Headers[correlationOptions.CorrelationIdHeaderName],
                correlationOptions.MaxLength)
            ?? GetIncomingValue(
                context.Request.Headers[correlationOptions.RequestIdHeaderName],
                correlationOptions.MaxLength)
            ?? GenerateCorrelationId(correlationOptions);

        accessor.CorrelationId = correlationId;
        context.TraceIdentifier = correlationId;

        if (correlationOptions.IncludeResponseHeader)
        {
            context.Response.Headers[correlationOptions.ResponseHeaderName] = correlationId;
        }

        using var scope = logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId
            });

        await next(context);
    }

    private static string? GetIncomingValue(
        StringValues headerValues,
        int maxLength)
    {
        if (headerValues.Count != 1)
        {
            return null;
        }

        var value = headerValues.ToString().Trim();

        if (value.Length is 0 or > maxLength)
        {
            return null;
        }

        return IsValidCorrelationId(value) ? value : null;
    }

    private static string GenerateCorrelationId(CorrelationIdOptions options)
    {
        var generated = options.Generator().Trim();

        if (generated.Length is 0 or > options.MaxLength ||
            !IsValidCorrelationId(generated))
        {
            throw new InvalidOperationException(
                "The configured correlation ID generator returned an invalid identifier.");
        }

        return generated;
    }

    private static bool IsValidCorrelationId(string value)
    {
        foreach (var character in value)
        {
            if (character is
                >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9' or
                '-' or '_' or '.' or ':')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
