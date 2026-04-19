namespace Qyl.Collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with issue lifecycle,
///     ownership, and regression detection storage operations.
/// </summary>
public sealed partial class DuckDbStore
{
    // ==========================================================================
    // Issue Events DDL (applied inline during schema init)
    // ==========================================================================

    internal const string IssueEventsDdl = """
                                           CREATE TABLE IF NOT EXISTS issue_events (
                                               event_id VARCHAR PRIMARY KEY,
                                               issue_id VARCHAR NOT NULL,
                                               event_type VARCHAR NOT NULL,
                                               old_value VARCHAR,
                                               new_value VARCHAR,
                                               reason VARCHAR,
                                               created_at TIMESTAMP DEFAULT now()
                                           );
                                           CREATE INDEX IF NOT EXISTS idx_issue_events_issue ON issue_events(issue_id);
                                           CREATE INDEX IF NOT EXISTS idx_issue_events_type ON issue_events(event_type);
                                           CREATE INDEX IF NOT EXISTS idx_issue_events_created ON issue_events(created_at DESC);
                                           """;

    // ==========================================================================
    // Issue Lifecycle Operations
    // ==========================================================================

    /// <summary>
    ///     Updates the status of an error (issue) by error_id, enforcing lifecycle transitions.
    /// </summary>
    public async Task UpdateIssueStatusAsync(string issueId, string newStatus, string? reason = null,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);

            // Read current status
            await using var readCmd = con.CreateCommand();
            readCmd.Transaction = tx;
            readCmd.CommandText = "SELECT status FROM errors WHERE error_id = $1";
            readCmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            if ((string?)await readCmd.ExecuteScalarAsync(token).ConfigureAwait(false) is not
                { } currentStatus) return;

            // Update error status
            await using var updateCmd = con.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = "UPDATE errors SET status = $1 WHERE error_id = $2";
            updateCmd.Parameters.Add(new DuckDBParameter { Value = newStatus });
            updateCmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            await updateCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            // Record the lifecycle event
            await InsertIssueEventInternalAsync(con, issueId, "status_change", currentStatus, newStatus, reason,
                token, tx).ConfigureAwait(false);

            await tx.CommitAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    ///     Assigns an owner to an issue (error).
    /// </summary>
    public async Task AssignIssueOwnerAsync(string issueId, string owner, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);

            // Read current owner
            await using var readCmd = con.CreateCommand();
            readCmd.Transaction = tx;
            readCmd.CommandText = "SELECT assigned_to FROM errors WHERE error_id = $1";
            readCmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            var currentOwner = (string?)await readCmd.ExecuteScalarAsync(token).ConfigureAwait(false);

            // Update owner
            await using var updateCmd = con.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = "UPDATE errors SET assigned_to = $1 WHERE error_id = $2";
            updateCmd.Parameters.Add(new DuckDBParameter { Value = owner });
            updateCmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            await updateCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            // Record ownership event
            await InsertIssueEventInternalAsync(con, issueId, "assigned", currentOwner, owner, null, token, tx)
                .ConfigureAwait(false);

            await tx.CommitAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    ///     Gets the owner of an issue by error_id.
    /// </summary>
    public async Task<string?> GetIssueOwnerAsync(string issueId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT assigned_to FROM errors WHERE error_id = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is DBNull or null ? null : (string)result;
    }

    /// <summary>
    ///     Gets issues (errors grouped by fingerprint) with optional status/owner filtering.
    /// </summary>
    public async Task<IReadOnlyList<IssueSummary>> GetIssuesAsync(
        string? status = null,
        string? owner = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var qb = new QueryBuilder();
        if (!string.IsNullOrEmpty(status))
            qb.Add("status = $N", status);
        if (!string.IsNullOrEmpty(owner))
            qb.Add("assigned_to = $N", owner);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT error_id, fingerprint, error_type, message, status,"
            + " assigned_to, occurrence_count, first_seen, last_seen"
            + " FROM errors "
            + qb.WhereClause
            + " ORDER BY last_seen DESC LIMIT "
            + qb.NextParam.ToString(CultureInfo.InvariantCulture);

        qb.ApplyTo(cmd);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var results = new List<IssueSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new IssueSummary
            {
                IssueId = reader.GetString(0),
                Fingerprint = reader.GetString(1),
                ErrorType = reader.GetString(2),
                ErrorMessage = reader.Col(3).AsString,
                Status = ParseIssueStatus(reader.GetString(4)),
                Owner = reader.Col(5).AsString,
                EventCount = (int)reader.GetInt64(6),
                FirstSeen = reader.GetDateTime(7),
                LastSeen = reader.GetDateTime(8)
            });
        }

        return results;
    }

    /// <summary>
    ///     Gets a single issue (error) detail by error_id.
    /// </summary>
    public async Task<IssueSummary?> GetIssueByIdAsync(string issueId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT error_id, fingerprint, error_type, message, status,
                                 assigned_to, occurrence_count, first_seen, last_seen
                          FROM errors WHERE error_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return new IssueSummary
        {
            IssueId = reader.GetString(0),
            Fingerprint = reader.GetString(1),
            ErrorType = reader.GetString(2),
            ErrorMessage = reader.Col(3).AsString,
            Status = ParseIssueStatus(reader.GetString(4)),
            Owner = reader.Col(5).AsString,
            EventCount = (int)reader.GetInt64(6),
            FirstSeen = reader.GetDateTime(7),
            LastSeen = reader.GetDateTime(8)
        };
    }

    /// <summary>
    ///     Gets the event timeline for a specific issue.
    /// </summary>
    public async Task<IReadOnlyList<IssueEvent>> GetIssueEventsAsync(string issueId, int limit = 100,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT event_id, issue_id, event_type, old_value, new_value, reason, created_at
                          FROM issue_events
                          WHERE issue_id = $1
                          ORDER BY created_at DESC
                          LIMIT $2
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var results = new List<IssueEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new IssueEvent(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.Col(3).AsString,
                reader.Col(4).AsString,
                reader.Col(5).AsString,
                reader.GetDateTime(6)));
        }

        return results;
    }

    /// <summary>
    ///     Finds resolved errors whose fingerprint has reappeared, transitioning them to regressed.
    /// </summary>
    public async Task<IReadOnlyList<string>> DetectRegressionsAsync(string serviceName, string? deployVersion = null,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);

            // Find errors that were resolved but have new occurrences (last_seen > resolved marker)
            // We detect regressions by finding errors with status='resolved' whose fingerprint
            // matches errors in the same service with recent occurrences.
            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                              SELECT error_id, fingerprint
                              FROM errors
                              WHERE status = 'resolved'
                                AND affected_services LIKE '%' || $1 || '%' ESCAPE '!'
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = EscapeLikePattern(serviceName) });

            var regressedIds = new List<string>();
            var now = TimeProvider.System.GetUtcNow().UtcDateTime;

            await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            var candidates = new List<(string ErrorId, string Fingerprint)>();
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                candidates.Add((reader.GetString(0), reader.GetString(1)));

            if (candidates.Count is 0)
                return regressedIds;

            var fingerprints = candidates
                .Select(static candidate => candidate.Fingerprint)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var regressedFingerprints = new HashSet<string>(StringComparer.Ordinal);
            await using (var checkCmd = con.CreateCommand())
            {
                checkCmd.Transaction = tx;
                var placeholders = string.Join(", ", fingerprints.Select(static (_, index) => $"${index + 1}"));
                checkCmd.CommandText = "SELECT DISTINCT fingerprint FROM errors"
                    + " WHERE status = 'new' AND fingerprint IN ("
                    + placeholders + ")";

                foreach (var fingerprint in fingerprints)
                    checkCmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });

                await using var checkReader = await checkCmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await checkReader.ReadAsync(token).ConfigureAwait(false))
                    regressedFingerprints.Add(checkReader.GetString(0));
            }

            foreach (var (errorId, fingerprint) in candidates)
            {
                if (!regressedFingerprints.Contains(fingerprint))
                    continue;

                // Transition to regressed
                await using var updateCmd = con.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = "UPDATE errors SET status = 'regressed' WHERE error_id = $1";
                updateCmd.Parameters.Add(new DuckDBParameter { Value = errorId });
                await updateCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                var reason = deployVersion is not null
                    ? $"Regression detected in deployment {deployVersion}"
                    : "Regression detected: same fingerprint reappeared";
                await InsertIssueEventInternalAsync(con, errorId, "regression", "resolved", "regressed", reason,
                    token, tx).ConfigureAwait(false);

                regressedIds.Add(errorId);
            }

            await tx.CommitAsync(token).ConfigureAwait(false);
            return (IReadOnlyList<string>)regressedIds;
        }, ct).ConfigureAwait(false);

    // ==========================================================================
    // Internal Helpers
    // ==========================================================================

    private static async ValueTask InsertIssueEventInternalAsync(
        DuckDBConnection con, string issueId, string eventType,
        string? oldValue, string? newValue, string? reason, CancellationToken ct,
        DbTransaction? transaction = null)
    {
        await using var cmd = con.CreateCommand();
        if (transaction is not null) cmd.Transaction = transaction;
        cmd.CommandText = """
                          INSERT INTO issue_events (event_id, issue_id, event_type, old_value, new_value, reason)
                          VALUES ($1, $2, $3, $4, $5, $6)
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = Guid.NewGuid().ToString("N") });
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
        cmd.Parameters.Add(new DuckDBParameter { Value = eventType });
        cmd.Parameters.Add(new DuckDBParameter { Value = oldValue ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = newValue ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = reason ?? (object)DBNull.Value });
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static IssueStatus ParseIssueStatus(string status) =>
        status switch
        {
            "new" => IssueStatus.New,
            "acknowledged" => IssueStatus.Acknowledged,
            "resolved" => IssueStatus.Resolved,
            "regressed" => IssueStatus.Regressed,
            "reopened" => IssueStatus.Reopened,
            _ => IssueStatus.New
        };

    private static string EscapeLikePattern(string value)
        => value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
}
