// qyl.collector - Telemetry SSE Broadcaster
// Thread-safe SSE broadcasting using bounded channels with DropOldest backpressure

using System.Collections.Concurrent;
using System.Threading.Channels;
using qyl.collector.Storage;

namespace qyl.collector.Realtime;

/// <summary>
/// Thread-safe SSE broadcaster using bounded channels with DropOldest backpressure.
/// </summary>
public interface ITelemetrySseBroadcaster : IAsyncDisposable
{
    int ClientCount { get; }
    ChannelReader<TelemetryMessage> Subscribe(Guid clientId);
    void Unsubscribe(Guid clientId);
    void Publish(TelemetryMessage item);
    void PublishSpans(SpanBatch batch);
}

/// <summary>
/// Implementation of thread-safe SSE broadcaster.
/// </summary>
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
            SingleWriter = false,  // Multiple publishers (trace, metric, log handlers)
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
        var message = new TelemetryMessage(TelemetrySignal.Spans, batch, DateTimeOffset.UtcNow);
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

/// <summary>
/// Telemetry signal types.
/// </summary>
public enum TelemetrySignal
{
    Connected = 0,
    Spans = 1,
    Metrics = 2,
    Logs = 3,
    Heartbeat = 4
}

/// <summary>
/// Telemetry message for SSE streaming.
/// </summary>
public sealed record TelemetryMessage(
    TelemetrySignal Signal,
    object? Data,
    DateTimeOffset Timestamp);

/// <summary>
/// DTO for SSE event payload.
/// </summary>
public sealed record TelemetryEventDto(
    string EventType,
    object? Data,
    DateTimeOffset Timestamp);
