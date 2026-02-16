namespace qyl.collector.Identity;

/// <summary>
///     CRUD for projects and project environments with cursor-based pagination.
///     Singleton service backed by DuckDbStore read/write paths.
/// </summary>
public sealed partial class ProjectService(DuckDbStore store, ILogger<ProjectService> logger)
{
    /// <summary>
    ///     Creates a new project and returns the persisted record.
    /// </summary>
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

    /// <summary>
    ///     Gets a single project by its identifier. Returns null when not found.
    /// </summary>
    public Task<ProjectRecord?> GetProjectAsync(
        string projectId,
        CancellationToken ct = default) =>
        store.GetProjectAsync(projectId, ct);

    /// <summary>
    ///     Lists projects for a workspace with cursor-based pagination.
    ///     When <paramref name="cursor" /> is provided, returns projects created after that cursor.
    /// </summary>
    public Task<IReadOnlyList<ProjectRecord>> ListProjectsAsync(
        string workspaceId,
        int limit = 50,
        string? cursor = null,
        CancellationToken ct = default) =>
        store.GetProjectsAsync(workspaceId, limit, cursor, ct);

    /// <summary>
    ///     Deletes a project by ID. Returns false if the project was not found.
    /// </summary>
    public async Task<bool> DeleteProjectAsync(
        string projectId,
        CancellationToken ct = default)
    {
        var deleted = await store.DeleteProjectAsync(projectId, ct).ConfigureAwait(false);
        if (deleted)
            LogProjectDeleted(projectId);
        return deleted;
    }

    // ==========================================================================
    // Project Environment Operations
    // ==========================================================================

    /// <summary>
    ///     Adds an environment to a project (e.g. production, staging, development).
    /// </summary>
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

    /// <summary>
    ///     Lists all environments for a project.
    /// </summary>
    public Task<IReadOnlyList<ProjectEnvironmentRecord>> ListEnvironmentsAsync(
        string projectId,
        CancellationToken ct = default) =>
        store.GetProjectEnvironmentsAsync(projectId, ct);

    /// <summary>
    ///     Removes an environment by ID. Returns false if not found.
    /// </summary>
    public async Task<bool> DeleteEnvironmentAsync(
        string environmentId,
        CancellationToken ct = default)
    {
        var deleted = await store.DeleteProjectEnvironmentAsync(environmentId, ct).ConfigureAwait(false);
        if (deleted)
            LogEnvironmentDeleted(environmentId);
        return deleted;
    }

    // ==========================================================================
    // LoggerMessage -- structured, zero-allocation logging
    // ==========================================================================

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

// =============================================================================
// Project Records
// =============================================================================

/// <summary>
///     Request to create a new project.
/// </summary>
public sealed record CreateProjectRequest(
    string WorkspaceId,
    string Name,
    string? Description = null);

/// <summary>
///     Storage record for a project. Maps to the projects DuckDB table.
/// </summary>
public sealed record ProjectRecord(
    string ProjectId,
    string WorkspaceId,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
///     Storage record for a project environment. Maps to the project_environments DuckDB table.
/// </summary>
public sealed record ProjectEnvironmentRecord(
    string EnvironmentId,
    string ProjectId,
    string Name,
    string? Description,
    DateTime CreatedAt);
