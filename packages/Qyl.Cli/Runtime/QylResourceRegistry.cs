
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Qyl.Cli.Runtime;

internal sealed class QylResourceRegistry(IReadOnlyList<QylResource> resources, TimeProvider time)
{
    private readonly Dictionary<string, QylResourceKind> _kinds =
        resources.ToDictionary(static r => r.Name, static r => r.Kind, StringComparer.Ordinal);

    // Broadcast fan-out: every subscriber owns one pending resync signal. State itself remains in
    // _latest, so a burst is conflated without ever dropping a resource's final state.
    private readonly ConcurrentDictionary<Guid, Channel<byte>> _subscribers = new();

    private readonly ConcurrentDictionary<string, QylResourceState> _latest = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, TaskCompletionSource> _ready = new(StringComparer.Ordinal);

    private volatile bool _completed;

    public IReadOnlyDictionary<string, QylResourceState> Snapshot => _latest;

    public bool Contains(string name) => _kinds.ContainsKey(name);

    // Subscribe first, THEN read Snapshot: an event racing the subscription is still delivered on the
    // channel, and because state is keyed by name (last-write-wins) a duplicate replay is idempotent.
    // Dispose the subscription to unsubscribe.
    public QylResourceSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<byte>(
            new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite
            });

        var id = Guid.NewGuid();
        _subscribers[id] = channel;
        if (_completed) channel.Writer.TryComplete();
        return new QylResourceSubscription(this, id, channel.Reader);
    }

    internal void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel)) channel.Writer.TryComplete();
    }

    // Completes when the named resource first reports Ready. Never completes for a resource that
    // only ever fails — pair with Snapshot polling to observe terminal failure.
    public Task WhenReady(string name) => Signal(name).Task;

    public void Publish(string name, ResourceLifecycle lifecycle, int? allocatedPort = null, Uri? endpoint = null,
        string? lastError = null)
    {
        Publish(new QylResourceState
        {
            Name = name,
            Lifecycle = lifecycle,
            Timestamp = time.GetUtcNow(),
            Kind = _kinds.GetValueOrDefault(name),
            AllocatedPort = allocatedPort,
            Endpoint = endpoint,
            LastError = lastError
        });
    }

    public void Publish(QylResourceState state)
    {
        _latest[state.Name] = state;
        if (state.Lifecycle == ResourceLifecycle.Ready) Signal(state.Name).TrySetResult();
        foreach (var channel in _subscribers.Values) channel.Writer.TryWrite(0);
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
    ChannelReader<byte> reader) : IDisposable
{
    public ChannelReader<byte> Events => reader;

    public void Dispose() => registry.Unsubscribe(id);
}
