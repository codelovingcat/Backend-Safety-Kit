using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyOptions> options,
    ICorrelationIdAccessor accessor,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationOptions = options.Value.Correlation;
        correlationOptions.Validate();

        var correlationId = GetIncomingValue(
            context.Request.Headers[correlationOptions.CorrelationIdHeaderName],
            correlationOptions)
            ?? GetIncomingValue(
                context.Request.Headers[correlationOptions.RequestIdHeaderName],
                correlationOptions)
            ?? correlationOptions.Generator().Trim();

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
        string? headerValue,
        CorrelationIdOptions options)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        var value = headerValue.Trim();

        if (value.Length > options.MaxLength)
        {
            return null;
        }

        return IsValidCorrelationId(value) ? value : null;
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
