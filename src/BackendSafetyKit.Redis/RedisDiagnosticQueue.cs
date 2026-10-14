using System.Threading.Channels;
using BackendSafetyKit;

namespace BackendSafetyKit.Redis;

internal sealed class RedisDiagnosticQueue
{
    private readonly Channel<BackendSafetyDiagnosticEvent> _channel;

    public RedisDiagnosticQueue(Microsoft.Extensions.Options.IOptions<BackendSafetyRedisOptions> options)
    {
        _channel = Channel.CreateBounded<BackendSafetyDiagnosticEvent>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
    }

    public bool TryWrite(BackendSafetyDiagnosticEvent diagnosticEvent) =>
        _channel.Writer.TryWrite(diagnosticEvent);

    public IAsyncEnumerable<BackendSafetyDiagnosticEvent> ReadAllAsync(
        CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
