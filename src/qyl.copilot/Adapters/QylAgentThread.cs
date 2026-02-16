// =============================================================================
// qyl.copilot - Agent Thread
// Thread abstraction for run-thread continuity across workflow checkpoints
// Links AgentSession to workflow execution for state rehydration
// =============================================================================

using System.Text.Json;
using Microsoft.Agents.AI;
using qyl.copilot.Workflows;

namespace qyl.copilot.Adapters;

/// <summary>
///     Agent thread that persists across workflow checkpoints.
///     Links an <see cref="AgentSession" /> to a workflow execution,
///     enabling thread state to be serialized into checkpoints and rehydrated
///     when a workflow resumes.
/// </summary>
public sealed class QylAgentThread : IDisposable
{
    private readonly Lock _lock = new();
    private readonly QylChatMessageStore _messageStore;
    private bool _disposed;
    private AgentSession? _session;

    /// <summary>
    ///     Creates a new agent thread linked to a workflow execution.
    /// </summary>
    /// <param name="threadId">Unique thread identifier (typically matches execution ID).</param>
    /// <param name="agentName">Name of the agent this thread is for.</param>
    /// <param name="messageStore">Backing message store for persistence.</param>
    public QylAgentThread(
        string threadId,
        string agentName,
        QylChatMessageStore messageStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        ThreadId = threadId;
        AgentName = agentName;
        _messageStore = Guard.NotNull(messageStore);
    }

    /// <summary>Unique thread identifier.</summary>
    public string ThreadId { get; }

    /// <summary>Agent name this thread is associated with.</summary>
    public string AgentName { get; }

    /// <summary>When this thread was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = TimeProvider.System.GetUtcNow();

    /// <summary>
    ///     Gets the underlying <see cref="AgentSession" /> for use with agent invocations.
    ///     Session is created lazily via <see cref="GetOrCreateSessionAsync" />.
    /// </summary>
    public AgentSession? Session
    {
        get
        {
            ThrowIfDisposed();
            return _session;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _disposed = true;

    /// <summary>
    ///     Gets the current session or creates one from the specified agent.
    /// </summary>
    /// <param name="agent">The agent to create a session from if none exists.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent session.</returns>
    public async ValueTask<AgentSession> GetOrCreateSessionAsync(AIAgent agent, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(agent);

        if (_session is not null)
            return _session;

        _session = await agent.CreateSessionAsync(ct).ConfigureAwait(false);
        return _session;
    }

    /// <summary>
    ///     Serializes thread state for checkpoint persistence.
    ///     Captures both the session state and message history.
    /// </summary>
    /// <returns>Serialized thread state.</returns>
    public ThreadCheckpoint CreateCheckpoint()
    {
        ThrowIfDisposed();

        using (_lock.EnterScope())
        {
            return new ThreadCheckpoint
            {
                ThreadId = ThreadId,
                AgentName = AgentName,
                CreatedAt = CreatedAt,
                MessagesJson = _messageStore.SerializeThread(ThreadId),
                Timestamp = TimeProvider.System.GetUtcNow()
            };
        }
    }

    /// <summary>
    ///     Restores thread state from a checkpoint.
    ///     Rehydrates the session and message history.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to restore from.</param>
    public void RestoreFromCheckpoint(ThreadCheckpoint checkpoint)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(checkpoint);

        using (_lock.EnterScope())
        {
            // Restore message history
            if (!string.IsNullOrEmpty(checkpoint.MessagesJson))
            {
                _messageStore.DeserializeThread(ThreadId, checkpoint.MessagesJson);
            }

            // Reset session for continued use (will be recreated from agent)
            _session = null;
        }
    }

    /// <summary>
    ///     Saves thread state into a <see cref="CheckpointManager" />.
    /// </summary>
    /// <param name="checkpointManager">The checkpoint manager to save to.</param>
    /// <param name="executionId">The workflow execution ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveToCheckpointAsync(
        CheckpointManager checkpointManager,
        string executionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(checkpointManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        var checkpoint = CreateCheckpoint();
        var stateJson = JsonSerializer.Serialize(checkpoint);

        await checkpointManager.SaveCheckpointAsync(
            executionId,
            $"thread:{ThreadId}",
            stateJson,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Restores thread state from a <see cref="CheckpointManager" />.
    /// </summary>
    /// <param name="checkpointManager">The checkpoint manager to load from.</param>
    /// <param name="executionId">The workflow execution ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if a checkpoint was found and restored.</returns>
    public async Task<bool> RestoreFromCheckpointAsync(
        CheckpointManager checkpointManager,
        string executionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(checkpointManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        var checkpointData = await checkpointManager.LoadLatestCheckpointAsync(executionId, ct)
            .ConfigureAwait(false);

        if (checkpointData is null)
            return false;

        var checkpoint = JsonSerializer.Deserialize<ThreadCheckpoint>(checkpointData.StateJson);
        if (checkpoint is null)
            return false;

        RestoreFromCheckpoint(checkpoint);
        return true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
///     Serializable thread state for checkpoint persistence.
/// </summary>
public sealed record ThreadCheckpoint
{
    /// <summary>Thread identifier.</summary>
    public required string ThreadId { get; init; }

    /// <summary>Agent name.</summary>
    public required string AgentName { get; init; }

    /// <summary>When the thread was originally created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Serialized message history JSON.</summary>
    public string? MessagesJson { get; init; }

    /// <summary>When this checkpoint was created.</summary>
    public DateTimeOffset Timestamp { get; init; }
}
