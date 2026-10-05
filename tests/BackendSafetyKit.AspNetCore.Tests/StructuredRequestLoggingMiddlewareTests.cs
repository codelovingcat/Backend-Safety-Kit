using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class StructuredRequestLoggingMiddlewareTests
{
    [Fact]
    public async Task SuccessfulRequestProducesOneStructuredCompletionLog()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(loggerProvider);

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] = "corr-42";

        await InvokeAsync(app, services, context);

        var entry = Assert.Single(loggerProvider.Entries, x => x.EventId == 2001);

        Assert.Equal(LogLevel.Information, entry.LogLevel);
        Assert.Equal("/orders/42", entry.Properties["RequestPath"]?.ToString());
        Assert.Equal("GET", entry.Properties["HttpMethod"]?.ToString());
        Assert.Equal(200, entry.Properties["StatusCode"]);
        Assert.Equal("corr-42", entry.Properties["CorrelationId"]?.ToString());
        Assert.Equal("api.example.test", entry.Properties["Host"]?.ToString());
        Assert.Equal(false, entry.Properties["IsFailure"]);
        Assert.Null(entry.Properties["FailureType"]);
        Assert.True(Convert.ToDouble(entry.Properties["DurationMs"], System.Globalization.CultureInfo.InvariantCulture) >= 0);
    }

    [Fact]
    public async Task DisabledRequestLoggingProducesNoCompletionLog()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options => options.Features.EnableRequestLogging = false);

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        await InvokeAsync(app, services, CreateContext());

        Assert.DoesNotContain(
            loggerProvider.Entries,
            entry => entry.EventId.Id is >= 2001 and <= 2003);
    }

    [Fact]
    public async Task FailedResponseProducesErrorCompletionLogWithFailureContext()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(loggerProvider);

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Headers["X-Correlation-ID"] = "failure-42";

        await InvokeAsync(app, services, context);

        var entry = Assert.Single(loggerProvider.Entries, x => x.EventId == 2003);

        Assert.Equal(LogLevel.Error, entry.LogLevel);
        Assert.Equal(500, entry.Properties["StatusCode"]);
        Assert.Equal("failure-42", entry.Properties["CorrelationId"]?.ToString());
        Assert.Equal(true, entry.Properties["IsFailure"]);
    }


    [Fact]
    public async Task AllowlistedSensitiveHeadersAreMaskedBeforeLogging()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options =>
            {
                options.RequestLogging.AllowedRequestHeaders.Add("Authorization");
                options.RequestLogging.AllowedResponseHeaders.Add("Set-Cookie");
                options.RequestLogging.DeniedRequestHeaders.Remove("Authorization");
                options.RequestLogging.DeniedResponseHeaders.Remove("Set-Cookie");
            });

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.Headers["Set-Cookie"] = "session=response-secret";
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Headers["Authorization"] = "Bearer request-secret";

        await InvokeAsync(app, services, context);

        var entry = Assert.Single(loggerProvider.Entries, x => x.EventId == 2001);
        var requestHeaders =
            Assert.IsType<Dictionary<string, string>>(entry.Properties["RequestHeaders"]);
        var responseHeaders =
            Assert.IsType<Dictionary<string, string>>(entry.Properties["ResponseHeaders"]);

        Assert.Equal("[REDACTED]", requestHeaders["Authorization"]);
        Assert.Equal("[REDACTED]", responseHeaders["Set-Cookie"]);
        Assert.DoesNotContain("request-secret", entry.RenderedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("response-secret", entry.RenderedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllowedHeadersAreLoggedWhileDeniedHeadersRemainExcluded()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options =>
            {
                options.RequestLogging.AllowedRequestHeaders.Add("X-Tenant");
                options.RequestLogging.AllowedRequestHeaders.Add("Authorization");
                options.RequestLogging.AllowedResponseHeaders.Add("Content-Type");
                options.RequestLogging.AllowedResponseHeaders.Add("Set-Cookie");
            });

        var app = BuildPipeline(
            services,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                context.Response.Headers["Content-Type"] = "application/json";
                context.Response.Headers["Set-Cookie"] = "session=secret";
                return Task.CompletedTask;
            });

        var context = CreateContext();
        context.Request.Headers["X-Tenant"] = "tenant-17";
        context.Request.Headers["Authorization"] = "Bearer very-secret-token";

        await InvokeAsync(app, services, context);

        var entry = Assert.Single(loggerProvider.Entries, x => x.EventId == 2001);
        var requestHeaders =
            Assert.IsType<Dictionary<string, string>>(entry.Properties["RequestHeaders"]);
        var responseHeaders =
            Assert.IsType<Dictionary<string, string>>(entry.Properties["ResponseHeaders"]);

        Assert.Equal("tenant-17", requestHeaders["X-Tenant"]);
        Assert.False(requestHeaders.ContainsKey("Authorization"));
        Assert.Equal("application/json", responseHeaders["Content-Type"]);
        Assert.False(responseHeaders.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task SensitivePayloadAndQueryStringAreNotLoggedByDefault()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(loggerProvider);

        var app = BuildPipeline(
            services,
            async context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                using var reader = new StreamReader(context.Request.Body);
                _ = await reader.ReadToEndAsync();
            });

        var context = CreateContext();
        context.Request.QueryString = new QueryString("?token=query-secret");
        context.Request.Headers["Authorization"] = "Bearer header-secret";
        context.Request.Headers["Cookie"] = "session=cookie-secret";
        context.Request.Body = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes("password=body-secret"));

        await InvokeAsync(app, services, context);

        var entry = Assert.Single(loggerProvider.Entries, x => x.EventId == 2001);

        Assert.Null(entry.Properties["RequestHeaders"]);
        Assert.DoesNotContain("query-secret", entry.RenderedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("header-secret", entry.RenderedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("cookie-secret", entry.RenderedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("body-secret", entry.RenderedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MetadataValuesAreTruncatedToConfiguredLimit()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = CreateServices(
            loggerProvider,
            options =>
            {
                options.RequestLogging.AllowedRequestHeaders.Add("X-Tenant");
                options.RequestLogging.MaxMetadataValueLength = 8;
            });

        var app = BuildPipeline(services, context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        var context = CreateContext();
        context.Request.Headers["X-Tenant"] = "1234567890";

        await InvokeAsync(app, services, context);

        var entry = Assert.Single(loggerProvider.Entries, x => x.EventId == 2001);
        var requestHeaders =
            Assert.IsType<Dictionary<string, string>>(entry.Properties["RequestHeaders"]);

        Assert.Equal("1234567…", requestHeaders["X-Tenant"]);
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

    private static DefaultHttpContext CreateContext() =>
        new()
        {
            Request =
            {
                Method = HttpMethods.Get,
                Path = "/orders/42",
                Host = new HostString("api.example.test")
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
        private readonly RecordingLogger _logger = new();

        public IReadOnlyList<LogEntry> Entries => _logger.Entries;

        public ILogger CreateLogger(string categoryName) => _logger;

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
