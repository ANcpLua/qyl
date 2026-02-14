using qyl.collector.Storage;

namespace qyl.collector.Identity;

/// <summary>
///     CRUD operations for workspace envelope entities with heartbeat lifecycle.
///     Singleton service — all writes go through the DuckDbStore channel-buffered path.
/// </summary>
public sealed partial class WorkspaceService(DuckDbStore store, ILogger<WorkspaceService> logger)
{
    /// <summary>
    ///     Creates or updates a workspace envelope. On conflict, refreshes mutable fields
    ///     and bumps the last_heartbeat timestamp.
    /// </summary>
    public async Task<WorkspaceRecord> UpsertWorkspaceAsync(
        WorkspaceRecord workspace,
        CancellationToken ct = default)
    {
        await store.UpsertWorkspaceAsync(workspace, ct).ConfigureAwait(false);
        LogWorkspaceUpserted(workspace.WorkspaceId, workspace.ServiceName);
        return workspace;
    }

    /// <summary>
    ///     Retrieves a workspace by its unique identifier. Returns null when not found.
    /// </summary>
    public Task<WorkspaceRecord?> GetWorkspaceAsync(
        string workspaceId,
        CancellationToken ct = default) =>
        store.GetWorkspaceAsync(workspaceId, ct);

    /// <summary>
    ///     Lists all workspaces ordered by most recent heartbeat.
    /// </summary>
    public Task<IReadOnlyList<WorkspaceRecord>> ListWorkspacesAsync(
        int limit = 50,
        CancellationToken ct = default) =>
        store.GetWorkspacesAsync(limit, ct);

    /// <summary>
    ///     Touches the last_heartbeat column for the specified workspace.
    ///     Returns false if the workspace does not exist.
    /// </summary>
    public async Task<bool> HeartbeatAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        var existing = await store.GetWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
        if (existing is null)
            return false;

        await store.UpdateHeartbeatAsync(workspaceId, ct).ConfigureAwait(false);
        LogHeartbeat(workspaceId);
        return true;
    }

    /// <summary>
    ///     Deletes a workspace by ID. Returns false if the workspace was not found.
    /// </summary>
    public async Task<bool> DeleteWorkspaceAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        var deleted = await store.DeleteWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
        if (deleted)
            LogWorkspaceDeleted(workspaceId);
        return deleted;
    }

    // ==========================================================================
    // LoggerMessage -- structured, zero-allocation logging
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Workspace upserted: {WorkspaceId} (service: {ServiceName})")]
    private partial void LogWorkspaceUpserted(string workspaceId, string? serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Workspace heartbeat: {WorkspaceId}")]
    private partial void LogHeartbeat(string workspaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Workspace deleted: {WorkspaceId}")]
    private partial void LogWorkspaceDeleted(string workspaceId);
}
