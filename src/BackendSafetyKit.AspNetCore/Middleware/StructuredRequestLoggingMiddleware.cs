using System.Diagnostics;
using System.Text;
using BackendSafetyKit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed partial class StructuredRequestLoggingMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyOptions> options,
    ISensitiveDataMasker masker,
    ILogger<StructuredRequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var backendOptions = options.Value;
        var loggingOptions = backendOptions.RequestLogging;
        loggingOptions.Validate();
        backendOptions.SensitiveDataMasking.Validate();

        var startTimestamp = Stopwatch.GetTimestamp();
        Exception? unhandledException = null;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            unhandledException = exception;
            throw;
        }
        finally
        {
            WriteCompletionLog(
                context,
                loggingOptions,
                startTimestamp,
                unhandledException);
        }
    }

    private void WriteCompletionLog(
        HttpContext context,
        RequestLoggingOptions options,
        long startTimestamp,
        Exception? unhandledException)
    {
        var statusCode = context.Response.StatusCode;
        var level = unhandledException is not null || statusCode >= 500
            ? LogLevel.Error
            : statusCode >= 400
                ? LogLevel.Warning
                : LogLevel.Information;

        if (!logger.IsEnabled(level))
        {
            return;
        }

        var durationMs = Math.Round(
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
            3);

        var correlationId = context.TraceIdentifier;
        var host = options.IncludeHost
            ? Truncate(context.Request.Host.Host, options.MaxMetadataValueLength)
            : null;

        var requestHeaders = CaptureHeaders(
            context.Request.Headers,
            options.AllowedRequestHeaders,
            options.DeniedRequestHeaders,
            options.MaxHeaderCount,
            options.MaxMetadataValueLength,
            masker);

        var responseHeaders = CaptureHeaders(
            context.Response.Headers,
            options.AllowedResponseHeaders,
            options.DeniedResponseHeaders,
            options.MaxHeaderCount,
            options.MaxMetadataValueLength,
            masker);

        var failureType = unhandledException?.GetType().FullName;
        var isFailure = unhandledException is not null || statusCode >= 400;

        switch (level)
        {
            case LogLevel.Error:
                LogCompletionError(
                    logger,
                    context.Request.Method,
                    context.Request.Path.Value ?? "/",
                    statusCode,
                    durationMs,
                    correlationId,
                    host,
                    isFailure,
                    failureType,
                    requestHeaders,
                    responseHeaders);
                break;

            case LogLevel.Warning:
                LogCompletionWarning(
                    logger,
                    context.Request.Method,
                    context.Request.Path.Value ?? "/",
                    statusCode,
                    durationMs,
                    correlationId,
                    host,
                    isFailure,
                    failureType,
                    requestHeaders,
                    responseHeaders);
                break;

            default:
                LogCompletionInformation(
                    logger,
                    context.Request.Method,
                    context.Request.Path.Value ?? "/",
                    statusCode,
                    durationMs,
                    correlationId,
                    host,
                    isFailure,
                    failureType,
                    requestHeaders,
                    responseHeaders);
                break;
        }
    }

    private static Dictionary<string, string>? CaptureHeaders(
        IHeaderDictionary headers,
        ISet<string> allowedHeaders,
        ISet<string> deniedHeaders,
        int maxHeaderCount,
        int maxValueLength,
        ISensitiveDataMasker masker)
    {
        if (maxHeaderCount == 0 || allowedHeaders.Count == 0)
        {
            return null;
        }

        Dictionary<string, string>? result = null;

        foreach (var headerName in allowedHeaders)
        {
            if (result?.Count >= maxHeaderCount)
            {
                break;
            }

            if (deniedHeaders.Contains(headerName) ||
                !headers.TryGetValue(headerName, out var values))
            {
                continue;
            }

            var value = FormatHeaderValue(values, maxValueLength);
            var maskedValue = masker.MaskString(value, headerName) ?? string.Empty;

            result ??= new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            result[headerName] = maskedValue;
        }

        return result;
    }

    private static string FormatHeaderValue(
        StringValues values,
        int maxLength)
    {
        if (values.Count <= 1)
        {
            return Truncate(
                values.Count == 0 ? string.Empty : values[0] ?? string.Empty,
                maxLength);
        }

        var builder = new StringBuilder(Math.Min(maxLength, 128));

        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0 && builder.Length < maxLength)
            {
                AppendWithLimit(builder, ", ", maxLength);
            }

            if (builder.Length >= maxLength)
            {
                break;
            }

            AppendWithLimit(
                builder,
                values[index] ?? string.Empty,
                maxLength);
        }

        return builder.ToString();
    }

    private static void AppendWithLimit(
        StringBuilder builder,
        string value,
        int maxLength)
    {
        var remaining = maxLength - builder.Length;

        if (remaining <= 0)
        {
            return;
        }

        if (value.Length <= remaining)
        {
            builder.Append(value);
            return;
        }

        if (remaining == 1)
        {
            builder.Append('…');
            return;
        }

        builder.Append(value.AsSpan(0, remaining - 1));
        builder.Append('…');
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        if (maxLength == 1)
        {
            return "…";
        }

        return string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "HTTP {HttpMethod} {RequestPath} completed with status {StatusCode} in {DurationMs} ms. CorrelationId={CorrelationId}; Host={Host}; IsFailure={IsFailure}; FailureType={FailureType}; RequestHeaders={RequestHeaders}; ResponseHeaders={ResponseHeaders}.")]
    private static partial void LogCompletionInformation(
        ILogger logger,
        string httpMethod,
        string requestPath,
        int statusCode,
        double durationMs,
        string correlationId,
        string? host,
        bool isFailure,
        string? failureType,
        IReadOnlyDictionary<string, string>? requestHeaders,
        IReadOnlyDictionary<string, string>? responseHeaders);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "HTTP {HttpMethod} {RequestPath} completed with status {StatusCode} in {DurationMs} ms. CorrelationId={CorrelationId}; Host={Host}; IsFailure={IsFailure}; FailureType={FailureType}; RequestHeaders={RequestHeaders}; ResponseHeaders={ResponseHeaders}.")]
    private static partial void LogCompletionWarning(
        ILogger logger,
        string httpMethod,
        string requestPath,
        int statusCode,
        double durationMs,
        string correlationId,
        string? host,
        bool isFailure,
        string? failureType,
        IReadOnlyDictionary<string, string>? requestHeaders,
        IReadOnlyDictionary<string, string>? responseHeaders);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "HTTP {HttpMethod} {RequestPath} completed with status {StatusCode} in {DurationMs} ms. CorrelationId={CorrelationId}; Host={Host}; IsFailure={IsFailure}; FailureType={FailureType}; RequestHeaders={RequestHeaders}; ResponseHeaders={ResponseHeaders}.")]
    private static partial void LogCompletionError(
        ILogger logger,
        string httpMethod,
        string requestPath,
        int statusCode,
        double durationMs,
        string correlationId,
        string? host,
        bool isFailure,
        string? failureType,
        IReadOnlyDictionary<string, string>? requestHeaders,
        IReadOnlyDictionary<string, string>? responseHeaders);
}
