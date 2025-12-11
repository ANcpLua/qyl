using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using qyl.grpc.Abstractions;

namespace qyl.grpc.Streaming;

public sealed class TelemetryBroadcaster : ITelemetryBroadcaster
{
    private readonly ITelemetrySseBroadcaster? _sseBroadcaster;
    private readonly ConcurrentDictionary<Guid, Channel<TelemetryMessage>> _subscribers = new();

    public TelemetryBroadcaster()
    {
    }

    public TelemetryBroadcaster(ITelemetrySseBroadcaster sseBroadcaster) =>
        _sseBroadcaster = sseBroadcaster;

    public int ConnectionCount => _sseBroadcaster?.ClientCount ?? _subscribers.Count;

    public ValueTask BroadcastAsync<T>(TelemetrySignal signal, T data, CancellationToken ct = default)
    {
        var message = new TelemetryMessage(signal, data!, DateTimeOffset.UtcNow);

        _sseBroadcaster?.Publish(message);

        foreach ((Guid _, Channel<TelemetryMessage> channel) in _subscribers) channel.Writer.TryWrite(message);

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<TelemetryMessage> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<TelemetryMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _subscribers.TryAdd(id, channel);

        try
        {
            await foreach (TelemetryMessage message in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false)) yield return message;
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
        }
    }
}
