using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.AspNetCore.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Backend Safety Kit services.
/// </summary>
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

        var options = services.AddOptions<BackendSafetyOptions>()
            .Validate(
                ValidateBackendSafetyOptions,
                "Backend Safety Kit configuration is invalid.")
            .ValidateOnStart();

        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.AddScoped<CorrelationIdAccessor>();
        services.AddScoped<ICorrelationIdAccessor>(
            serviceProvider => serviceProvider.GetRequiredService<CorrelationIdAccessor>());

        services.AddSingleton<ISensitiveDataMasker>(
            serviceProvider => new SensitiveDataMasker(
                serviceProvider.GetRequiredService<IOptions<BackendSafetyOptions>>().Value
                    .SensitiveDataMasking));

        return services;
    }

    private static bool ValidateBackendSafetyOptions(BackendSafetyOptions options)
    {
        options.ExceptionHandling.Validate();
        options.Correlation.Validate();
        options.ProblemDetails.Validate();
        options.RequestLogging.Validate();
        options.SensitiveDataMasking.Validate();
        options.RequestTiming.Validate();
        options.HttpSecurity.Validate();

        return true;
    }
}
