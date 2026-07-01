
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Qyl.Run.Internal;

internal sealed class QylResourceRegistry(TimeProvider time)
{
    // Broadcast fan-out: every subscriber (the Spectre TUI, an SSE endpoint, …) owns its own channel.
    // Publish records the latest state and mirrors the event into every subscriber's channel, so a second
    // consumer never steals events from the first (the old single-reader channel could not be shared).
    private readonly ConcurrentDictionary<Guid, Channel<QylResourceState>> _subscribers = new();

    private readonly ConcurrentDictionary<string, QylResourceState> _latest = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, TaskCompletionSource> _ready = new(StringComparer.Ordinal);

    private volatile bool _completed;

    public IReadOnlyDictionary<string, QylResourceState> Snapshot => _latest;

    // Subscribe first, THEN read Snapshot: an event racing the subscription is still delivered on the
    // channel, and because state is keyed by name (last-write-wins) a duplicate replay is idempotent.
    // Dispose the subscription to unsubscribe.
    public QylResourceSubscription Subscribe()
    {
        var channel = Channel.CreateUnbounded<QylResourceState>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        var id = Guid.NewGuid();
        _subscribers[id] = channel;
        if (_completed) channel.Writer.TryComplete();
        return new QylResourceSubscription(this, id, channel.Reader);
    }

    internal void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel)) channel.Writer.TryComplete();
    }

    public void Publish(string name, ResourceLifecycle lifecycle, int? allocatedPort = null, Uri? endpoint = null,
        string? lastError = null)
    {
        Publish(new QylResourceState
        {
            Name = name,
            Lifecycle = lifecycle,
            Timestamp = time.GetUtcNow(),
            AllocatedPort = allocatedPort,
            Endpoint = endpoint,
            LastError = lastError
        });
    }

    public void Publish(QylResourceState state)
    {
        _latest[state.Name] = state;
        if (state.Lifecycle == ResourceLifecycle.Ready) Signal(state.Name).TrySetResult();
        foreach (var channel in _subscribers.Values) channel.Writer.TryWrite(state);
    }

    // Completes the first time the named resource reaches Ready. Subscribing (Signal) before the recheck
    // closes the race with Publish: a Ready arriving in between still completes the same TaskCompletionSource.
    public Task WhenReadyAsync(string name, CancellationToken cancellationToken)
    {
        var tcs = Signal(name);
        if (_latest.TryGetValue(name, out var state) && state.Lifecycle == ResourceLifecycle.Ready)
        {
            tcs.TrySetResult();
        }

        return tcs.Task.WaitAsync(cancellationToken);
    }

    public Task WhenAllReadyAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        return Task.WhenAll(names.Select(name => WhenReadyAsync(name, cancellationToken)));
    }

    public void Complete()
    {
        _completed = true;
        foreach (var channel in _subscribers.Values) channel.Writer.TryComplete();
    }

    private TaskCompletionSource Signal(string name)
    {
        return _ready.GetOrAdd(name,
            static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }
}

// One consumer's view of the broadcast. Read Events after taking a Snapshot; dispose to release the channel.
internal sealed class QylResourceSubscription(
    QylResourceRegistry registry,
    Guid id,
    ChannelReader<QylResourceState> reader) : IDisposable
{
    public ChannelReader<QylResourceState> Events => reader;

    public void Dispose() => registry.Unsubscribe(id);
}
