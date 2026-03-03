using qyl.collector.Autofix;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with GitHub webhook event
///     storage operations.
/// </summary>
public sealed partial class DuckDbStore
{
    /// <summary>
    ///     Inserts a new GitHub event record via the channel-buffered write path.
    /// </summary>
    public async Task InsertGitHubEventAsync(GitHubEventRecord @event, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
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
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets GitHub events with optional filtering by event type and repository.
    /// </summary>
    public async Task<IReadOnlyList<GitHubEventRecord>> GetGitHubEventsAsync(
        int limit = 50, string? eventType = null, string? repoFullName = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        int paramIndex = 1;
        string sql = """
                     SELECT event_id, event_type, action, repo_full_name, sender,
                            pr_number, pr_url, ref, payload_json, created_at
                     FROM github_events
                     WHERE 1=1
                     """;

        if (eventType is not null)
        {
            sql += $" AND event_type = ${paramIndex}";
            cmd.Parameters.Add(new DuckDBParameter { Value = eventType });
            paramIndex++;
        }

        if (repoFullName is not null)
        {
            sql += $" AND repo_full_name = ${paramIndex}";
            cmd.Parameters.Add(new DuckDBParameter { Value = repoFullName });
            paramIndex++;
        }

        sql += $" ORDER BY created_at DESC LIMIT ${paramIndex}";
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        cmd.CommandText = sql;

        var results = new List<GitHubEventRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapGitHubEvent(reader));

        return results;
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
