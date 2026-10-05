using System.Diagnostics;
using BackendSafetyKit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed partial class RequestTimingMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyOptions> options,
    ILogger<RequestTimingMiddleware> logger)
{
    internal static readonly object TimingItemKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var timingOptions = options.Value.RequestTiming;
        timingOptions.Validate();

        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            var duration = Stopwatch.GetElapsedTime(startTimestamp);
            var timing = new RequestTimingContext
            {
                Method = context.Request.Method,
                Path = context.Request.Path.Value ?? "/",
                StatusCode = context.Response.StatusCode,
                CorrelationId = context.TraceIdentifier,
                Duration = duration,
                IsSlow = duration >= timingOptions.SlowRequestThreshold
            };

            context.Items[TimingItemKey] = timing;

            if (timing.IsSlow &&
                timingOptions.EnableSlowRequestLogging &&
                timingOptions.SlowRequestLogLevel != RequestTimingLogLevel.None)
            {
                var level = ToLogLevel(timingOptions.SlowRequestLogLevel);

                if (logger.IsEnabled(level))
                {
                    LogSlowRequest(
                        logger,
                        level,
                        timing.Method,
                        timing.Path,
                        timing.StatusCode,
                        timing.Duration.TotalMilliseconds,
                        timing.CorrelationId);
                }
            }

            InvokeCompletionHook(timingOptions, timing);
        }
    }

    private void InvokeCompletionHook(
        RequestTimingOptions timingOptions,
        RequestTimingContext timing)
    {
        if (timingOptions.OnCompleted is null)
        {
            return;
        }

        try
        {
            timingOptions.OnCompleted(timing);
        }
        catch (Exception exception)
        {
            LogCompletionHookFailure(logger, exception);
        }
    }

    private static LogLevel ToLogLevel(RequestTimingLogLevel logLevel) =>
        logLevel switch
        {
            RequestTimingLogLevel.Trace => LogLevel.Trace,
            RequestTimingLogLevel.Debug => LogLevel.Debug,
            RequestTimingLogLevel.Information => LogLevel.Information,
            RequestTimingLogLevel.Warning => LogLevel.Warning,
            RequestTimingLogLevel.Error => LogLevel.Error,
            RequestTimingLogLevel.Critical => LogLevel.Critical,
            _ => LogLevel.None
        };

    [LoggerMessage(
        EventId = 3001,
        Message = "Slow HTTP request {HttpMethod} {RequestPath} completed with status {StatusCode} in {DurationMs} ms. CorrelationId={CorrelationId}.")]
    private static partial void LogSlowRequest(
        ILogger logger,
        LogLevel level,
        string httpMethod,
        string requestPath,
        int statusCode,
        double durationMs,
        string correlationId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Error,
        Message = "The request timing completion hook failed.")]
    private static partial void LogCompletionHookFailure(
        ILogger logger,
        Exception exception);
}
