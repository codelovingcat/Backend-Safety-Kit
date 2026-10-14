using BackendSafetyKit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed class RequestSizePolicyMiddleware(
    RequestDelegate next,
    IOptions<BackendSafetyOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var maximumBodySize = options.Value.HttpSecurity.MaxRequestBodySize;

        if (maximumBodySize.HasValue &&
            context.Request.ContentLength is long contentLength &&
            contentLength > maximumBodySize.Value)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        await next(context);
    }
}
