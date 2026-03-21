namespace Qyl.Collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with read-only
///     queries for regression events from the <c>issue_events</c> table.
/// </summary>
public sealed partial class DuckDbStore
{
    private const string RegressionEventSelectSql = """
                                                    SELECT event_id, issue_id, old_value, new_value, reason, created_at
                                                    FROM issue_events
                                                    WHERE event_type = 'regression'
                                                    """;

    /// <summary>
    ///     Gets regression events across all issues, ordered by most recent first.
    /// </summary>
    public async Task<IReadOnlyList<RegressionEventRow>> GetRegressionEventsAsync(
        int limit, DateTime? since = null, CancellationToken ct = default)
    {
        var qb = new QueryBuilder();
        qb.AddCondition("event_type = 'regression'");
        if (since is not null)
            qb.Add("created_at >= $N", since.Value);

        return await ReadManyAsync(
            $"""
             SELECT event_id, issue_id, old_value, new_value, reason, created_at
             FROM issue_events
             {qb.WhereClause}
             ORDER BY created_at DESC
             LIMIT {qb.NextParam}
             """,
            cmd =>
            {
                qb.ApplyTo(cmd);
                cmd.Parameters.Add(new DuckDBParameter { Value = limit });
            },
            MapRegressionEventRow, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets regression events for a specific issue, ordered by most recent first.
    /// </summary>
    public Task<IReadOnlyList<RegressionEventRow>> GetIssueRegressionEventsAsync(
        string issueId, int limit, CancellationToken ct = default) =>
        ReadManyAsync(
            RegressionEventSelectSql + """
                                        AND issue_id = $1
                                       ORDER BY created_at DESC
                                       LIMIT $2
                                       """,
            cmd =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
                cmd.Parameters.Add(new DuckDBParameter { Value = limit });
            },
            MapRegressionEventRow, ct);

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
