using BackendSafetyKit.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;

namespace BackendSafetyKit.AspNetCore.DependencyInjection;

public static class BackendSafetyApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Backend Safety Kit middleware to the ASP.NET Core request pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder for chaining.</returns>
    public static IApplicationBuilder UseBackendSafety(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<BackendSafetyMiddleware>();
    }
}
