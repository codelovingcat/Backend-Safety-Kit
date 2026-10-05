using BackendSafetyKit.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;

namespace BackendSafetyKit.AspNetCore.DependencyInjection;

public static class BackendSafetyApplicationBuilderExtensions
{
    public static IApplicationBuilder UseBackendSafety(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<BackendSafetyMiddleware>();
    }
}
