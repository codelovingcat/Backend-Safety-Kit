using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class RequestTimingMiddlewareTests
{
    [Fact]
    public async Task RequestDurationIsMeasuredAndCompletionHookReceivesDiagnostics()
    {
        RequestTimingContext? observed = null;
        var services = CreateServices(options =>
        {
            options.RequestTiming.OnCompleted = context => observed = context;
        });

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] = "timing-42";

        await InvokeAsync(app, services, context);

        Assert.NotNull(observed);
        var timing = observed!;
        Assert.Equal("GET", timing.Method);
        Assert.Equal("/timing", timing.Path);
        Assert.Equal(StatusCodes.Status204NoContent, timing.StatusCode);
        Assert.Equal("timing-42", timing.CorrelationId);
        Assert.True(timing.Duration >= TimeSpan.Zero);
        Assert.False(timing.IsSlow);
    }

    [Fact]
    public async Task ThresholdMarksRequestAsSlowWithoutWaitingForWallClockTime()
    {
        RequestTimingContext? observed = null;
        var services = CreateServices(options =>
        {
            options.RequestTiming.SlowRequestThreshold = TimeSpan.Zero;
            options.RequestTiming.OnCompleted = context => observed = context;
        });

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        await InvokeAsync(app, services, CreateContext());

        Assert.NotNull(observed);
        var timing = observed!;
        Assert.True(timing.IsSlow);
        Assert.True(timing.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task SlowRequestIsLoggedAtConfiguredLevel()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options =>
            {
                options.RequestTiming.SlowRequestThreshold = TimeSpan.Zero;
                options.RequestTiming.EnableSlowRequestLogging = true;
                options.RequestTiming.SlowRequestLogLevel = RequestTimingLogLevel.Error;
            });

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] = "slow-42";

        await InvokeAsync(app, services, context);

        var entry = Assert.Single(
            loggerProvider.Entries,
            x => x.EventId == 3001);

        Assert.Equal(LogLevel.Error, entry.LogLevel);
        Assert.Equal("slow-42", entry.Properties["CorrelationId"]?.ToString());
        Assert.Equal("/timing", entry.Properties["RequestPath"]?.ToString());
        Assert.Equal(200, entry.Properties["StatusCode"]);
        Assert.True(Convert.ToDouble(
            entry.Properties["DurationMs"],
            System.Globalization.CultureInfo.InvariantCulture) >= 0);
    }

    [Fact]
    public async Task SlowRequestLoggingIsDisabledByDefault()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options => options.RequestTiming.SlowRequestThreshold = TimeSpan.Zero);

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        await InvokeAsync(app, services, CreateContext());

        Assert.DoesNotContain(loggerProvider.Entries, x => x.EventId == 3001);
    }

    [Fact]
    public async Task CompletionHookFailureDoesNotBreakRequestProcessing()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options => options.RequestTiming.OnCompleted = _ =>
                throw new InvalidOperationException("hook-failure"));

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();

        await app(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains(
            loggerProvider.Entries,
            x => x.EventId == 3002 &&
                x.LogLevel == LogLevel.Error);
    }

    private static ServiceCollection CreateServices(
        Action<BackendSafetyOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(new RecordingLoggerProvider()));
        services.AddBackendSafety(configure);
        return services;
    }

    private static ServiceCollection CreateServices(
        RecordingLoggerProvider loggerProvider,
        Action<BackendSafetyOptions>? configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddBackendSafety(configure);
        return services;
    }

    private static RequestDelegate BuildPipeline(
        IServiceCollection services,
        RequestDelegate terminal)
    {
        var provider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(provider);

        builder.UseBackendSafety();
        builder.Run(terminal);

        return builder.Build();
    }

    private static DefaultHttpContext CreateContext() =>
        new()
        {
            Request =
            {
                Method = HttpMethods.Get,
                Path = "/timing"
            }
        };

    private static async Task InvokeAsync(
        RequestDelegate app,
        IServiceCollection services,
        DefaultHttpContext context)
    {
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        context.RequestServices = scope.ServiceProvider;
        await app(context);
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
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(x => x.Key, x => x.Value)
                : new Dictionary<string, object?>();

            entries.Add(
                new LogEntry(
                    logLevel,
                    eventId,
                    properties,
                    formatter(state, exception)));
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
        IReadOnlyDictionary<string, object?> Properties,
        string RenderedMessage);
}
