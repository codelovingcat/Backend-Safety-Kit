using BackendSafetyKit.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;

namespace BackendSafetyKit.AspNetCore.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Backend Safety Kit in the ASP.NET Core application pipeline.
/// </summary>
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

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<StructuredRequestLoggingMiddleware>();
        app.UseMiddleware<RequestTimingMiddleware>();
        app.UseMiddleware<HttpSecurityMiddleware>();
        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

        return app;
    }
}
