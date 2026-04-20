namespace Qyl.Collector.Storage;

using Autofix;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with agent handoff
///     storage operations for the autofix module.
/// </summary>
public sealed partial class DuckDbStore
{
    private const string HandoffSelectSql = """
                                            SELECT handoff_id, run_id, agent_type, status, context_json,
                                                   result_json, error_message, accepted_at, submitted_at,
                                                   failed_at, timeout_at, created_at
                                            FROM agent_handoffs
                                            """;

    /// <summary>
    ///     Inserts a new agent handoff record via the channel-buffered write path.
    /// </summary>
    public async Task InsertHandoffAsync(AgentHandoffRecord record, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO agent_handoffs
                                  (handoff_id, run_id, agent_type, status, context_json,
                                   result_json, error_message)
                              VALUES ($1, $2, $3, $4, $5, $6, $7)
                              ON CONFLICT (handoff_id) DO NOTHING
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = record.HandoffId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.RunId });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.AgentType });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ContextJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ResultJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ErrorMessage ?? (object)DBNull.Value });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    ///     Updates a handoff's status with conditional timestamp setting.
    /// </summary>
    public async Task<int> UpdateHandoffStatusAsync(
        string handoffId, string status, string? resultJson = null,
        string? errorMessage = null, string? expectedCurrentStatus = null,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = expectedCurrentStatus is not null
                ? """
                  UPDATE agent_handoffs SET
                      status = $1,
                      result_json = $2,
                      error_message = $3,
                      accepted_at = CASE WHEN $1 = 'accepted' THEN now() ELSE accepted_at END,
                      submitted_at = CASE WHEN $1 = 'completed' THEN now() ELSE submitted_at END,
                      failed_at = CASE WHEN $1 IN ('failed', 'timed_out') THEN now() ELSE failed_at END
                  WHERE handoff_id = $4 AND status = $5
                  """
                : """
                  UPDATE agent_handoffs SET
                      status = $1,
                      result_json = $2,
                      error_message = $3,
                      accepted_at = CASE WHEN $1 = 'accepted' THEN now() ELSE accepted_at END,
                      submitted_at = CASE WHEN $1 = 'completed' THEN now() ELSE submitted_at END,
                      failed_at = CASE WHEN $1 IN ('failed', 'timed_out') THEN now() ELSE failed_at END
                  WHERE handoff_id = $4
                  """;
            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            cmd.Parameters.Add(new DuckDBParameter { Value = resultJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = errorMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = handoffId });
            if (expectedCurrentStatus is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = expectedCurrentStatus });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    ///     Gets a single agent handoff by handoff_id.
    /// </summary>
    public Task<AgentHandoffRecord?> GetHandoffAsync(string handoffId, CancellationToken ct = default) =>
        ReadOneAsync(
            HandoffSelectSql + " WHERE handoff_id = $1",
            cmd => cmd.Parameters.Add(new DuckDBParameter { Value = handoffId }),
            MapHandoff, ct);

    /// <summary>
    ///     Gets pending handoffs ordered by creation time ascending (oldest first).
    /// </summary>
    public Task<IReadOnlyList<AgentHandoffRecord>> GetPendingHandoffsAsync(
        int limit = 50, CancellationToken ct = default) =>
        ReadManyAsync(
            HandoffSelectSql + " WHERE status = 'pending' ORDER BY created_at ASC LIMIT $1",
            cmd => cmd.Parameters.Add(new DuckDBParameter { Value = limit }),
            MapHandoff, ct);

    /// <summary>
    ///     Gets handoffs for a specific run, ordered by creation time descending.
    /// </summary>
    public Task<IReadOnlyList<AgentHandoffRecord>> GetHandoffsForRunAsync(
        string runId, int limit = 50, CancellationToken ct = default) =>
        ReadManyAsync(
            HandoffSelectSql + " WHERE run_id = $1 ORDER BY created_at DESC LIMIT $2",
            cmd =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = runId });
                cmd.Parameters.Add(new DuckDBParameter { Value = limit });
            },
            MapHandoff, ct);

    private static AgentHandoffRecord MapHandoff(DbDataReader reader) =>
        new()
        {
            HandoffId = reader.GetString(0),
            RunId = reader.GetString(1),
            AgentType = reader.GetString(2),
            Status = reader.GetString(3),
            ContextJson = reader.Col(4).AsString,
            ResultJson = reader.Col(5).AsString,
            ErrorMessage = reader.Col(6).AsString,
            AcceptedAt = reader.Col(7).AsDateTime,
            SubmittedAt = reader.Col(8).AsDateTime,
            FailedAt = reader.Col(9).AsDateTime,
            TimeoutAt = reader.Col(10).AsDateTime,
            CreatedAt = reader.Col(11).AsDateTime
        };
}
