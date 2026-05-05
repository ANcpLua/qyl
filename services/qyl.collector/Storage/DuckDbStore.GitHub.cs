using Qyl.Collector.Autofix;

namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{
    private const string GitHubEventSelectSql = """
                                                SELECT event_id, event_type, action, repo_full_name, sender,
                                                       pr_number, pr_url, ref, payload_json, created_at
                                                FROM github_events
                                                """;

    public async Task InsertGitHubEventAsync(GitHubEventRecord @event, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO github_events
                                  (event_id, event_type, action, repo_full_name, sender,
                                   pr_number, pr_url, ref, payload_json)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                              ON CONFLICT (event_id) DO NOTHING
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = @event.EventId });
            cmd.Parameters.Add(new DuckDBParameter { Value = @event.EventType });
            cmd.Parameters.Add(new DuckDBParameter { Value = @event.Action ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = @event.RepoFullName });
            cmd.Parameters.Add(new DuckDBParameter { Value = @event.Sender ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = @event.PrNumber ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = @event.PrUrl ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = @event.Ref ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = @event.PayloadJson ?? (object)DBNull.Value });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<GitHubEventRecord>> GetGitHubEventsAsync(
        int limit = 50, string? eventType = null, string? repoFullName = null,
        CancellationToken ct = default)
    {
        var qb = new QueryBuilder();
        if (eventType is not null)
            qb.Add("event_type = $N", eventType);
        if (repoFullName is not null)
            qb.Add("repo_full_name = $N", repoFullName);

        return await ReadManyAsync(
            $"{GitHubEventSelectSql} {qb.WhereClause} ORDER BY created_at DESC LIMIT {qb.NextParam}",
            cmd =>
            {
                qb.ApplyTo(cmd);
                cmd.Parameters.Add(new DuckDBParameter { Value = limit });
            },
            MapGitHubEvent, ct).ConfigureAwait(false);
    }

    private static GitHubEventRecord MapGitHubEvent(DbDataReader reader) =>
        new()
        {
            EventId = reader.GetString(0),
            EventType = reader.GetString(1),
            Action = reader.Col(2).AsString,
            RepoFullName = reader.GetString(3),
            Sender = reader.Col(4).AsString,
            PrNumber = reader.Col(5).AsInt32,
            PrUrl = reader.Col(6).AsString,
            Ref = reader.Col(7).AsString,
            PayloadJson = reader.Col(8).AsString,
            CreatedAt = reader.Col(9).AsDateTime
        };
}
