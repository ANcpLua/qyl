namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{

    public async Task InsertCodingAgentRunAsync(CodingAgentRunRecord record, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task UpdateCodingAgentRunStatusAsync(
        string id, string status, string? prUrl = null, string? agentUrl = null,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public Task<CodingAgentRunRecord?> GetCodingAgentRunAsync(string id, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<CodingAgentRunRecord?>(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT id, fix_run_id, provider, status, agent_url, pr_url,
                                     repo_full_name, created_at, completed_at
                              FROM coding_agent_runs WHERE id = $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = id });

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return MapCodingAgentRun(reader);
        }, ct);
    }

    public Task<IReadOnlyList<CodingAgentRunRecord>> GetCodingAgentRunsForFixRunAsync(
        string fixRunId, int limit = 50, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<CodingAgentRunRecord>>(con =>
        {
            using var cmd = con.CreateCommand();
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
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                results.Add(MapCodingAgentRun(reader));

            return results;
        }, ct);
    }

    private static CodingAgentRunRecord MapCodingAgentRun(DbDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            FixRunId = reader.GetString(1),
            Provider = CodingAgentProviderNames.NormalizeSlug(reader.GetString(2)),
            Status = reader.GetString(3),
            AgentUrl = reader.Col(4).AsString,
            PrUrl = reader.Col(5).AsString,
            RepoFullName = reader.Col(6).AsString,
            CreatedAt = reader.Col(7).AsDateTime ?? DateTime.MinValue,
            CompletedAt = reader.Col(8).AsDateTime
        };


    public Task<LoomSettingsRecord> GetLoomSettingsAsync(string orgId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT id, default_coding_agent, default_coding_agent_integration_id,
                                     automation_tuning, updated_at
                              FROM Loom_settings WHERE id = $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = orgId });

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new LoomSettingsRecord
                {
                    Id = orgId,
                    DefaultCodingAgent = CodingAgentProviderNames.ToSlug(CodingAgentProvider.Loom),
                    AutomationTuning = "medium",
                    UpdatedAt = DateTime.UnixEpoch
                };
            }

            return new LoomSettingsRecord
            {
                Id = reader.GetString(0),
                DefaultCodingAgent = CodingAgentProviderNames.NormalizeSlug(reader.Col(1).AsString),
                DefaultCodingAgentIntegrationId = reader.Col(2).AsString,
                AutomationTuning = reader.Col(3).AsString ?? "medium",
                UpdatedAt = reader.Col(4).AsDateTime ?? DateTime.MinValue
            };
        }, ct);
    }

    public async Task UpsertLoomSettingsAsync(LoomSettingsRecord settings, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = CodingAgentProviderNames.NormalizeSlug(settings.DefaultCodingAgent)
            });
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = settings.DefaultCodingAgentIntegrationId ?? (object)DBNull.Value
            });
            cmd.Parameters.Add(new DuckDBParameter { Value = settings.AutomationTuning });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
}
