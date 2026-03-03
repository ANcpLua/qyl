namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with read-only
///     queries for regression events from the <c>issue_events</c> table.
/// </summary>
public sealed partial class DuckDbStore
{
    /// <summary>
    ///     Gets regression events across all issues, ordered by most recent first.
    /// </summary>
    public async Task<IReadOnlyList<RegressionEventRow>> GetRegressionEventsAsync(
        int limit, DateTime? since = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        if (since is not null)
        {
            cmd.CommandText = """
                              SELECT event_id, issue_id, old_value, new_value, reason, created_at
                              FROM issue_events
                              WHERE event_type = 'regression' AND created_at >= $1
                              ORDER BY created_at DESC
                              LIMIT $2
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = since.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });
        }
        else
        {
            cmd.CommandText = """
                              SELECT event_id, issue_id, old_value, new_value, reason, created_at
                              FROM issue_events
                              WHERE event_type = 'regression'
                              ORDER BY created_at DESC
                              LIMIT $1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });
        }

        var results = new List<RegressionEventRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapRegressionEventRow(reader));

        return results;
    }

    /// <summary>
    ///     Gets regression events for a specific issue, ordered by most recent first.
    /// </summary>
    public async Task<IReadOnlyList<RegressionEventRow>> GetIssueRegressionEventsAsync(
        string issueId, int limit, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        cmd.CommandText = """
                          SELECT event_id, issue_id, old_value, new_value, reason, created_at
                          FROM issue_events
                          WHERE event_type = 'regression' AND issue_id = $1
                          ORDER BY created_at DESC
                          LIMIT $2
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var results = new List<RegressionEventRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapRegressionEventRow(reader));

        return results;
    }

    private static RegressionEventRow MapRegressionEventRow(DbDataReader reader) =>
        new()
        {
            EventId = reader.GetString(0),
            IssueId = reader.GetString(1),
            OldValue = reader.Col(2).AsString,
            NewValue = reader.Col(3).AsString,
            Reason = reader.Col(4).AsString,
            CreatedAt = reader.Col(5).AsDateTime
        };
}

/// <summary>
///     Projection of a regression event from the <c>issue_events</c> table.
/// </summary>
public sealed record RegressionEventRow
{
    public required string EventId { get; init; }
    public required string IssueId { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? Reason { get; init; }
    public DateTime? CreatedAt { get; init; }
}
