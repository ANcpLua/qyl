using qyl.collector.Identity;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with workspace identity operations,
///     project CRUD, project environments, and handshake PKCE challenge persistence.
/// </summary>
public sealed partial class DuckDbStore
{
    // ==========================================================================
    // Workspace Operations
    // ==========================================================================

    /// <summary>
    ///     Upserts a workspace record via the channel-buffered write path.
    ///     On conflict, updates all mutable fields and refreshes last_heartbeat.
    /// </summary>
    public async Task UpsertWorkspaceAsync(WorkspaceRecord workspace, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO workspaces
                                  (workspace_id, name, service_name, sdk_version, runtime_version,
                                   framework, git_commit, status, metadata_json)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                              ON CONFLICT (workspace_id) DO UPDATE SET
                                  name = EXCLUDED.name,
                                  service_name = EXCLUDED.service_name,
                                  sdk_version = EXCLUDED.sdk_version,
                                  runtime_version = EXCLUDED.runtime_version,
                                  framework = EXCLUDED.framework,
                                  git_commit = EXCLUDED.git_commit,
                                  status = EXCLUDED.status,
                                  last_heartbeat = now(),
                                  metadata_json = EXCLUDED.metadata_json
                              """;
            AddWorkspaceParameters(cmd, workspace);
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a single workspace by its workspace ID.
    /// </summary>
    public async Task<WorkspaceRecord?> GetWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT workspace_id, name, service_name, sdk_version, runtime_version,
                                 framework, git_commit, status, first_seen, last_heartbeat, metadata_json
                          FROM workspaces
                          WHERE workspace_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return MapWorkspace(reader);

        return null;
    }

    /// <summary>
    ///     Updates the last_heartbeat timestamp for a workspace.
    /// </summary>
    public async Task UpdateHeartbeatAsync(string workspaceId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE workspaces SET last_heartbeat = now()
                              WHERE workspace_id = $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists workspaces ordered by last heartbeat, most recent first.
    /// </summary>
    public async Task<IReadOnlyList<WorkspaceRecord>> GetWorkspacesAsync(
        int limit = 50,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var clampedLimit = Math.Clamp(limit, 1, 1000);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT workspace_id, name, service_name, sdk_version, runtime_version,
                                  framework, git_commit, status, first_seen, last_heartbeat, metadata_json
                           FROM workspaces
                           ORDER BY last_heartbeat DESC
                           LIMIT {clampedLimit}
                           """;

        var workspaces = new List<WorkspaceRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            workspaces.Add(MapWorkspace(reader));

        return workspaces;
    }

    /// <summary>
    ///     Deletes a workspace by ID. Returns true if a row was deleted.
    /// </summary>
    public async Task<bool> DeleteWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM workspaces WHERE workspace_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false) > 0;
    }

    // ==========================================================================
    // Project Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts a new project record.
    /// </summary>
    public async Task InsertProjectAsync(ProjectRecord project, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO projects
                                  (project_id, workspace_id, name, description, created_at, updated_at)
                              VALUES ($1, $2, $3, $4, $5, $6)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = project.ProjectId });
            cmd.Parameters.Add(new DuckDBParameter { Value = project.WorkspaceId });
            cmd.Parameters.Add(new DuckDBParameter { Value = project.Name });
            cmd.Parameters.Add(new DuckDBParameter { Value = project.Description ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = project.CreatedAt });
            cmd.Parameters.Add(new DuckDBParameter { Value = project.UpdatedAt });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a single project by its ID.
    /// </summary>
    public async Task<ProjectRecord?> GetProjectAsync(string projectId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT project_id, workspace_id, name, description, created_at, updated_at
                          FROM projects
                          WHERE project_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = projectId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return MapProject(reader);

        return null;
    }

    /// <summary>
    ///     Lists projects for a workspace with cursor-based pagination.
    ///     When <paramref name="cursor" /> is provided, returns projects created after that ID.
    /// </summary>
    public async Task<IReadOnlyList<ProjectRecord>> GetProjectsAsync(
        string workspaceId,
        int limit = 50,
        string? cursor = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var clampedLimit = Math.Clamp(limit, 1, 1000);
        var qb = new QueryBuilder();
        qb.Add("workspace_id = $N", workspaceId);

        if (!string.IsNullOrEmpty(cursor))
            qb.Add("project_id > $N", cursor);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT project_id, workspace_id, name, description, created_at, updated_at
                           FROM projects
                           {qb.WhereClause}
                           ORDER BY project_id ASC
                           LIMIT {qb.NextParam}
                           """;

        qb.ApplyTo(cmd);
        cmd.Parameters.Add(new DuckDBParameter { Value = clampedLimit });

        var projects = new List<ProjectRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            projects.Add(MapProject(reader));

        return projects;
    }

    /// <summary>
    ///     Deletes a project by ID. Returns true if a row was deleted.
    /// </summary>
    public async Task<bool> DeleteProjectAsync(string projectId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM projects WHERE project_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false) > 0;
    }

    // ==========================================================================
    // Project Environment Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts a new project environment record.
    /// </summary>
    public async Task InsertProjectEnvironmentAsync(
        ProjectEnvironmentRecord env,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO project_environments
                                  (environment_id, project_id, name, description, created_at)
                              VALUES ($1, $2, $3, $4, $5)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = env.EnvironmentId });
            cmd.Parameters.Add(new DuckDBParameter { Value = env.ProjectId });
            cmd.Parameters.Add(new DuckDBParameter { Value = env.Name });
            cmd.Parameters.Add(new DuckDBParameter { Value = env.Description ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = env.CreatedAt });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all environments for a project.
    /// </summary>
    public async Task<IReadOnlyList<ProjectEnvironmentRecord>> GetProjectEnvironmentsAsync(
        string projectId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT environment_id, project_id, name, description, created_at
                          FROM project_environments
                          WHERE project_id = $1
                          ORDER BY created_at ASC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = projectId });

        var envs = new List<ProjectEnvironmentRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            envs.Add(MapProjectEnvironment(reader));

        return envs;
    }

    /// <summary>
    ///     Deletes a project environment by ID. Returns true if a row was deleted.
    /// </summary>
    public async Task<bool> DeleteProjectEnvironmentAsync(
        string environmentId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM project_environments WHERE environment_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = environmentId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false) > 0;
    }

    // ==========================================================================
    // Handshake PKCE Challenge Operations
    // ==========================================================================

    /// <summary>
    ///     Stores a PKCE challenge for a pending handshake session.
    /// </summary>
    public async Task UpsertHandshakeChallengeAsync(
        string workspaceId,
        string codeChallenge,
        DateTime createdAt,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO handshake_challenges (workspace_id, code_challenge, created_at)
                              VALUES ($1, $2, $3)
                              ON CONFLICT (workspace_id) DO UPDATE SET
                                  code_challenge = EXCLUDED.code_challenge,
                                  created_at = EXCLUDED.created_at
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });
            cmd.Parameters.Add(new DuckDBParameter { Value = codeChallenge });
            cmd.Parameters.Add(new DuckDBParameter { Value = createdAt });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Retrieves the stored PKCE challenge for a workspace.
    /// </summary>
    public async Task<HandshakeChallengeRecord?> GetHandshakeChallengeAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT workspace_id, code_challenge, created_at
                          FROM handshake_challenges
                          WHERE workspace_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new HandshakeChallengeRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDateTime(2));
        }

        return null;
    }

    /// <summary>
    ///     Deletes a PKCE challenge after handshake verification.
    /// </summary>
    public async Task DeleteHandshakeChallengeAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM handshake_challenges WHERE workspace_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    // ==========================================================================
    // Private Methods - Identity Mapping
    // ==========================================================================

    private static void AddWorkspaceParameters(DuckDBCommand cmd, WorkspaceRecord workspace)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.WorkspaceId });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.Name ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.ServiceName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.SdkVersion ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.RuntimeVersion ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.Framework ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.GitCommit ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.Status ?? "pending" });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.MetadataJson ?? (object)DBNull.Value });
    }

    private static WorkspaceRecord MapWorkspace(IDataReader reader)
    {
        var fallback = TimeProvider.System.GetUtcNow().UtcDateTime;
        return new WorkspaceRecord(
            reader.GetString(0),
            reader.Col(1).AsString,
            reader.Col(2).AsString,
            reader.Col(3).AsString,
            reader.Col(4).AsString,
            reader.Col(5).AsString,
            reader.Col(6).AsString,
            reader.Col(7).AsString ?? "pending",
            reader.Col(8).AsDateTime ?? fallback,
            reader.Col(9).AsDateTime ?? fallback,
            reader.Col(10).AsString
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ProjectRecord MapProject(IDataReader reader)
    {
        var fallback = TimeProvider.System.GetUtcNow().UtcDateTime;
        return new ProjectRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.Col(3).AsString,
            reader.Col(4).AsDateTime ?? fallback,
            reader.Col(5).AsDateTime ?? fallback);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ProjectEnvironmentRecord MapProjectEnvironment(IDataReader reader)
    {
        var fallback = TimeProvider.System.GetUtcNow().UtcDateTime;
        return new ProjectEnvironmentRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.Col(3).AsString,
            reader.Col(4).AsDateTime ?? fallback);
    }
}

// =============================================================================
// Workspace Identity Records
// =============================================================================

/// <summary>
///     Storage record for a workspace. Maps to the workspaces DuckDB table.
/// </summary>
public sealed record WorkspaceRecord(
    string WorkspaceId,
    string? Name,
    string? ServiceName,
    string? SdkVersion,
    string? RuntimeVersion,
    string? Framework,
    string? GitCommit,
    string Status,
    DateTime FirstSeen,
    DateTime LastHeartbeat,
    string? MetadataJson = null);
