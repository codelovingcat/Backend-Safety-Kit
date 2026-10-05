using System.Net;
using System.Text.Json;
using BackendSafetyKit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BackendSafetyKit.AspNetCore.Tests;

public sealed class SampleApiIntegrationTests
{
    [Fact]
    public async Task SuccessfulRequestRunsThroughConfiguredPipeline()
    {
        using var factory = new TestFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/orders/42");

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", GetHeader(response, "X-Content-Type-Options"));
        Assert.False(string.IsNullOrWhiteSpace(GetHeader(response, "X-Correlation-ID")));
        Assert.Contains(""id":"42"", body);
        Assert.Contains(""status":"ready"", body);
    }

    [Fact]
    public async Task MissingCorrelationIdGeneratesSafeIdentifierAndInvalidIdentifierIsIgnored()
    {
        using var factory = new TestFactory();
        using var client = factory.CreateClient();

        using var missingResponse = await client.GetAsync("/api/orders/42");
        using var invalidRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/orders/42");
        invalidRequest.Headers.TryAddWithoutValidation(
            "X-Correlation-ID",
            "not allowed\r\nvalue");

        using var invalidResponse = await client.SendAsync(invalidRequest);

        var generatedId = GetHeader(missingResponse, "X-Correlation-ID");
        var invalidGeneratedId = GetHeader(invalidResponse, "X-Correlation-ID");

        Assert.False(string.IsNullOrWhiteSpace(generatedId));
        Assert.False(string.IsNullOrWhiteSpace(invalidGeneratedId));
        Assert.NotEqual("not allowed\r\nvalue", invalidGeneratedId);
    }

    [Fact]
    public async Task RequestIdFallbackIsPropagated()
    {
        using var factory = new TestFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/orders/42");
        request.Headers.Add("X-Request-ID", "request-42");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("request-42", GetHeader(response, "X-Correlation-ID"));
    }

    [Fact]
    public async Task KnownExceptionIsMappedToNotFoundWithoutLeakingDetails()
    {
        using var factory = new TestFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/orders/missing");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("An error occurred while processing the request.", body);
        Assert.DoesNotContain("order-42", body);
        Assert.DoesNotContain("KeyNotFoundException", body);
    }

    [Fact]
    public async Task ValidationStyleExceptionReturnsBadRequestWithoutLeakingDetails()
    {
        using var factory = new TestFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/validation");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.ToString());
        Assert.DoesNotContain("validation details must not be exposed", body);
        Assert.DoesNotContain("ArgumentException", body);
    }

    [Fact]
    public async Task SlowEndpointCompletesAndProducesTimingDiagnostics()
    {
        using var factory = new TestFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/slow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            factory.Logs,
            log => log.EventId == 3001 &&
                log.Properties.TryGetValue("RequestPath", out var requestPath) &&
                requestPath?.ToString() == "/api/slow");
    }

    [Fact]
    public async Task SensitiveStructuredFieldsAreMasked()
    {
        using var factory = new TestFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/diagnostics/masked");
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("demo-user", root.GetProperty("username").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("password").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("accessToken").GetString());
    }

    [Fact]
    public async Task SensitiveAllowlistedHeaderIsMaskedInStructuredLogs()
    {
        using var factory = new TestFactory();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/orders/42");
        request.Headers.TryAddWithoutValidation(
            "X-Api-Key",
            "super-secret-api-key");

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var log = Assert.Single(
            factory.Logs,
            entry =>
                entry.EventId == 2001 &&
                entry.Properties.TryGetValue("RequestHeaders", out var headers) &&
                headers?.ToString()?.Contains("[REDACTED]", StringComparison.Ordinal) == true);

        Assert.NotNull(log);
    }

    [Fact]
    public async Task HealthEndpointReportsHealthyWithoutExposingConfiguration()
    {
        using var factory = new TestFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", body, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetHeader(
        HttpResponseMessage response,
        string name)
    {
        return response.Headers.TryGetValues(name, out var values)
            ? Assert.Single(values)
            : response.Content.Headers.TryGetValues(name, out values)
                ? Assert.Single(values)
                : null;
    }

    private sealed class TestFactory : WebApplicationFactory<Program>
    {
        private readonly RecordingLoggerProvider loggerProvider = new();

        public IReadOnlyList<LogEntry> Logs => loggerProvider.Entries;

        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddProvider(loggerProvider);
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                loggerProvider.Dispose();
            }

            base.Dispose(disposing);
        }
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
        private readonly object sync = new();
        private readonly List<LogEntry> entries = new();

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (sync)
                {
                    return entries.ToArray();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information;

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

            lock (sync)
            {
                entries.Add(
                    new LogEntry(
                        logLevel,
                        eventId,
                        properties,
                        formatter(state, exception)));
            }
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
