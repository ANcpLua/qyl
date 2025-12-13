using System.Collections.Concurrent;
using System.Threading.Channels;
using Qyl;
using qyl.collector.Storage;

namespace qyl.collector.Realtime;

public sealed class SseHub
{
    private readonly ConcurrentDictionary<string, Subscriber> _subscribers = new();

    public int SubscriberCount => _subscribers.Count;

    public IDisposable Subscribe(string connectionId, Channel<SpanBatch> channel, string? sessionFilter = null)
    {
        var subscriber = new Subscriber(connectionId, channel, sessionFilter);
        _subscribers[connectionId] = subscriber;

        return new SubscriptionHandle(this, connectionId);
    }

    public void Unsubscribe(string connectionId)
    {
        _subscribers.TryRemove(connectionId, out _);
    }

    public void Broadcast(SpanBatch batch)
    {
        Throw.IfNull(batch);

        foreach (var (_, subscriber) in _subscribers)
        {
            var batchToSend = batch;

            if (subscriber.SessionFilter is not null)
            {
                var filteredSpans = batch.Spans
                    .Where(s => string.Equals(s.SessionId, subscriber.SessionFilter, StringComparison.Ordinal))
                    .ToList();

                if (filteredSpans.Count == 0) continue;

                batchToSend = new SpanBatch(filteredSpans);
            }

            subscriber.Channel.Writer.TryWrite(batchToSend);
        }
    }

    public void Broadcast(SpanBatch batch, CancellationToken ct = default)
    {
        Throw.IfNull(batch);

        foreach (var (_, subscriber) in _subscribers)
        {
            if (ct.IsCancellationRequested) break;

            var filteredBatch = batch;
            if (subscriber.SessionFilter is not null)
            {
                var filteredSpans = batch.Spans
                    .Where(s => string.Equals(s.SessionId, subscriber.SessionFilter, StringComparison.Ordinal))
                    .ToList();

                if (filteredSpans.Count == 0) continue;

                filteredBatch = new SpanBatch(filteredSpans);
            }

            subscriber.Channel.Writer.TryWrite(filteredBatch);
        }
    }

    private sealed record Subscriber(
        string ConnectionId,
        Channel<SpanBatch> Channel,
        string? SessionFilter);

    private sealed class SubscriptionHandle(SseHub hub, string connectionId) : IDisposable
    {
        public void Dispose()
        {
            hub.Unsubscribe(connectionId);
        }
    }
}

public static class SseEvents
{
    public const string Connected = "connected";
    public const string Span = "span";
    public const string Batch = "batch";
    public const string Heartbeat = "heartbeat";
}