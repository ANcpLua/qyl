using Qyl.Collector.Identity;

namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{

    public async Task UpsertWorkspaceAsync(WorkspaceRecord workspace, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO workspaces
                                  (workspace_id, name, service_name, sdk_version, runtime_version,
                                   framework, git_commit, status, metadata_json)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                              ON CONFLICT (workspace_id) DO UPDATE SET
                                  name = EXCLUDED.name,
                                  service_name = COALESCE(EXCLUDED.service_name, service_name),
                                  sdk_version = EXCLUDED.sdk_version,
                                  runtime_version = EXCLUDED.runtime_version,
                                  framework = EXCLUDED.framework,
                                  git_commit = EXCLUDED.git_commit,
                                  status = EXCLUDED.status,
                                  last_heartbeat = now(),
                                  metadata_json = EXCLUDED.metadata_json
                              """;
            AddWorkspaceParameters(cmd, workspace);
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public Task<WorkspaceRecord?> GetWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<WorkspaceRecord?>(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT workspace_id, name, service_name, sdk_version, runtime_version,
                                     framework, git_commit, status, first_seen, last_heartbeat, metadata_json
                              FROM workspaces
                              WHERE workspace_id = $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                return MapWorkspace(reader);

            return null;
        }, ct);
    }

    public async Task UpdateHeartbeatAsync(string workspaceId, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE workspaces SET last_heartbeat = now()
                              WHERE workspace_id = $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public Task<IReadOnlyList<WorkspaceRecord>> GetWorkspacesAsync(
        int limit = 50,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<WorkspaceRecord>>(con =>
        {
            var clampedLimit = Math.Clamp(limit, 1, 1000);

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT workspace_id, name, service_name, sdk_version, runtime_version,"
                              + " framework, git_commit, status, first_seen, last_heartbeat, metadata_json"
                              + " FROM workspaces ORDER BY last_heartbeat DESC LIMIT "
                              + clampedLimit.ToString(CultureInfo.InvariantCulture);

            var workspaces = new List<WorkspaceRecord>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                workspaces.Add(MapWorkspace(reader));

            return workspaces;
        }, ct);
    }

    public async Task<bool> DeleteWorkspaceAsync(string workspaceId, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM workspaces WHERE workspace_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
        }, ct).ConfigureAwait(false);


    public async Task InsertProjectAsync(ProjectRecord project, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public Task<ProjectRecord?> GetProjectAsync(string projectId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<ProjectRecord?>(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT project_id, workspace_id, name, description, created_at, updated_at
                              FROM projects
                              WHERE project_id = $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                return MapProject(reader);

            return null;
        }, ct);
    }

    public Task<IReadOnlyList<ProjectRecord>> GetProjectsAsync(
        string workspaceId,
        int limit = 50,
        string? cursor = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<ProjectRecord>>(con =>
        {
            var clampedLimit = Math.Clamp(limit, 1, 1000);
            var qb = new QueryBuilder();
            qb.Add("workspace_id = $N", workspaceId);

            if (!string.IsNullOrEmpty(cursor))
                qb.Add("project_id > $N", cursor);

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT project_id, workspace_id, name, description, created_at, updated_at"
                              + " FROM projects " + qb.WhereClause
                              + " ORDER BY project_id ASC LIMIT "
                              + qb.NextParam.ToString(CultureInfo.InvariantCulture);

            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = clampedLimit });

            var projects = new List<ProjectRecord>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                projects.Add(MapProject(reader));

            return projects;
        }, ct);
    }

    public async Task<bool> DeleteProjectAsync(string projectId, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM projects WHERE project_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
        }, ct).ConfigureAwait(false);


    public async Task InsertProjectEnvironmentAsync(
        ProjectEnvironmentRecord env,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public Task<IReadOnlyList<ProjectEnvironmentRecord>> GetProjectEnvironmentsAsync(
        string projectId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<ProjectEnvironmentRecord>>(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT environment_id, project_id, name, description, created_at
                              FROM project_environments
                              WHERE project_id = $1
                              ORDER BY created_at ASC
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });

            var envs = new List<ProjectEnvironmentRecord>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                envs.Add(MapProjectEnvironment(reader));

            return envs;
        }, ct);
    }

    public async Task<bool> DeleteProjectEnvironmentAsync(
        string environmentId,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM project_environments WHERE environment_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = environmentId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
        }, ct).ConfigureAwait(false);


    public async Task UpsertHandshakeChallengeAsync(
        string workspaceId,
        string codeChallenge,
        DateTime createdAt,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public Task<HandshakeChallengeRecord?> GetHandshakeChallengeAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<HandshakeChallengeRecord?>(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT workspace_id, code_challenge, created_at
                              FROM handshake_challenges
                              WHERE workspace_id = $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new HandshakeChallengeRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetDateTime(2));
            }

            return null;
        }, ct);
    }

    public async Task DeleteHandshakeChallengeAsync(
        string workspaceId,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM handshake_challenges WHERE workspace_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);


    public async Task UpsertGitHubTokenAsync(
        string token,
        string? scope,
        string? githubLogin,
        string authMethod = "pat",
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, cancellation) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO github_tokens (key, token, scope, github_login, auth_method, created_at)
                              VALUES ($1, $2, $3, $4, $5, now())
                              ON CONFLICT (key) DO UPDATE SET
                                  token = EXCLUDED.token,
                                  scope = EXCLUDED.scope,
                                  github_login = EXCLUDED.github_login,
                                  auth_method = EXCLUDED.auth_method,
                                  created_at = now()
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = "default" });
            cmd.Parameters.Add(new DuckDBParameter { Value = token });
            cmd.Parameters.Add(new DuckDBParameter { Value = scope ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = githubLogin ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = authMethod });
            await cmd.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public Task<GitHubTokenRecord?> GetGitHubTokenAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<GitHubTokenRecord?>(static con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT token, scope, github_login, auth_method, created_at
                              FROM github_tokens
                              WHERE key = 'default'
                              """;

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new GitHubTokenRecord(
                    reader.GetString(0),
                    reader.Col(1).AsString,
                    reader.Col(2).AsString,
                    reader.Col(3).AsString ?? "pat",
                    reader.Col(4).AsDateTime ?? TimeProvider.System.GetUtcNow().UtcDateTime);
            }

            return null;
        }, ct);
    }

    public async Task<bool> DeleteGitHubTokenAsync(CancellationToken ct = default) =>
        await ExecuteWriteAsync(static async (con, cancellation) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM github_tokens WHERE key = 'default'";
            return await cmd.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false) > 0;
        }, ct).ConfigureAwait(false);


    private static void AddWorkspaceParameters(DuckDBCommand cmd, WorkspaceRecord workspace)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.WorkspaceId });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.Name ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.ServiceName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.SdkVersion ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.RuntimeVersion ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.Framework ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.GitCommit ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.Status });
        cmd.Parameters.Add(new DuckDBParameter { Value = workspace.MetadataJson ?? (object)DBNull.Value });
    }

    private static WorkspaceRecord MapWorkspace(DbDataReader reader)
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
    private static ProjectRecord MapProject(DbDataReader reader)
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
    private static ProjectEnvironmentRecord MapProjectEnvironment(DbDataReader reader)
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


public sealed record GitHubTokenRecord(
    string Token,
    string? Scope,
    string? GitHubLogin,
    string AuthMethod,
    DateTime CreatedAt);

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
