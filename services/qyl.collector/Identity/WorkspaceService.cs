namespace Qyl.Collector.Identity;

[QylService(QylLifetime.Singleton)]
public sealed partial class WorkspaceService(DuckDbStore store, ILogger<WorkspaceService> logger)
{
    public async Task<WorkspaceRecord> UpsertWorkspaceAsync(
        WorkspaceRecord workspace,
        CancellationToken ct = default)
    {
        await store.UpsertWorkspaceAsync(workspace, ct).ConfigureAwait(false);
        LogWorkspaceUpserted(workspace.WorkspaceId, workspace.ServiceName);
        return workspace;
    }

    public Task<WorkspaceRecord?> GetWorkspaceAsync(
        string workspaceId,
        CancellationToken ct = default) =>
        store.GetWorkspaceAsync(workspaceId, ct);

    public Task<IReadOnlyList<WorkspaceRecord>> ListWorkspacesAsync(
        int limit = 50,
        CancellationToken ct = default) =>
        store.GetWorkspacesAsync(limit, ct);

    public async Task<bool> HeartbeatAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        if (await store.GetWorkspaceAsync(workspaceId, ct).ConfigureAwait(false) is null)
            return false;

        await store.UpdateHeartbeatAsync(workspaceId, ct).ConfigureAwait(false);
        LogHeartbeat(workspaceId);
        return true;
    }

    public async Task<bool> DeleteWorkspaceAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        var deleted = await store.DeleteWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
        if (deleted)
            LogWorkspaceDeleted(workspaceId);
        return deleted;
    }


    [LoggerMessage(Level = LogLevel.Information,
        Message = "Workspace upserted: {WorkspaceId} (service: {ServiceName})")]
    private partial void LogWorkspaceUpserted(string workspaceId, string? serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Workspace heartbeat: {WorkspaceId}")]
    private partial void LogHeartbeat(string workspaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Workspace deleted: {WorkspaceId}")]
    private partial void LogWorkspaceDeleted(string workspaceId);
}
