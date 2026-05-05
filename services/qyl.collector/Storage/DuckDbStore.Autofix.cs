namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{
    private const string FixRunSelectSql = """
                                           SELECT run_id, issue_id, execution_id, status, policy,
                                                  fix_description, confidence_score, changes_json,
                                                  instruction, stopping_point,
                                                  created_at, completed_at
                                           FROM fix_runs
                                           """;

    public async Task InsertFixRunAsync(FixRunRecord record, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO fix_runs
                                  (run_id, issue_id, execution_id, status, policy,
                                   fix_description, confidence_score, changes_json,
                                   instruction, stopping_point)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
                              ON CONFLICT (run_id) DO NOTHING
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = record.RunId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.IssueId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ExecutionId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Policy });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.FixDescription ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ConfidenceScore ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ChangesJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Instruction ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.StoppingPoint ?? (object)DBNull.Value });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task UpdateFixRunAsync(
        string runId, string status, string? description = null,
        double? confidence = null, string? changesJson = null,
        string? instructionAppend = null, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE fix_runs SET
                                  status = $1,
                                  fix_description = COALESCE($2, fix_description),
                                  confidence_score = COALESCE($3, confidence_score),
                                  changes_json = COALESCE($4, changes_json),
                                  instruction = CASE
                                      WHEN $5 IS NULL THEN instruction
                                      WHEN instruction IS NULL THEN $5
                                      ELSE instruction || E'\n---\n' || $5
                                  END,
                                  completed_at = CASE WHEN $1 IN ('applied', 'failed', 'rejected') THEN now() ELSE completed_at END
                              WHERE run_id = $6
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            cmd.Parameters.Add(new DuckDBParameter { Value = description ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = confidence ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = changesJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = instructionAppend ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = runId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task<FixRunRecord> CreateFixRunAsync(
        string issueId, FixPolicy policy, CancellationToken ct = default)
    {
        var record = new FixRunRecord
        {
            RunId = Guid.NewGuid().ToString("N"),
            IssueId = issueId,
            Status = "pending",
            Policy = policy.ToString().ToLowerInvariant()
        };
        await InsertFixRunAsync(record, ct).ConfigureAwait(false);
        return record;
    }

    public Task<IReadOnlyList<FixRunRecord>> GetFixRunsByStatusAsync(
        string status, int limit = 50, CancellationToken ct = default) =>
        ReadManyAsync(
            FixRunSelectSql + " WHERE status = $1 ORDER BY created_at ASC LIMIT $2",
            cmd =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = status });
                cmd.Parameters.Add(new DuckDBParameter { Value = limit });
            },
            MapFixRun, ct);

    public Task<FixRunRecord?> GetFixRunAsync(string runId, CancellationToken ct = default) =>
        ReadOneAsync(
            FixRunSelectSql + " WHERE run_id = $1",
            cmd => cmd.Parameters.Add(new DuckDBParameter { Value = runId }),
            MapFixRun, ct);

    public Task<IReadOnlyList<FixRunRecord>> GetFixRunsAsync(
        string issueId, int limit = 50, CancellationToken ct = default) =>
        ReadManyAsync(
            FixRunSelectSql + " WHERE issue_id = $1 ORDER BY created_at DESC LIMIT $2",
            cmd =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
                cmd.Parameters.Add(new DuckDBParameter { Value = limit });
            },
            MapFixRun, ct);

    private static FixRunRecord MapFixRun(DbDataReader reader) =>
        new()
        {
            RunId = reader.GetString(0),
            IssueId = reader.GetString(1),
            ExecutionId = reader.Col(2).AsString,
            Status = reader.GetString(3),
            Policy = reader.GetString(4),
            FixDescription = reader.Col(5).AsString,
            ConfidenceScore = reader.Col(6).AsDouble,
            ChangesJson = reader.Col(7).AsString,
            Instruction = reader.Col(8).AsString,
            StoppingPoint = reader.Col(9).AsString,
            CreatedAt = reader.Col(10).AsDateTime,
            CompletedAt = reader.Col(11).AsDateTime
        };
}
