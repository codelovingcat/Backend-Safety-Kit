using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BackendSafetyKit.AspNetCore.Health;

/// <summary>
/// Provides health-check registration extensions for Backend Safety Kit.
/// </summary>
public static class BackendSafetyHealthCheckExtensions
{
    /// <summary>
    /// Registers the Backend Safety Kit configuration health check with the standard ASP.NET Core health-check infrastructure.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="name">The health-check registration name.</param>
    /// <param name="tags">Optional tags for liveness/readiness filtering.</param>
    /// <returns>The configured health-check builder.</returns>
    public static IHealthChecksBuilder AddBackendSafetyHealthChecks(
        this IServiceCollection services,
        string name = BackendSafetyHealthCheck.DefaultName,
        params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "A health-check name is required.",
                nameof(name));
        }

        ArgumentNullException.ThrowIfNull(tags);

        return services
            .AddHealthChecks()
            .AddCheck<BackendSafetyHealthCheck>(
                name.Trim(),
                failureStatus: HealthStatus.Unhealthy,
                tags: tags);
    }
}
