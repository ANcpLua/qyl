using qyl.collector.CodingAgent;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with coding agent run
///     and Loom settings storage operations.
/// </summary>
public sealed partial class DuckDbStore
{
    // =========================================================================
    // Coding Agent Runs
    // =========================================================================

    public async Task InsertCodingAgentRunAsync(CodingAgentRunRecord record, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO coding_agent_runs
                                  (id, fix_run_id, provider, status, agent_url, pr_url, repo_full_name)
                              VALUES ($1, $2, $3, $4, $5, $6, $7)
                              ON CONFLICT (id) DO NOTHING
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Id });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.FixRunId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Provider });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.AgentUrl ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.PrUrl ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.RepoFullName ?? (object)DBNull.Value });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    public async Task UpdateCodingAgentRunStatusAsync(
        string id, string status, string? prUrl = null, string? agentUrl = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE coding_agent_runs SET
                                  status = $1,
                                  pr_url = COALESCE($2, pr_url),
                                  agent_url = COALESCE($3, agent_url),
                                  completed_at = CASE WHEN $1 IN ('completed', 'failed') THEN now() ELSE completed_at END
                              WHERE id = $4
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            cmd.Parameters.Add(new DuckDBParameter { Value = prUrl ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = agentUrl ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = id });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    public async Task<CodingAgentRunRecord?> GetCodingAgentRunAsync(string id, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, fix_run_id, provider, status, agent_url, pr_url,
                                 repo_full_name, created_at, completed_at
                          FROM coding_agent_runs WHERE id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = id });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapCodingAgentRun(reader);
    }

    public async Task<IReadOnlyList<CodingAgentRunRecord>> GetCodingAgentRunsForFixRunAsync(
        string fixRunId, int limit = 50, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, fix_run_id, provider, status, agent_url, pr_url,
                                 repo_full_name, created_at, completed_at
                          FROM coding_agent_runs
                          WHERE fix_run_id = $1
                          ORDER BY created_at DESC
                          LIMIT $2
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = fixRunId });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var results = new List<CodingAgentRunRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapCodingAgentRun(reader));

        return results;
    }

    private static CodingAgentRunRecord MapCodingAgentRun(DbDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            FixRunId = reader.GetString(1),
            Provider = reader.GetString(2),
            Status = reader.GetString(3),
            AgentUrl = reader.Col(4).AsString,
            PrUrl = reader.Col(5).AsString,
            RepoFullName = reader.Col(6).AsString,
            CreatedAt = reader.Col(7).AsDateTime ?? DateTime.MinValue,
            CompletedAt = reader.Col(8).AsDateTime
        };

    // =========================================================================
    // Loom Settings
    // =========================================================================

    public async Task<LoomSettingsRecord> GetLoomSettingsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, default_coding_agent, default_coding_agent_integration_id,
                                 automation_tuning, updated_at
                          FROM Loom_settings WHERE id = 'default'
                          """;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return new LoomSettingsRecord { Id = "default" };

        return new LoomSettingsRecord
        {
            Id = reader.GetString(0),
            DefaultCodingAgent = reader.Col(1).AsString ?? "Loom",
            DefaultCodingAgentIntegrationId = reader.Col(2).AsString,
            AutomationTuning = reader.Col(3).AsString ?? "medium",
            UpdatedAt = reader.Col(4).AsDateTime ?? DateTime.MinValue
        };
    }

    public async Task UpsertLoomSettingsAsync(LoomSettingsRecord settings, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO Loom_settings
                                  (id, default_coding_agent, default_coding_agent_integration_id, automation_tuning, updated_at)
                              VALUES ($1, $2, $3, $4, now())
                              ON CONFLICT (id) DO UPDATE SET
                                  default_coding_agent = $2,
                                  default_coding_agent_integration_id = $3,
                                  automation_tuning = $4,
                                  updated_at = now()
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = settings.Id });
            cmd.Parameters.Add(new DuckDBParameter { Value = settings.DefaultCodingAgent });
            cmd.Parameters.Add(new DuckDBParameter
                { Value = settings.DefaultCodingAgentIntegrationId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = settings.AutomationTuning });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }
}
