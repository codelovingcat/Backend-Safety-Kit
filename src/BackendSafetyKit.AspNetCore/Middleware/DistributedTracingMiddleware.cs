using System.Diagnostics;
using BackendSafetyKit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed class DistributedTracingMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyOptions> options)
{
    private const string TraceParentHeader = "traceparent";
    private const string TraceStateHeader = "tracestate";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tracingOptions = options.Value.DistributedTracing;
        tracingOptions.Validate();

        if (Activity.Current is not null)
        {
            await next(context);
            return;
        }

        var parentContext = ParseIncomingParentContext(context);
        using var activity = parentContext.HasValue
            ? BackendSafetyDiagnostics.ActivitySource.StartActivity(
                tracingOptions.ActivityName,
                ActivityKind.Server,
                parentContext.Value)
            : BackendSafetyDiagnostics.ActivitySource.StartActivity(
                tracingOptions.ActivityName,
                ActivityKind.Server);

        await next(context);
    }

    private static ActivityContext? ParseIncomingParentContext(
        HttpContext context)
    {
        var traceParent = GetSingleHeader(
            context.Request.Headers[TraceParentHeader]);

        if (traceParent is null)
        {
            return null;
        }

        var traceState = GetSingleHeader(
            context.Request.Headers[TraceStateHeader]);

        return ActivityContext.TryParse(
            traceParent,
            traceState,
            isRemote: true,
            out var parentContext)
            ? parentContext
            : null;
    }

    private static string? GetSingleHeader(
        Microsoft.Extensions.Primitives.StringValues values)
    {
        if (values.Count != 1)
        {
            return null;
        }

        var value = values.ToString().Trim();

        return value.Length == 0 ? null : value;
    }
}
