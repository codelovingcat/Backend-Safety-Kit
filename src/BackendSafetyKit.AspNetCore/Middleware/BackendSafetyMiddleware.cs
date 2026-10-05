using Microsoft.AspNetCore.Http;

namespace BackendSafetyKit.AspNetCore.Middleware;

internal sealed class BackendSafetyMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return next(context);
    }
}
