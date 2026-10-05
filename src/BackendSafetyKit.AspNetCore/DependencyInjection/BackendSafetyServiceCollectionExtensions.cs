using BackendSafetyKit;
using Microsoft.Extensions.DependencyInjection;

namespace BackendSafetyKit.AspNetCore.DependencyInjection;

public static class BackendSafetyServiceCollectionExtensions
{
    /// <summary>
    /// Registers Backend Safety Kit services in the application's dependency injection container.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddBackendSafety(
        this IServiceCollection services,
        Action<BackendSafetyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
