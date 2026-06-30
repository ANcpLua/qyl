
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Qyl.Run.Internal;

internal sealed class QylResourceRegistry(TimeProvider time)
{
    private readonly Channel<QylResourceState> _events = Channel.CreateUnbounded<QylResourceState>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<string, QylResourceState> _latest = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, TaskCompletionSource> _ready = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, QylResourceState> Snapshot => _latest;

    public ChannelReader<QylResourceState> Events => _events.Reader;

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
        _events.Writer.TryWrite(state);
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
        _events.Writer.TryComplete();
    }

    private TaskCompletionSource Signal(string name)
    {
        return _ready.GetOrAdd(name,
            static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }
}
