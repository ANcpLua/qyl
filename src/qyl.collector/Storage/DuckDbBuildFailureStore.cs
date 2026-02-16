namespace qyl.collector.Storage;

public sealed class DuckDbBuildFailureStore : IBuildFailureStore
{
    private readonly string _connectionString;
    private readonly int _maxRetainedFailures;

    public DuckDbBuildFailureStore(string databasePath, int maxRetainedFailures)
    {
        _connectionString = $"DataSource={databasePath}";
        _maxRetainedFailures = Math.Max(1, maxRetainedFailures);
    }

    public async Task<string> InsertAsync(BuildFailureRecord record, CancellationToken ct = default)
    {
        var id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("N") : record.Id;

        await using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                              INSERT INTO build_failures (
                                  id, timestamp, target, exit_code, binlog_path, error_summary,
                                  property_issues_json, env_reads_json, call_stack_json, duration_ms)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
                              """;

            cmd.Parameters.Add(new DuckDBParameter { Value = id });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Timestamp.UtcDateTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.Target });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ExitCode });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.BinlogPath ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.ErrorSummary ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.PropertyIssuesJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.EnvReadsJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.CallStackJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = record.DurationMs ?? (object)DBNull.Value });
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await CleanupOldestAsync(connection, ct).ConfigureAwait(false);
        return id;
    }

    public async Task<BuildFailureRecord?> GetAsync(string id, CancellationToken ct = default)
    {
        await using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, timestamp, target, exit_code, binlog_path, error_summary,
                                 property_issues_json, env_reads_json, call_stack_json, duration_ms, created_at
                          FROM build_failures
                          WHERE id = $1
                          LIMIT 1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = id });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<BuildFailureRecord>> ListAsync(int limit = 10, CancellationToken ct = default)
    {
        await using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, timestamp, target, exit_code, binlog_path, error_summary,
                                 property_issues_json, env_reads_json, call_stack_json, duration_ms, created_at
                          FROM build_failures
                          ORDER BY timestamp DESC
                          LIMIT $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 500) });

        return await ReadManyAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BuildFailureRecord>> SearchAsync(string pattern, int limit = 50,
        CancellationToken ct = default)
    {
        await using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, timestamp, target, exit_code, binlog_path, error_summary,
                                 property_issues_json, env_reads_json, call_stack_json, duration_ms, created_at
                          FROM build_failures
                          WHERE error_summary LIKE $1
                             OR CAST(property_issues_json AS VARCHAR) LIKE $1
                             OR CAST(call_stack_json AS VARCHAR) LIKE $1
                          ORDER BY timestamp DESC
                          LIMIT $2
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = $"%{pattern}%" });
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 500) });

        return await ReadManyAsync(cmd, ct).ConfigureAwait(false);
    }

    private async Task CleanupOldestAsync(DuckDBConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          DELETE FROM build_failures
                          WHERE id IN (
                              SELECT id
                              FROM build_failures
                              ORDER BY timestamp DESC
                              OFFSET $1
                          )
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = _maxRetainedFailures });
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<BuildFailureRecord>> ReadManyAsync(DuckDBCommand cmd, CancellationToken ct)
    {
        var rows = new List<BuildFailureRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(Map(reader));

        return rows;
    }

    private static BuildFailureRecord Map(IDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            Timestamp = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
            Target = reader.GetString(2),
            ExitCode = reader.GetInt32(3),
            BinlogPath = reader.Col(4).AsString,
            ErrorSummary = reader.Col(5).AsString,
            PropertyIssuesJson = reader.Col(6).AsString,
            EnvReadsJson = reader.Col(7).AsString,
            CallStackJson = reader.Col(8).AsString,
            DurationMs = reader.Col(9).AsInt32,
            CreatedAt = reader.Col(10).AsDateTimeOffset
        };
}
