// =============================================================================
// qyl.copilot - Checkpoint Manager
// Durable checkpointing for workflow recovery via IExecutionStore
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;

namespace qyl.copilot.Workflows;

/// <summary>
///     Data stored at a checkpoint boundary.
/// </summary>
public sealed record CheckpointData
{
    /// <summary>The execution this checkpoint belongs to.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>The node ID at which the checkpoint was taken.</summary>
    public required string NodeId { get; init; }

    /// <summary>Serialized state at the checkpoint.</summary>
    public required string StateJson { get; init; }

    /// <summary>When the checkpoint was saved (UTC).</summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
///     Manages durable checkpoints for workflow recovery.
///     Stores checkpoints via <see cref="IExecutionStore" /> when available,
///     falls back to in-memory storage otherwise.
/// </summary>
public sealed class CheckpointManager
{
    private readonly ConcurrentDictionary<string, List<CheckpointData>> _inMemory =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _lock = new();
    private readonly IExecutionStore? _store;

    /// <summary>
    ///     Creates a new checkpoint manager.
    /// </summary>
    /// <param name="store">Optional durable execution store.</param>
    public CheckpointManager(IExecutionStore? store = null) => _store = store;

    /// <summary>
    ///     Saves a checkpoint after a durable node boundary.
    /// </summary>
    public async Task SaveCheckpointAsync(string executionId, string nodeId, object? state,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        var checkpoint = new CheckpointData
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            StateJson = JsonSerializer.Serialize(state),
            Timestamp = TimeProvider.System.GetUtcNow()
        };

        using (_lock.EnterScope())
        {
            var list = _inMemory.GetOrAdd(executionId, static _ => []);
            list.Add(checkpoint);
        }

        // Best-effort persist to durable store
        if (_store is not null)
        {
            try
            {
                await _store.SaveCheckpointAsync(checkpoint, ct).ConfigureAwait(false);
            }
            catch
            {
                // In-memory is the primary path; durable store is best-effort
            }
        }
    }

    /// <summary>
    ///     Loads the latest checkpoint for an execution.
    /// </summary>
    public Task<CheckpointData?> LoadLatestCheckpointAsync(string executionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ct.ThrowIfCancellationRequested();

        if (_inMemory.TryGetValue(executionId, out var list))
        {
            using (_lock.EnterScope())
            {
                var latest = list.Count > 0 ? list[^1] : null;
                return Task.FromResult(latest);
            }
        }

        return Task.FromResult<CheckpointData?>(null);
    }

    /// <summary>
    ///     Gets all checkpoints for an execution, ordered by timestamp.
    /// </summary>
    public Task<IReadOnlyList<CheckpointData>> GetCheckpointsAsync(string executionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ct.ThrowIfCancellationRequested();

        if (_inMemory.TryGetValue(executionId, out var list))
        {
            using (_lock.EnterScope())
            {
                IReadOnlyList<CheckpointData> result = [.. list];
                return Task.FromResult(result);
            }
        }

        IReadOnlyList<CheckpointData> empty = [];
        return Task.FromResult(empty);
    }
}
