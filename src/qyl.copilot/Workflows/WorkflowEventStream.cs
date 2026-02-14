// =============================================================================
// qyl.copilot - Workflow Event Stream
// Append-only event log for workflow execution with SSE replay support
// =============================================================================

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace qyl.copilot.Workflows;

/// <summary>
///     Event type for workflow execution events.
/// </summary>
public enum WorkflowEventType
{
    /// <summary>A workflow node started executing.</summary>
    NodeStarted,

    /// <summary>A workflow node completed successfully.</summary>
    NodeCompleted,

    /// <summary>A workflow node failed.</summary>
    NodeFailed,

    /// <summary>A checkpoint was saved.</summary>
    CheckpointSaved,

    /// <summary>Shared state was updated.</summary>
    StateUpdated
}

/// <summary>
///     An event emitted during workflow execution.
/// </summary>
public sealed record WorkflowEvent
{
    /// <summary>Monotonically increasing sequence number.</summary>
    public long Sequence { get; init; }

    /// <summary>The execution this event belongs to.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>The type of event.</summary>
    public required WorkflowEventType Type { get; init; }

    /// <summary>The node ID this event relates to.</summary>
    public string? NodeId { get; init; }

    /// <summary>Optional payload data.</summary>
    public object? Data { get; init; }

    /// <summary>When the event occurred (UTC).</summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
///     Append-only event log for workflow execution.
///     Supports SSE replay by sequence cursor.
/// </summary>
public sealed class WorkflowEventStream
{
    private readonly ConcurrentDictionary<string, List<WorkflowEvent>> _streams = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _appendLock = new();
    private long _globalSequence;

    /// <summary>
    ///     Appends an event to the stream, assigning a sequence number.
    /// </summary>
    public Task AppendAsync(WorkflowEvent workflowEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);
        ct.ThrowIfCancellationRequested();

        using (_appendLock.EnterScope())
        {
            var seq = Interlocked.Increment(ref _globalSequence);
            var stamped = workflowEvent with
            {
                Sequence = seq,
                Timestamp = workflowEvent.Timestamp == default ? TimeProvider.System.GetUtcNow() : workflowEvent.Timestamp
            };

            var list = _streams.GetOrAdd(stamped.ExecutionId, static _ => []);
            list.Add(stamped);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Replays events for an execution starting from a given sequence number.
    /// </summary>
    public async IAsyncEnumerable<WorkflowEvent> ReplayAsync(
        string executionId,
        long fromSequence,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        if (!_streams.TryGetValue(executionId, out var list))
            yield break;

        List<WorkflowEvent> snapshot;
        using (_appendLock.EnterScope())
        {
            snapshot = [.. list];
        }

        foreach (var evt in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            if (evt.Sequence >= fromSequence)
            {
                yield return evt;
                await Task.CompletedTask.ConfigureAwait(false);
            }
        }
    }
}
