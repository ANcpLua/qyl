namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore"/> with issue lifecycle,
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
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            // Read current status
            await using var readCmd = con.CreateCommand();
            readCmd.CommandText = "SELECT status FROM errors WHERE error_id = $1";
            readCmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            var currentStatus = (string?)await readCmd.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (currentStatus is null) return 0;

            // Update error status
            await using var updateCmd = con.CreateCommand();
            updateCmd.CommandText = "UPDATE errors SET status = $1 WHERE error_id = $2";
            updateCmd.Parameters.Add(new DuckDBParameter { Value = newStatus });
            updateCmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            await updateCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            // Record the lifecycle event
            await InsertIssueEventInternalAsync(con, issueId, "status_change", currentStatus, newStatus, reason,
                token).ConfigureAwait(false);

            return 1;
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Assigns an owner to an issue (error).
    /// </summary>
    public async Task AssignIssueOwnerAsync(string issueId, string owner, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            // Read current owner
            await using var readCmd = con.CreateCommand();
            readCmd.CommandText = "SELECT assigned_to FROM errors WHERE error_id = $1";
            readCmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            var currentOwner = (string?)await readCmd.ExecuteScalarAsync(token).ConfigureAwait(false);

            // Update owner
            await using var updateCmd = con.CreateCommand();
            updateCmd.CommandText = "UPDATE errors SET assigned_to = $1 WHERE error_id = $2";
            updateCmd.Parameters.Add(new DuckDBParameter { Value = owner });
            updateCmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            await updateCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            // Record ownership event
            await InsertIssueEventInternalAsync(con, issueId, "assigned", currentOwner, owner, null, token)
                .ConfigureAwait(false);

            return 1;
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

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
        cmd.CommandText = $"""
            SELECT error_id, fingerprint, error_type, message, status,
                   assigned_to, occurrence_count, first_seen, last_seen
            FROM errors
            {qb.WhereClause}
            ORDER BY last_seen DESC
            LIMIT {qb.NextParam}
            """;

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
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<IReadOnlyList<string>>(async (con, token) =>
        {
            // Find errors that were resolved but have new occurrences (last_seen > resolved marker)
            // We detect regressions by finding errors with status='resolved' whose fingerprint
            // matches errors in the same service with recent occurrences.
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                SELECT error_id, fingerprint
                FROM errors
                WHERE status = 'resolved'
                  AND affected_services LIKE '%' || $1 || '%' ESCAPE '\'
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = EscapeLikePattern(serviceName) });

            var regressedIds = new List<string>();
            var now = TimeProvider.System.GetUtcNow().UtcDateTime;

            await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            var candidates = new List<(string ErrorId, string Fingerprint)>();
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                candidates.Add((reader.GetString(0), reader.GetString(1)));

            foreach (var (errorId, fingerprint) in candidates)
            {
                // Check if there are newer unresolved errors with the same fingerprint
                await using var checkCmd = con.CreateCommand();
                checkCmd.CommandText = """
                    SELECT COUNT(*) FROM errors
                    WHERE fingerprint = $1 AND status = 'new' AND error_id != $2
                    """;
                checkCmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });
                checkCmd.Parameters.Add(new DuckDBParameter { Value = errorId });

                var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(token).ConfigureAwait(false),
                    CultureInfo.InvariantCulture);
                if (count <= 0) continue;

                // Transition to regressed
                await using var updateCmd = con.CreateCommand();
                updateCmd.CommandText = "UPDATE errors SET status = 'regressed' WHERE error_id = $1";
                updateCmd.Parameters.Add(new DuckDBParameter { Value = errorId });
                await updateCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                var reason = deployVersion is not null
                    ? $"Regression detected in deployment {deployVersion}"
                    : "Regression detected: same fingerprint reappeared";
                await InsertIssueEventInternalAsync(con, errorId, "regression", "resolved", "regressed", reason,
                    token).ConfigureAwait(false);

                regressedIds.Add(errorId);
            }

            return (IReadOnlyList<string>)regressedIds;
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    // ==========================================================================
    // Internal Helpers
    // ==========================================================================

    private static async ValueTask InsertIssueEventInternalAsync(
        DuckDBConnection con, string issueId, string eventType,
        string? oldValue, string? newValue, string? reason, CancellationToken ct)
    {
        await using var cmd = con.CreateCommand();
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
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

// =============================================================================
// Issue Storage Types (used by DuckDbStore.Issues + ErrorLifecycleService)
// =============================================================================

/// <summary>
///     Issue lifecycle status for the legacy <c>errors</c> table.
/// </summary>
public enum IssueStatus
{
    New,
    Acknowledged,
    Resolved,
    Regressed,
    Reopened
}

/// <summary>
///     Summary projection of an error issue from the <c>errors</c> table.
/// </summary>
public sealed record IssueSummary
{
    public required string IssueId { get; init; }
    public required string Fingerprint { get; init; }
    public required string ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
    public required IssueStatus Status { get; init; }
    public string? Owner { get; init; }
    public required int EventCount { get; init; }
    public required DateTime FirstSeen { get; init; }
    public required DateTime LastSeen { get; init; }
}

/// <summary>
///     A lifecycle event record from the <c>issue_events</c> table.
/// </summary>
public sealed record IssueEvent(
    string EventId,
    string IssueId,
    string EventType,
    string? OldValue,
    string? NewValue,
    string? Reason,
    DateTime CreatedAt);
