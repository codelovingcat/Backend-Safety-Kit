using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using BackendSafetyKit;
using BackendSafetyKit.Abstractions;
using BackendSafetyKit.Redis;
using BackendSafetyKit.Redis.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace BackendSafetyKit.Redis.Tests;

public sealed class BackendSafetyRedisRuntimeTests
{
    [RedisIntegrationFact]
    public async Task EventsAreDeliveredAndRedisStreamLengthIsTrimmed()
    {
        var connection = await ConnectionMultiplexer.ConnectAsync(GetConnectionString());
        var streamKey = $"backend-safety-tests:{Guid.NewGuid():N}";
        var database = connection.GetDatabase();
        await database.KeyDeleteAsync(streamKey);
        var host = await StartRedisHostAsync(connection, streamKey, queueCapacity: 512, maxStreamLength: 100);

        try
        {
            var sink = host.Services.GetRequiredService<IBackendSafetyDiagnosticSink>();
            const int eventCount = 350;
            var lastEventName = $"event-{eventCount - 1}";

            for (var index = 0; index < eventCount; index++)
            {
                sink.Write(CreateEvent($"event-{index}", index));
            }

            var entries = await WaitForEventAsync(database, streamKey, lastEventName);
            Assert.NotEmpty(entries);
            Assert.True(
                entries.Length < eventCount,
                $"Expected approximate Redis stream trimming below {eventCount} entries, got {entries.Length}.");

            var finalEntry = Assert.Single(entries, entry => GetField(entry, "name") == lastEventName);
            var propertiesJson = GetField(finalEntry, "properties");
            Assert.NotNull(propertiesJson);
            using var properties = JsonDocument.Parse(propertiesJson);
            Assert.Equal(eventCount - 1, properties.RootElement.GetProperty("sequence").GetInt32());

            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopTimeout.Token);
            Assert.True(
                host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.IsCancellationRequested);
        }
        finally
        {
            host.Dispose();
            await database.KeyDeleteAsync(streamKey);
            connection.Dispose();
        }
    }

    [RedisIntegrationFact]
    public async Task UnavailableRedisDropsEventsWithoutStoppingTheHost()
    {
        var unavailableConnection = await ConnectionMultiplexer.ConnectAsync(
            "127.0.0.1:1,abortConnect=false,connectTimeout=250,asyncTimeout=250,connectRetry=0");
        var loggerProvider = new CapturingLoggerProvider();
        var streamKey = $"backend-safety-tests:unavailable:{Guid.NewGuid():N}";
        var host = await StartRedisHostAsync(
            unavailableConnection,
            streamKey,
            queueCapacity: 8,
            maxStreamLength: 100,
            loggerProvider);

        try
        {
            var sink = host.Services.GetRequiredService<IBackendSafetyDiagnosticSink>();
            sink.Write(CreateEvent("should-be-dropped", 1));

            await WaitForConditionAsync(
                () => loggerProvider.Messages.Any(message =>
                    message.Contains("could not be written to Redis", StringComparison.Ordinal)),
                TimeSpan.FromSeconds(8));

            Assert.False(
                host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested);
        }
        finally
        {
            await StopHostAsync(host);
            unavailableConnection.Dispose();
        }
    }

    [Fact]
    public async Task BoundedQueueDropsOldestPendingEventOnOverflow()
    {
        var queue = new RedisDiagnosticQueue(Options.Create(new BackendSafetyRedisOptions
        {
            QueueCapacity = 2
        }));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var reader = queue.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();

        Assert.True(queue.TryWrite(CreateEvent("first", 1)));
        Assert.True(queue.TryWrite(CreateEvent("second", 2)));
        Assert.True(queue.TryWrite(CreateEvent("third", 3)));

        Assert.True(await reader.MoveNextAsync());
        Assert.Equal("second", reader.Current.Name);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal("third", reader.Current.Name);
    }

    private static string GetConnectionString() =>
        Environment.GetEnvironmentVariable("REDIS_CONNECTION")
        ?? throw new InvalidOperationException(
            "REDIS_CONNECTION must point to a disposable Redis instance for integration tests.");

    private static async Task<IHost> StartRedisHostAsync(
        IConnectionMultiplexer connection,
        string streamKey,
        int queueCapacity,
        int maxStreamLength,
        ILoggerProvider? loggerProvider = null)
    {
        var builder = Host.CreateApplicationBuilder();
        if (loggerProvider is not null)
        {
            builder.Logging.AddProvider(loggerProvider);
        }

        builder.Services.AddSingleton<IConnectionMultiplexer>(connection);
        builder.Services.AddBackendSafetyRedisDiagnostics(options =>
        {
            options.StreamKey = streamKey;
            options.QueueCapacity = queueCapacity;
            options.MaxStreamLength = maxStreamLength;
        });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static async Task StopHostAsync(IHost host)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(timeout.Token);
        }
        finally
        {
            host.Dispose();
        }
    }

    private static BackendSafetyDiagnosticEvent CreateEvent(string name, int sequence) =>
        new(
            name,
            DateTimeOffset.UtcNow,
            BackendSafetyDiagnosticSeverity.Information,
            new Dictionary<string, object?> { ["sequence"] = sequence });

    private static string? GetField(StreamEntry entry, string name) =>
        entry.Values
            .Where(field => string.Equals(field.Name.ToString(), name, StringComparison.Ordinal))
            .Select(field => field.Value.ToString())
            .FirstOrDefault();

    private static async Task<StreamEntry[]> WaitForEventAsync(
        IDatabase database,
        RedisKey streamKey,
        string eventName)
    {
        var stopwatch = Stopwatch.StartNew();
        do
        {
            var entries = await database.StreamRangeAsync(streamKey);
            if (entries.Any(entry => string.Equals(GetField(entry, "name"), eventName, StringComparison.Ordinal)))
            {
                return entries;
            }

            await Task.Delay(25);
        }
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(10));

        throw new TimeoutException($"Redis stream '{streamKey}' did not receive event '{eventName}'.");
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Redis did not report the expected write failure within the timeout.");
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    messages.Enqueue(formatter(state, exception));
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class RedisIntegrationFactAttribute : FactAttribute
    {
        public RedisIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDIS_CONNECTION")))
            {
                Skip = "Set REDIS_CONNECTION to a disposable Redis service to run this integration test.";
            }
        }
    }
}
