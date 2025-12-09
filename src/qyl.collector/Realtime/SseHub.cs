// qyl.collector - SSE Hub
// Real-time span streaming via Server-Sent Events

using System.Collections.Concurrent;
using System.Threading.Channels;
using qyl.collector.Storage;

namespace qyl.collector.Realtime;

/// <summary>
/// Pub/sub hub for real-time span streaming via SSE.
/// </summary>
public sealed class SseHub
{
    private readonly ConcurrentDictionary<string, Subscriber> _subscribers = new();

    /// <summary>
    /// Number of active subscribers.
    /// </summary>
    public int SubscriberCount => _subscribers.Count;

    /// <summary>
    /// Subscribes a client to the live stream.
    /// </summary>
    public IDisposable Subscribe(string connectionId, Channel<SpanBatch> channel, string? sessionFilter = null)
    {
        var subscriber = new Subscriber(connectionId, channel, sessionFilter);
        _subscribers[connectionId] = subscriber;

        return new SubscriptionHandle(this, connectionId);
    }

    /// <summary>
    /// Unsubscribes a client.
    /// </summary>
    public void Unsubscribe(string connectionId)
    {
        _subscribers.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Broadcasts a span batch to all subscribers.
    /// Non-blocking - drops if subscriber channel is full.
    /// </summary>
    public void Broadcast(SpanBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        foreach (var (_, subscriber) in _subscribers)
        {
            var batchToSend = batch;

            // Apply session filter if set
            if (subscriber.SessionFilter is not null)
            {
                var filteredSpans = batch.Spans
                    .Where(s => string.Equals(s.SessionId, subscriber.SessionFilter, StringComparison.Ordinal))
                    .ToList();

                if (filteredSpans.Count == 0) continue;

                batchToSend = new SpanBatch(filteredSpans);
            }

            // Non-blocking write - drop if channel full
            subscriber.Channel.Writer.TryWrite(batchToSend);
        }
    }

    /// <summary>
    /// Broadcasts a span batch to all subscribers.
    /// Non-blocking - uses TryWrite to avoid blocking.
    /// </summary>
    public void BroadcastAsync(SpanBatch batch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        foreach (var (_, subscriber) in _subscribers)
        {
            if (ct.IsCancellationRequested) break;

            // Apply session filter if set
            var filteredBatch = batch;
            if (subscriber.SessionFilter is not null)
            {
                var filteredSpans = batch.Spans
                    .Where(s => string.Equals(s.SessionId, subscriber.SessionFilter, StringComparison.Ordinal))
                    .ToList();

                if (filteredSpans.Count == 0) continue;

                filteredBatch = new SpanBatch(filteredSpans);
            }

            // Non-blocking write - drop if channel full
            subscriber.Channel.Writer.TryWrite(filteredBatch);
        }
    }

    private sealed record Subscriber(
        string ConnectionId,
        Channel<SpanBatch> Channel,
        string? SessionFilter);

    private sealed class SubscriptionHandle(SseHub hub, string connectionId) : IDisposable
    {
        public void Dispose() => hub.Unsubscribe(connectionId);
    }
}

/// <summary>
/// SSE event types.
/// </summary>
public static class SseEvents
{
    public const string Connected = "connected";
    public const string Span = "span";
    public const string Batch = "batch";
    public const string Heartbeat = "heartbeat";
}
