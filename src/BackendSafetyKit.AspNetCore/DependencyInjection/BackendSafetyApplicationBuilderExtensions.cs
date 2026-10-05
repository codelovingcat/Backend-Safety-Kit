using BackendSafetyKit.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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

        var options = app.ApplicationServices
            .GetRequiredService<
                Microsoft.Extensions.Options.IOptions<BackendSafetyKit.BackendSafetyOptions>>();

        var features = options.Value.Features;

        if (features.EnableCorrelationId)
        {
            app.UseMiddleware<CorrelationIdMiddleware>();
        }

        if (features.EnableRequestLogging)
        {
            app.UseMiddleware<StructuredRequestLoggingMiddleware>();
        }

        if (features.EnableRequestTiming)
        {
            app.UseMiddleware<RequestTimingMiddleware>();
        }

        if (features.EnableHttpSecurity)
        {
            app.UseMiddleware<HttpSecurityMiddleware>();
        }

        if (features.EnableExceptionHandling)
        {
            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        }

        return app;
    }
}
