using Qyl.Collector.Provisioning;

namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{

    public async Task UpsertConfigSelectionAsync(
        string workspaceId,
        string profileId,
        string? customOverrides,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

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


    // The generation_jobs table (see DuckDbSchema.g.cs:106) names the primary
    // key `id`, the timestamp `queued_at`, and the output column `output_path`.
    // The C# GenerationJobRecord shape (JobId / CreatedAt / OutputUrl) predates
    // that schema; the SQL below maps between them so the endpoint contract
    // can stay stable. `job_type` / `priority` are required columns on the
    // table but are not part of the current HTTP request body — defaulted to
    // "full" / 0 until ProvisioningEndpoints surfaces them.
    public async Task InsertGenerationJobAsync(
        GenerationJobRecord job,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO generation_jobs
                                  (id, workspace_id, profile_id, job_type, status, priority,
                                   output_path, error_message, queued_at, completed_at)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = job.JobId });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.WorkspaceId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.ProfileId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = "full" });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = 0 });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.OutputUrl ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.ErrorMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.CreatedAt });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.CompletedAt ?? (object)DBNull.Value });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task<GenerationJobRecord?> GetGenerationJobAsync(
        string jobId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, workspace_id, profile_id, status,
                                 output_path, error_message, queued_at, completed_at
                          FROM generation_jobs
                          WHERE id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = jobId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return MapGenerationJob(reader);

        return null;
    }

    public async Task UpdateGenerationJobAsync(
        GenerationJobRecord job,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE generation_jobs SET
                                  status = $1,
                                  output_path = $2,
                                  error_message = $3,
                                  completed_at = $4
                              WHERE id = $5
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = job.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.OutputUrl ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.ErrorMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.CompletedAt ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = job.JobId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<GenerationJobRecord>> GetGenerationJobsByWorkspaceAsync(
        string workspaceId,
        int limit = 50,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var clampedLimit = Math.Clamp(limit, 1, 1000);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, workspace_id, profile_id, status,"
                          + " output_path, error_message, queued_at, completed_at"
                          + " FROM generation_jobs WHERE workspace_id = $1"
                          + " ORDER BY queued_at DESC LIMIT "
                          + clampedLimit.ToString(CultureInfo.InvariantCulture);
        cmd.Parameters.Add(new DuckDBParameter { Value = workspaceId });

        var jobs = new List<GenerationJobRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            jobs.Add(MapGenerationJob(reader));

        return jobs;
    }


    private static ConfigSelectionRecord MapConfigSelection(DbDataReader reader)
    {
        var fallback = TimeProvider.System.GetUtcNow().UtcDateTime;
        return new ConfigSelectionRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.Col(2).AsString,
            reader.Col(3).AsDateTime ?? fallback);
    }

    private static GenerationJobRecord MapGenerationJob(DbDataReader reader)
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
