using BackendSafetyKit;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace BackendSafetyKit.OpenTelemetry.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Backend Safety Kit with OpenTelemetry.
/// </summary>
public static class BackendSafetyOpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Backend Safety Kit activity source and meter with the OpenTelemetry SDK.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">Optional integration configuration.</param>
    /// <returns>The OpenTelemetry builder for additional exporter and resource configuration.</returns>
    public static OpenTelemetryBuilder AddBackendSafetyOpenTelemetry(
        this IServiceCollection services,
        Action<BackendSafetyOpenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new BackendSafetyOpenTelemetryOptions();
        configure?.Invoke(options);

        var builder = services.AddOpenTelemetry();

        if (options.EnableTracing)
        {
            builder.WithTracing(tracing =>
                tracing.AddSource(BackendSafetyDiagnostics.ActivitySource.Name));
        }

        if (options.EnableMetrics)
        {
            builder.WithMetrics(metrics =>
                metrics.AddMeter(BackendSafetyMetrics.MeterName));
        }

        return builder;
    }
}
