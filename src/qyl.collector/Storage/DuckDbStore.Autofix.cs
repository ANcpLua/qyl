using qyl.collector.Autofix;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with fix run
///     storage operations for the autofix module.
/// </summary>
public sealed partial class DuckDbStore
{
    /// <summary>
    ///     Inserts a new fix run record via the channel-buffered write path.
    /// </summary>
    public async Task InsertFixRunAsync(FixRunRecord record, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO fix_runs
                                  (run_id, issue_id, execution_id, status, policy,
                                   fix_description, confidence_score, changes_json)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
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
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates a fix run's status, description, confidence score, and changes.
    /// </summary>
    public async Task UpdateFixRunAsync(
        string runId, string status, string? description = null,
        double? confidence = null, string? changesJson = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE fix_runs SET
                                  status = $1,
                                  fix_description = $2,
                                  confidence_score = $3,
                                  changes_json = $4,
                                  completed_at = CASE WHEN $1 IN ('applied', 'failed') THEN now() ELSE completed_at END
                              WHERE run_id = $5
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            cmd.Parameters.Add(new DuckDBParameter { Value = description ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = confidence ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = changesJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = runId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a single fix run by run_id.
    /// </summary>
    public async Task<FixRunRecord?> GetFixRunAsync(string runId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT run_id, issue_id, execution_id, status, policy,
                                 fix_description, confidence_score, changes_json,
                                 created_at, completed_at
                          FROM fix_runs WHERE run_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = runId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapFixRun(reader);
    }

    /// <summary>
    ///     Gets fix runs for a specific issue, ordered by creation time descending.
    /// </summary>
    public async Task<IReadOnlyList<FixRunRecord>> GetFixRunsAsync(
        string issueId, int limit = 50, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT run_id, issue_id, execution_id, status, policy,
                                 fix_description, confidence_score, changes_json,
                                 created_at, completed_at
                          FROM fix_runs
                          WHERE issue_id = $1
                          ORDER BY created_at DESC
                          LIMIT $2
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var results = new List<FixRunRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapFixRun(reader));

        return results;
    }

    private static FixRunRecord MapFixRun(IDataReader reader) =>
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
            CreatedAt = reader.Col(8).AsDateTime,
            CompletedAt = reader.Col(9).AsDateTime
        };
}
