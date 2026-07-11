using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Qyl.Host.Internal;

// Bounded, per-resource log buffer with broadcast fan-out — the log analogue of QylResourceRegistry.
// Producers (the process launcher; the container `docker logs -f` follower) Append lines; consumers take a
// Snapshot of recent lines and Subscribe for subsequent ones (the /runner API, and through it the runner console).
internal sealed class QylLogStore
{
    private const int MaxLinesPerResource = 500;

    private readonly ConcurrentDictionary<string, LineBuffer> _buffers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, LogSubscriber> _subscribers = new();

    public void Append(string resource, bool isError, string line)
    {
        var entry = new QylLogLine { Resource = resource, Stream = isError ? "err" : "out", Line = line };
        _buffers.GetOrAdd(resource, static _ => new LineBuffer(MaxLinesPerResource)).Add(entry);
        foreach (var subscriber in _subscribers.Values)
        {
            if (string.Equals(subscriber.Resource, resource, StringComparison.Ordinal))
            {
                subscriber.Channel.Writer.TryWrite(entry);
            }
        }
    }

    public IReadOnlyList<QylLogLine> Snapshot(string resource) =>
        _buffers.TryGetValue(resource, out var buffer) ? buffer.Snapshot() : [];

    public QylLogSubscription Subscribe(string resource)
    {
        var channel = Channel.CreateUnbounded<QylLogLine>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        var id = Guid.NewGuid();
        _subscribers[id] = new LogSubscriber(resource, channel);
        return new QylLogSubscription(this, id, channel.Reader);
    }

    internal void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var subscriber)) subscriber.Channel.Writer.TryComplete();
    }

    private readonly record struct LogSubscriber(string Resource, Channel<QylLogLine> Channel);

    private sealed class LineBuffer(int capacity)
    {
        private readonly Queue<QylLogLine> _lines = new();
        private readonly Lock _gate = new();

        public void Add(QylLogLine line)
        {
            lock (_gate)
            {
                _lines.Enqueue(line);
                while (_lines.Count > capacity) _lines.Dequeue();
            }
        }

        public IReadOnlyList<QylLogLine> Snapshot()
        {
            lock (_gate) return [.. _lines];
        }
    }
}

internal sealed record QylLogLine
{
    public required string Resource { get; init; }
    public required string Stream { get; init; }
    public required string Line { get; init; }
}

internal sealed class QylLogSubscription(QylLogStore store, Guid id, ChannelReader<QylLogLine> reader) : IDisposable
{
    public ChannelReader<QylLogLine> Events => reader;

    public void Dispose() => store.Unsubscribe(id);
}
