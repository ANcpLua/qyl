// Copyright (c) 2025-2026 ancplua

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Qyl.Run.Internal;

/// <summary>
///     Thread-safe ledger of the orchestrator's runtime view of every resource. The orchestrator
///     writes new <see cref="QylResourceState" /> snapshots, the Spectre UI reads the latest
///     snapshot per resource on every repaint tick.
/// </summary>
internal sealed class QylResourceRegistry(TimeProvider time)
{
    private readonly Channel<QylResourceState> _events = Channel.CreateUnbounded<QylResourceState>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<string, QylResourceState> _latest = new(StringComparer.Ordinal);

    /// <summary>Latest snapshot for every resource, keyed by <see cref="QylResource.Name" />.</summary>
    public IReadOnlyDictionary<string, QylResourceState> Snapshot => _latest;

    /// <summary>Event stream — UI loop consumes this to know when to repaint.</summary>
    public ChannelReader<QylResourceState> Events => _events.Reader;

    /// <summary>
    ///     Publish a state change. Stamps <see cref="QylResourceState.Timestamp" /> from the injected
    ///     <see cref="TimeProvider" />.
    /// </summary>
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

    /// <summary>Signal the UI to stop polling (orchestrator shutting down).</summary>
    public void Complete()
    {
        _events.Writer.TryComplete();
    }
}
