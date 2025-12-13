using System.Collections.Concurrent;
using System.Threading.Channels;
using qyl.collector.Storage;

namespace qyl.collector.Realtime;

public interface ITelemetrySseBroadcaster : IAsyncDisposable
{
    int ClientCount { get; }
    ChannelReader<TelemetryMessage> Subscribe(Guid clientId);
    void Unsubscribe(Guid clientId);
    void Publish(TelemetryMessage item);
    void PublishSpans(SpanBatch batch);
}

public sealed class TelemetrySseBroadcaster : ITelemetrySseBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<TelemetryMessage>> _channels = new();
    private volatile bool _disposed;

    public int ClientCount => _channels.Count;

    public ChannelReader<TelemetryMessage> Subscribe(Guid clientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateBounded<TelemetryMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        });

        _channels[clientId] = channel;
        return channel.Reader;
    }

    public void Unsubscribe(Guid clientId)
    {
        if (_channels.TryRemove(clientId, out var channel))
            channel.Writer.TryComplete();
    }

    public void Publish(TelemetryMessage item)
    {
        if (_disposed) return;

        foreach (var channel in _channels.Values)
            channel.Writer.TryWrite(item);
    }

    public void PublishSpans(SpanBatch batch)
    {
        var message = new TelemetryMessage(TelemetrySignal.Spans, batch, TimeProvider.System.GetUtcNow());
        Publish(message);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        foreach (var channel in _channels.Values)
            channel.Writer.TryComplete();

        _channels.Clear();
        return ValueTask.CompletedTask;
    }
}

public enum TelemetrySignal
{
    Connected = 0,
    Spans = 1,
    Metrics = 2,
    Logs = 3,
    Heartbeat = 4
}

public sealed record TelemetryMessage(
    TelemetrySignal Signal,
    object? Data,
    DateTimeOffset Timestamp);

public sealed record TelemetryEventDto(
    string EventType,
    object? Data,
    DateTimeOffset Timestamp);
