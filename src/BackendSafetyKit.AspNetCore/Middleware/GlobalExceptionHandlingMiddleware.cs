using BackendSafetyKit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed partial class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyKit.BackendSafetyOptions> options,
    IHostEnvironment? environment,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions ProblemDetailsJsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var backendOptions = options.Value;
            backendOptions.ExceptionHandling.Validate();
            backendOptions.ProblemDetails.Validate();

            var statusCode = backendOptions.ExceptionHandling.GetStatusCode(exception);

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

            var problemOptions = backendOptions.ProblemDetails;

            var customization = new ProblemDetailsCustomizationContext
            {
                Exception = exception,
                StatusCode = statusCode,
                RequestMethod = context.Request.Method,
                RequestPath = context.Request.Path.ToString(),
                TraceId = context.TraceIdentifier,
                Type = problemOptions.DefaultType,
                Title = problemOptions.GetTitle(exception)
                    ?? GetDefaultTitle(statusCode),
                Detail = problemOptions.IncludeExceptionDetailInDevelopment &&
                    environment?.IsDevelopment() == true
                    ? exception.Message
                    : null,
                Instance = problemOptions.IncludeInstance
                    ? context.Request.Path.ToString()
                    : null,
                ErrorCode = problemOptions.GetErrorCode(exception)
            };

            problemOptions.Customize?.Invoke(customization);

            var problemDetails = new ProblemDetails
            {
                Type = customization.Type,
                Title = customization.Title,
                Status = customization.StatusCode,
                Detail = customization.Detail,
                Instance = customization.Instance
            };

            if (problemOptions.IncludeTraceId)
            {
                problemDetails.Extensions["traceId"] = customization.TraceId;
            }

            if (!string.IsNullOrWhiteSpace(customization.ErrorCode))
            {
                problemDetails.Extensions["code"] = customization.ErrorCode;
            }

            foreach (var extension in customization.Extensions)
            {
                problemDetails.Extensions[extension.Key] = extension.Value;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json; charset=utf-8";

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                problemDetails,
                ProblemDetailsJsonOptions,
                context.RequestAborted);
        }
    }

    private static string GetDefaultTitle(int statusCode) =>
        statusCode >= 500
            ? "An unexpected error occurred."
            : "An error occurred while processing the request.";

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
}
