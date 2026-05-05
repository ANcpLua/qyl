namespace Qyl.Collector.Identity;

[QylService(QylLifetime.Singleton)]
public sealed partial class ProjectService(DuckDbStore store, ILogger<ProjectService> logger)
{
    public async Task<ProjectRecord> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken ct = default)
    {
        var projectId = $"proj-{Guid.CreateVersion7():N}"[..28];
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var project = new ProjectRecord(
            projectId,
            request.WorkspaceId,
            request.Name,
            request.Description,
            now,
            now);

        await store.InsertProjectAsync(project, ct).ConfigureAwait(false);
        LogProjectCreated(projectId, request.Name);
        return project;
    }

    public Task<ProjectRecord?> GetProjectAsync(
        string projectId,
        CancellationToken ct = default) =>
        store.GetProjectAsync(projectId, ct);

    public Task<IReadOnlyList<ProjectRecord>> ListProjectsAsync(
        string workspaceId,
        int limit = 50,
        string? cursor = null,
        CancellationToken ct = default) =>
        store.GetProjectsAsync(workspaceId, limit, cursor, ct);

    public async Task<bool> DeleteProjectAsync(
        string projectId,
        CancellationToken ct = default)
    {
        var deleted = await store.DeleteProjectAsync(projectId, ct).ConfigureAwait(false);
        if (deleted)
            LogProjectDeleted(projectId);
        return deleted;
    }


    public async Task<ProjectEnvironmentRecord> AddEnvironmentAsync(
        string projectId,
        string name,
        string? description = null,
        CancellationToken ct = default)
    {
        var envId = $"env-{Guid.CreateVersion7():N}"[..24];
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var env = new ProjectEnvironmentRecord(
            envId,
            projectId,
            name,
            description,
            now);

        await store.InsertProjectEnvironmentAsync(env, ct).ConfigureAwait(false);
        LogEnvironmentAdded(envId, projectId, name);
        return env;
    }

    public Task<IReadOnlyList<ProjectEnvironmentRecord>> ListEnvironmentsAsync(
        string projectId,
        CancellationToken ct = default) =>
        store.GetProjectEnvironmentsAsync(projectId, ct);

    public async Task<bool> DeleteEnvironmentAsync(
        string environmentId,
        CancellationToken ct = default)
    {
        var deleted = await store.DeleteProjectEnvironmentAsync(environmentId, ct).ConfigureAwait(false);
        if (deleted)
            LogEnvironmentDeleted(environmentId);
        return deleted;
    }


    [LoggerMessage(Level = LogLevel.Information,
        Message = "Project created: {ProjectId} ({Name})")]
    private partial void LogProjectCreated(string projectId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project deleted: {ProjectId}")]
    private partial void LogProjectDeleted(string projectId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Environment added: {EnvironmentId} to project {ProjectId} ({Name})")]
    private partial void LogEnvironmentAdded(string environmentId, string projectId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Environment deleted: {EnvironmentId}")]
    private partial void LogEnvironmentDeleted(string environmentId);
}


public sealed record CreateProjectRequest(
    string WorkspaceId,
    string Name,
    string? Description = null);

public sealed record ProjectRecord(
    string ProjectId,
    string WorkspaceId,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ProjectEnvironmentRecord(
    string EnvironmentId,
    string ProjectId,
    string Name,
    string? Description,
    DateTime CreatedAt);
