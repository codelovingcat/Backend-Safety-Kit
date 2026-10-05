using BackendSafetyKit;
using Microsoft.Extensions.DependencyInjection;

namespace BackendSafetyKit.AspNetCore.DependencyInjection;

public static class BackendSafetyServiceCollectionExtensions
{
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
