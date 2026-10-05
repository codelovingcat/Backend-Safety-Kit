using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.Correlation;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    private static readonly Action<ILogger, Exception?> DownstreamLog =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2000, "DownstreamRequest"),
            "downstream request log");

    [Fact]
    public async Task ValidCorrelationIdIsPropagatedToAccessorTraceAndResponseHeader()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(loggerProvider);
        var observedAccessorValue = string.Empty;

        var app = BuildPipeline(
            services,
            context =>
            {
                var accessor = context.RequestServices
                    .GetRequiredService<ICorrelationIdAccessor>();

                observedAccessorValue = accessor.CorrelationId ?? string.Empty;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] = "corr-abc-123";
        context.Request.Headers["X-Request-ID"] = "request-fallback";

        using var scope = services.BuildServiceProvider().CreateScope();
        context.RequestServices = scope.ServiceProvider;

        await app(context);

        Assert.Equal("corr-abc-123", observedAccessorValue);
        Assert.Equal("corr-abc-123", context.TraceIdentifier);
        Assert.Equal("corr-abc-123", context.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Fact]
    public async Task MissingCorrelationIdUsesConfiguredGeneratorOnce()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var generatedCount = 0;
        var services = CreateServices(
            loggerProvider,
            options =>
            {
                options.Correlation.Generator = () =>
                {
                    generatedCount++;
                    return "generated-42";
                };
            });

        var app = BuildPipeline(
            services,
            context =>
            {
                Assert.Equal(
                    "generated-42",
                    context.RequestServices
                        .GetRequiredService<ICorrelationIdAccessor>()
                        .CorrelationId);

                return Task.CompletedTask;
            });

        var context = CreateContext();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        context.RequestServices = scope.ServiceProvider;

        await app(context);

        Assert.Equal(1, generatedCount);
        Assert.Equal("generated-42", context.TraceIdentifier);
        Assert.Equal("generated-42", context.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Fact]
    public async Task CorrelationIdTakesPrecedenceOverRequestId()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(loggerProvider);
        var app = BuildPipeline(
            services,
            context =>
            {
                Assert.Equal(
                    "correlation-wins",
                    context.RequestServices
                        .GetRequiredService<ICorrelationIdAccessor>()
                        .CorrelationId);

                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] = "correlation-wins";
        context.Request.Headers["X-Request-ID"] = "request-id";

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        context.RequestServices = scope.ServiceProvider;

        await app(context);

        Assert.Equal(
            "correlation-wins",
            context.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Fact]
    public async Task InvalidCorrelationIdFallsBackToValidRequestId()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(loggerProvider);
        var app = BuildPipeline(services, _ => Task.CompletedTask);

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] = "invalid value";
        context.Request.Headers["X-Request-ID"] = "request-valid-42";

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        context.RequestServices = scope.ServiceProvider;

        await app(context);

        Assert.Equal("request-valid-42", context.TraceIdentifier);
        Assert.Equal(
            "request-valid-42",
            context.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Fact]
    public async Task OversizedCorrelationIdIsReplacedSafely()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options =>
            {
                options.Correlation.MaxLength = 16;
                options.Correlation.Generator = static () => "generated-safe";
            });

        var app = BuildPipeline(services, _ => Task.CompletedTask);

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] =
            new string('x', 17);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        context.RequestServices = scope.ServiceProvider;

        await app(context);

        Assert.Equal("generated-safe", context.TraceIdentifier);
        Assert.Equal(
            "generated-safe",
            context.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Fact]
    public async Task CorrelationHeaderAndIdNamesAreConfigurable()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options =>
            {
                options.Correlation.CorrelationIdHeaderName = "X-Trace-Token";
                options.Correlation.RequestIdHeaderName = "X-Legacy-Request";
                options.Correlation.ResponseHeaderName = "X-Trace-Token-Response";
            });

        var app = BuildPipeline(services, _ => Task.CompletedTask);

        var context = CreateContext();
        context.Request.Headers["X-Trace-Token"] = "trace-77";
        context.Request.Headers["X-Legacy-Request"] = "legacy-88";

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        context.RequestServices = scope.ServiceProvider;

        await app(context);

        Assert.Equal("trace-77", context.TraceIdentifier);
        Assert.Equal(
            "trace-77",
            context.Response.Headers["X-Trace-Token-Response"].ToString());
        Assert.False(context.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task ResponseHeaderCanBeDisabled()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options => options.Correlation.IncludeResponseHeader = false);

        var app = BuildPipeline(services, _ => Task.CompletedTask);

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] = "no-response-header";

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        context.RequestServices = scope.ServiceProvider;

        await app(context);

        Assert.Equal("no-response-header", context.TraceIdentifier);
        Assert.False(context.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task DownstreamLoggingReceivesCorrelationIdScope()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(loggerProvider);

        var app = BuildPipeline(
            services,
            _ =>
            {
                var logger = _.RequestServices.GetRequiredService<ILogger<CorrelationIdMiddlewareTests>>();
                DownstreamLog(logger, null);
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] = "log-correlation-42";

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        context.RequestServices = scope.ServiceProvider;

        await app(context);

        Assert.Equal("log-correlation-42", loggerProvider.CorrelationIdFromScope);
    }

    private static ServiceCollection CreateServices(
        RecordingLoggerProvider loggerProvider,
        Action<BackendSafetyOptions>? configure = null)
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

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            Request =
            {
                Method = HttpMethods.Get,
                Path = "/test"
            }
        };
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly RecordingLogger _logger = new();

        public string? CorrelationIdFromScope => _logger.CorrelationIdFromScope;

        public ILogger CreateLogger(string categoryName) => _logger;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public string? CorrelationIdFromScope { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values &&
                values.FirstOrDefault(x => x.Key == "CorrelationId") is { } value)
            {
                CorrelationIdFromScope = value.Value?.ToString();
            }

            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
