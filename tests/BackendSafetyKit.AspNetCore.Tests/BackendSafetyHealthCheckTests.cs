using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class BackendSafetyHealthCheckTests
{
    [Fact]
    public async Task HealthyConfigurationReturnsHealthyStatusWithoutSensitiveValues()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety(options =>
        {
            options.SensitiveDataMasking.MaskValue = "super-secret-mask";
        });
        services.AddBackendSafetyHealthChecks();

        using var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        var report = await healthCheckService.CheckHealthAsync();

        var entry = Assert.Single(report.Entries);

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Equal(HealthStatus.Healthy, entry.Value.Status);
        Assert.Equal(
            "Backend Safety Kit configuration is valid.",
            entry.Value.Description);
        Assert.DoesNotContain(
            "super-secret-mask",
            entry.Value.Data.Values.Select(value => value?.ToString() ?? string.Empty));
    }

    [Fact]
    public async Task InvalidConfigurationReturnsUnhealthyStatusWithoutConfigurationDetails()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddBackendSafety(options =>
        {
            options.Correlation.MaxLength = 0;
        });
        services.AddBackendSafetyHealthChecks();

        using var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        var report = await healthCheckService.CheckHealthAsync();

        var entry = Assert.Single(report.Entries);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal(HealthStatus.Unhealthy, entry.Value.Status);
        Assert.Equal(
            "Backend Safety Kit configuration is invalid.",
            entry.Value.Description);
        Assert.Null(entry.Value.Exception);
        Assert.Contains(
            loggerProvider.Entries,
            x => x.EventId == 4001 && x.LogLevel == LogLevel.Error);
    }

    [Fact]
    public async Task CustomNameAndTagsAreRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafety();
        services.AddBackendSafetyHealthChecks(
            "backend-safety-readiness",
            "ready",
            "backend-safety");

        using var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        var report = await healthCheckService.CheckHealthAsync(
            registrationName: "backend-safety-readiness");

        var entry = Assert.Single(report.Entries);

        Assert.Equal(HealthStatus.Healthy, entry.Value.Status);
    }

    [Fact]
    public async Task HealthCheckSupportsReadinessTagFiltering()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendSafetyHealthChecks(tags: new[] { "ready" });

        using var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        var report = await healthCheckService.CheckHealthAsync(
            predicate: registration => registration.Tags.Contains("ready"));

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Single(report.Entries);
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly RecordingLogger logger = new();

        public IReadOnlyList<LogEntry> Entries => logger.Entries;

        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly List<LogEntry> entries = new();

        public IReadOnlyList<LogEntry> Entries => entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new LogEntry(logLevel, eventId, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed record LogEntry(
        LogLevel LogLevel,
        EventId EventId,
        Exception? Exception);
}
