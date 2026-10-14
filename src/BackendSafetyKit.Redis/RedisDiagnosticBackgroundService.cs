using System.Globalization;
using System.Text.Json;
using BackendSafetyKit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BackendSafetyKit.Redis;

internal sealed class RedisDiagnosticBackgroundService(
    RedisDiagnosticQueue queue,
    IOptions<BackendSafetyRedisOptions> options,
    IConnectionMultiplexer connection,
    ILogger<RedisDiagnosticBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var database = connection.GetDatabase();

        await foreach (var diagnosticEvent in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                var properties = JsonSerializer.Serialize(diagnosticEvent.Properties);
                var fields = new[]
                {
                    new NameValueEntry("timestamp", diagnosticEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture)),
                    new NameValueEntry("name", diagnosticEvent.Name),
                    new NameValueEntry("severity", diagnosticEvent.Severity.ToString()),
                    new NameValueEntry("properties", properties)
                };

                await database.StreamAddAsync(
                    options.Value.StreamKey,
                    fields,
                    maxLength: options.Value.MaxStreamLength,
                    useApproximateMaxLength: true).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is RedisException or TimeoutException or JsonException or NotSupportedException)
            {
                logger.LogWarning(
                    exception,
                    "A Backend Safety diagnostic event could not be written to Redis and was dropped.");
            }
        }
    }
}
