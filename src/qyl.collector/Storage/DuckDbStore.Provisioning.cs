using qyl.collector.Provisioning;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with provisioning operations:
///     config selections, generation job CRUD, and job queue queries.
/// </summary>
public sealed partial class DuckDbStore
{
    // ==========================================================================
    // Config Selection Operations
    // ==========================================================================

    /// <summary>
    ///     Upserts a workspace's instrumentation profile selection.
    /// </summary>
    public async Task UpsertConfigSelectionAsync(
        string workspaceId,
        string profileId,
        string? customOverrides,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO config_selections (workspace_id, profile_id, custom_overrides, updated_at)
                              VALUES ($1, $2, $3, now())
                              ON CONFLICT (workspace_id) DO UPDATE SET
                                  profile_id = EXCLUDED.profile_id,
                                  custom_overrides = EXCLUDED.custom_overrides,
                                  updated_at = now()
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });
            cmd.Parameters.Add(new DuckDBParameter { Value = profileId });
            cmd.Parameters.Add(new DuckDBParameter { Value = customOverrides ?? (object)DBNull.Value });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the current profile selection for a workspace.
    /// </summary>
    public async Task<ConfigSelectionRecord?> GetConfigSelectionAsync(
        string workspaceId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT workspace_id, profile_id, custom_overrides, updated_at
                          FROM config_selections
                          WHERE workspace_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return MapConfigSelection(reader);

        return null;
    }

    // ==========================================================================
    // Generation Job Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts a new generation job record.
    /// </summary>
    public async Task InsertGenerationJobAsync(
        GenerationJobRecord job,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var writeJob = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO generation_jobs
                                  (job_id, workspace_id, profile_id, status, output_url, error_message, created_at, completed_at)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = job.JobId });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.WorkspaceId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.ProfileId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.OutputUrl ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.ErrorMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.CreatedAt });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.CompletedAt ?? (object)DBNull.Value });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(writeJob, ct).ConfigureAwait(false);
        await writeJob.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a generation job by its ID.
    /// </summary>
    public async Task<GenerationJobRecord?> GetGenerationJobAsync(
        string jobId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT job_id, workspace_id, profile_id, status,
                                 output_url, error_message, created_at, completed_at
                          FROM generation_jobs
                          WHERE job_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = jobId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return MapGenerationJob(reader);

        return null;
    }

    /// <summary>
    ///     Updates a generation job (status, output, error, completion time).
    /// </summary>
    public async Task UpdateGenerationJobAsync(
        GenerationJobRecord job,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var writeJob = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE generation_jobs SET
                                  status = $1,
                                  output_url = $2,
                                  error_message = $3,
                                  completed_at = $4
                              WHERE job_id = $5
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = job.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.OutputUrl ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.ErrorMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.CompletedAt ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.JobId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(writeJob, ct).ConfigureAwait(false);
        await writeJob.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists generation jobs for a workspace, ordered by creation time descending.
    /// </summary>
    public async Task<IReadOnlyList<GenerationJobRecord>> GetGenerationJobsByWorkspaceAsync(
        string workspaceId,
        int limit = 50,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var clampedLimit = Math.Clamp(limit, 1, 1000);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT job_id, workspace_id, profile_id, status,
                                  output_url, error_message, created_at, completed_at
                           FROM generation_jobs
                           WHERE workspace_id = $1
                           ORDER BY created_at DESC
                           LIMIT {clampedLimit}
                           """;
        cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });

        var jobs = new List<GenerationJobRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            jobs.Add(MapGenerationJob(reader));

        return jobs;
    }

    // ==========================================================================
    // Private Methods - Provisioning Mapping
    // ==========================================================================

    private static ConfigSelectionRecord MapConfigSelection(IDataReader reader)
    {
        var fallback = TimeProvider.System.GetUtcNow().UtcDateTime;
        return new ConfigSelectionRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.Col(2).AsString,
            reader.Col(3).AsDateTime ?? fallback);
    }

    private static GenerationJobRecord MapGenerationJob(IDataReader reader)
    {
        var fallback = TimeProvider.System.GetUtcNow().UtcDateTime;
        return new GenerationJobRecord(
            reader.GetString(0),
            reader.Col(1).AsString,
            reader.Col(2).AsString,
            reader.GetString(3),
            reader.Col(4).AsString,
            reader.Col(5).AsString,
            reader.Col(6).AsDateTime ?? fallback,
            reader.Col(7).AsDateTime);
    }
}
