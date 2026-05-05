
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

    public IReadOnlyDictionary<string, QylResourceState> Snapshot => _latest;

    public ChannelReader<QylResourceState> Events => _events.Reader;

    public void Publish(string name, ResourceLifecycle lifecycle, int? allocatedPort = null, Uri? endpoint = null,
        string? lastError = null)
    {
        var state = new QylResourceState
        {
            Name = name,
            Lifecycle = lifecycle,
            Timestamp = time.GetUtcNow(),
            AllocatedPort = allocatedPort,
            Endpoint = endpoint,
            LastError = lastError
        };
        _latest[name] = state;
        _events.Writer.TryWrite(state);
    }

    public void Complete()
    {
        _events.Writer.TryComplete();
    }
}
