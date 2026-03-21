using Qyl.Collector.Autofix;

namespace Qyl.Collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with triage result
///     storage operations for the Loom triage pipeline.
/// </summary>
public sealed partial class DuckDbStore
{
    private const string TriageSelectSql = """
                                           SELECT triage_id, issue_id, fixability_score, automation_level,
                                                  ai_summary, root_cause_hypothesis, triggered_by,
                                                  fix_run_id, scoring_method, created_at
                                           FROM triage_results
                                           """;

    /// <summary>
    ///     Inserts a new triage result via the channel-buffered write path.
    /// </summary>
    public async Task InsertTriageResultAsync(TriageResult result, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO triage_results
                                  (triage_id, issue_id, fixability_score, automation_level,
                                   ai_summary, root_cause_hypothesis, triggered_by,
                                   fix_run_id, scoring_method)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                              ON CONFLICT (triage_id) DO NOTHING
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = result.TriageId });
            cmd.Parameters.Add(new DuckDBParameter { Value = result.IssueId });
            cmd.Parameters.Add(new DuckDBParameter { Value = result.FixabilityScore });
            cmd.Parameters.Add(new DuckDBParameter { Value = result.AutomationLevel });
            cmd.Parameters.Add(new DuckDBParameter { Value = result.AiSummary ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = result.RootCauseHypothesis ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = result.TriggeredBy });
            cmd.Parameters.Add(new DuckDBParameter { Value = result.FixRunId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = result.ScoringMethod });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    ///     Updates a triage result's fix_run_id after routing to autofix.
    /// </summary>
    public async Task UpdateTriageFixRunAsync(string triageId, string fixRunId, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE triage_results SET fix_run_id = $1 WHERE triage_id = $2";
            cmd.Parameters.Add(new DuckDBParameter { Value = fixRunId });
            cmd.Parameters.Add(new DuckDBParameter { Value = triageId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    ///     Gets a single triage result by triage_id.
    /// </summary>
    public Task<TriageResult?> GetTriageResultAsync(string triageId, CancellationToken ct = default) =>
        ReadOneAsync(
            TriageSelectSql + " WHERE triage_id = $1",
            cmd => cmd.Parameters.Add(new DuckDBParameter { Value = triageId }),
            MapTriageResult, ct);

    /// <summary>
    ///     Gets the latest triage result for a specific issue.
    /// </summary>
    public Task<TriageResult?> GetLatestTriageForIssueAsync(string issueId, CancellationToken ct = default) =>
        ReadOneAsync(
            TriageSelectSql + " WHERE issue_id = $1 ORDER BY created_at DESC LIMIT 1",
            cmd => cmd.Parameters.Add(new DuckDBParameter { Value = issueId }),
            MapTriageResult, ct);

    /// <summary>
    ///     Gets triage results with optional filtering by automation level.
    /// </summary>
    public async Task<IReadOnlyList<TriageResult>> GetTriageResultsAsync(
        string? automationLevel = null, int limit = 50, CancellationToken ct = default)
    {
        var qb = new QueryBuilder();
        if (automationLevel is not null)
            qb.Add("automation_level = $N", automationLevel);

        return await ReadManyAsync(
            $"{TriageSelectSql} {qb.WhereClause} ORDER BY created_at DESC LIMIT {qb.NextParam}",
            cmd =>
            {
                qb.ApplyTo(cmd);
                cmd.Parameters.Add(new DuckDBParameter { Value = limit });
            },
            MapTriageResult, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Returns issue IDs that have no triage result yet (untriaged errors).
    /// </summary>
    public Task<IReadOnlyList<string>> GetUntriagedIssueIdsAsync(
        int limit = 100, CancellationToken ct = default) =>
        ReadManyAsync(
            """
            SELECT e.error_id
            FROM errors e
            LEFT JOIN triage_results t ON e.error_id = t.issue_id
            WHERE t.triage_id IS NULL
              AND e.status NOT IN ('resolved', 'ignored', 'wont_fix')
            ORDER BY e.last_seen DESC
            LIMIT $1
            """,
            cmd => cmd.Parameters.Add(new DuckDBParameter { Value = limit }),
            static reader => reader.GetString(0), ct);

    private static TriageResult MapTriageResult(DbDataReader reader) =>
        new()
        {
            TriageId = reader.GetString(0),
            IssueId = reader.GetString(1),
            FixabilityScore = reader.GetDouble(2),
            AutomationLevel = reader.GetString(3),
            AiSummary = reader.Col(4).AsString,
            RootCauseHypothesis = reader.Col(5).AsString,
            TriggeredBy = reader.GetString(6),
            FixRunId = reader.Col(7).AsString,
            ScoringMethod = reader.GetString(8),
            CreatedAt = reader.Col(9).AsDateTime
        };
}
