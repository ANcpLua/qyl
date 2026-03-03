using qyl.collector.Autofix;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with triage result
///     storage operations for the Seer triage pipeline.
/// </summary>
public sealed partial class DuckDbStore
{
    /// <summary>
    ///     Inserts a new triage result via the channel-buffered write path.
    /// </summary>
    public async Task InsertTriageResultAsync(TriageResult result, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
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
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates a triage result's fix_run_id after routing to autofix.
    /// </summary>
    public async Task UpdateTriageFixRunAsync(string triageId, string fixRunId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE triage_results SET fix_run_id = $1 WHERE triage_id = $2";
            cmd.Parameters.Add(new DuckDBParameter { Value = fixRunId });
            cmd.Parameters.Add(new DuckDBParameter { Value = triageId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a single triage result by triage_id.
    /// </summary>
    public async Task<TriageResult?> GetTriageResultAsync(string triageId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT triage_id, issue_id, fixability_score, automation_level,
                                 ai_summary, root_cause_hypothesis, triggered_by,
                                 fix_run_id, scoring_method, created_at
                          FROM triage_results WHERE triage_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = triageId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapTriageResult(reader);
    }

    /// <summary>
    ///     Gets the latest triage result for a specific issue.
    /// </summary>
    public async Task<TriageResult?> GetLatestTriageForIssueAsync(string issueId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT triage_id, issue_id, fixability_score, automation_level,
                                 ai_summary, root_cause_hypothesis, triggered_by,
                                 fix_run_id, scoring_method, created_at
                          FROM triage_results
                          WHERE issue_id = $1
                          ORDER BY created_at DESC
                          LIMIT 1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapTriageResult(reader);
    }

    /// <summary>
    ///     Gets triage results with optional filtering by automation level.
    /// </summary>
    public async Task<IReadOnlyList<TriageResult>> GetTriageResultsAsync(
        string? automationLevel = null, int limit = 50, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        if (automationLevel is not null)
        {
            cmd.CommandText = """
                              SELECT triage_id, issue_id, fixability_score, automation_level,
                                     ai_summary, root_cause_hypothesis, triggered_by,
                                     fix_run_id, scoring_method, created_at
                              FROM triage_results
                              WHERE automation_level = $1
                              ORDER BY created_at DESC
                              LIMIT $2
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = automationLevel });
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });
        }
        else
        {
            cmd.CommandText = """
                              SELECT triage_id, issue_id, fixability_score, automation_level,
                                     ai_summary, root_cause_hypothesis, triggered_by,
                                     fix_run_id, scoring_method, created_at
                              FROM triage_results
                              ORDER BY created_at DESC
                              LIMIT $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });
        }

        var results = new List<TriageResult>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapTriageResult(reader));

        return results;
    }

    /// <summary>
    ///     Returns issue IDs that have no triage result yet (untriaged errors).
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUntriagedIssueIdsAsync(
        int limit = 100, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT e.error_id
                          FROM errors e
                          LEFT JOIN triage_results t ON e.error_id = t.issue_id
                          WHERE t.triage_id IS NULL
                            AND e.status NOT IN ('resolved', 'ignored', 'wont_fix')
                          ORDER BY e.last_seen DESC
                          LIMIT $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var ids = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            ids.Add(reader.GetString(0));

        return ids;
    }

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
