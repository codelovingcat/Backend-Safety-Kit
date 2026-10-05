using BackendSafetyKit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed partial class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyOptions> options,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    private static readonly EventId UnhandledExceptionEvent =
        new(1000, nameof(UnhandledExceptionEvent));

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var exceptionOptions = options.Value.ExceptionHandling;
            exceptionOptions.Validate();

            var statusCode = exceptionOptions.GetStatusCode(exception);

            LogUnhandledException(
                logger,
                exception,
                context.Request.Method,
                context.Request.Path,
                statusCode);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            var payload = new SafeExceptionResponse
            {
                Title = statusCode >= 500
                    ? "An unexpected error occurred."
                    : "An error occurred while processing the request.",
                Status = statusCode,
                TraceId = context.TraceIdentifier
            };

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                payload,
                cancellationToken: context.RequestAborted);
        }
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {RequestMethod} {RequestPath}. Returning status code {StatusCode}.")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string requestMethod,
        string requestPath,
        int statusCode);

    private sealed class SafeExceptionResponse
    {
        public required string Title { get; init; }

        public int Status { get; init; }

        public required string TraceId { get; init; }
    }
}
