using BackendSafetyKit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackendSafetyKit.AspNetCore.Health;

internal sealed partial class BackendSafetyHealthCheck(
    IOptions<BackendSafetyOptions> options,
    ILogger<BackendSafetyHealthCheck> logger) : IHealthCheck
{
    internal const string DefaultName = "backend-safety";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var backendOptions = options.Value;

            backendOptions.ExceptionHandling.Validate();
            backendOptions.Correlation.Validate();
            backendOptions.ProblemDetails.Validate();
            backendOptions.RequestLogging.Validate();
            backendOptions.SensitiveDataMasking.Validate();
            backendOptions.RequestTiming.Validate();
            backendOptions.HttpSecurity.Validate();

            var data = new Dictionary<string, object>
            {
                ["configuration"] = "valid",
                ["features"] = new[]
                {
                    "exception-handling",
                    "problem-details",
                    "correlation",
                    "request-logging",
                    "sensitive-data-masking",
                    "request-timing",
                    "http-security"
                }
            };

            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "Backend Safety Kit configuration is valid.",
                    data));
        }
        catch (Exception exception)
        {
            LogInvalidConfiguration(logger, exception);

            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Backend Safety Kit configuration is invalid."));
        }
    }

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Error,
        Message = "Backend Safety Kit configuration validation failed. Health status is unhealthy.")]
    private static partial void LogInvalidConfiguration(
        ILogger logger,
        Exception exception);
}
