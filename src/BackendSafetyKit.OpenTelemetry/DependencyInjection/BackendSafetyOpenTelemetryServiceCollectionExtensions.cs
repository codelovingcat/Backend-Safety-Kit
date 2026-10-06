using BackendSafetyKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.OpenTelemetry.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Backend Safety Kit with OpenTelemetry.
/// </summary>
public static class BackendSafetyOpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Backend Safety Kit activity source and meter with OpenTelemetry provider configuration.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">Optional integration configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddBackendSafetyOpenTelemetry(
        this IServiceCollection services,
        Action<BackendSafetyOpenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<BackendSafetyOpenTelemetryOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.ConfigureOpenTelemetryTracerProvider((serviceProvider, tracerProviderBuilder) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<BackendSafetyOpenTelemetryOptions>>()
                .Value;

            if (options.EnableTracing)
            {
                tracerProviderBuilder.AddSource(
                    BackendSafetyDiagnostics.ActivitySource.Name);
            }
        });

        services.ConfigureOpenTelemetryMeterProvider((serviceProvider, meterProviderBuilder) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<BackendSafetyOpenTelemetryOptions>>()
                .Value;

            if (options.EnableMetrics)
            {
                meterProviderBuilder.AddMeter(BackendSafetyMetrics.MeterName);
            }
        });

        return services;
    }
}
