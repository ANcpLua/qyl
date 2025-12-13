using System.Data;
using System.Threading.Channels;
using DuckDB.NET.Data;

namespace qyl.collector.Storage;

public sealed class DuckDbStore : IAsyncDisposable
{
    private const string _insertSpanSql = """
                                          INSERT INTO spans (
                                              trace_id, span_id, parent_span_id, session_id,
                                              name, kind, start_time, end_time, status_code, status_message,
                                              provider_name, request_model, tokens_in, tokens_out,
                                              cost_usd, eval_score, eval_reason, attributes, events
                                          ) VALUES (
                                              $trace_id, $span_id, $parent_span_id, $session_id,
                                              $name, $kind, $start_time, $end_time, $status_code, $status_message,
                                              $provider_name, $request_model, $tokens_in, $tokens_out,
                                              $cost_usd, $eval_score, $eval_reason, $attributes, $events
                                          )
                                          ON CONFLICT (trace_id, span_id) DO UPDATE SET
                                              end_time = EXCLUDED.end_time,
                                              status_code = EXCLUDED.status_code,
                                              status_message = EXCLUDED.status_message,
                                              tokens_in = EXCLUDED.tokens_in,
                                              tokens_out = EXCLUDED.tokens_out,
                                              cost_usd = EXCLUDED.cost_usd,
                                              eval_score = EXCLUDED.eval_score,
                                              eval_reason = EXCLUDED.eval_reason,
                                              attributes = EXCLUDED.attributes,
                                              events = EXCLUDED.events
                                          """;

    private const string _schema = """
                                   -- sessions table
                                   CREATE TABLE IF NOT EXISTS sessions (
                                       session_id VARCHAR PRIMARY KEY,
                                       name VARCHAR,
                                       user_id VARCHAR,
                                       started_at TIMESTAMPTZ NOT NULL,
                                       ended_at TIMESTAMPTZ,
                                       metadata JSON
                                   );

                                   -- spans table (hot storage)
                                   CREATE TABLE IF NOT EXISTS spans (
                                       trace_id VARCHAR NOT NULL,
                                       span_id VARCHAR NOT NULL,
                                       parent_span_id VARCHAR,
                                       session_id VARCHAR,

                                       name VARCHAR NOT NULL,
                                       kind VARCHAR,
                                       start_time TIMESTAMPTZ NOT NULL,
                                       end_time TIMESTAMPTZ NOT NULL,
                                       duration_ms DOUBLE GENERATED ALWAYS AS (
                                           EXTRACT(EPOCH FROM (end_time - start_time)) * 1000
                                       ),
                                       status_code INT,
                                       status_message VARCHAR,

                                       -- GenAI semantic conventions (v1.38)
                                       provider_name VARCHAR,
                                       request_model VARCHAR,
                                       tokens_in INT,
                                       tokens_out INT,

                                       -- qyl. extensions
                                       cost_usd DECIMAL(10,6),
                                       eval_score FLOAT,
                                       eval_reason VARCHAR,

                                       -- Flexible storage
                                       attributes JSON,
                                       events JSON,

                                       PRIMARY KEY (trace_id, span_id)
                                   );

                                   -- feedback table
                                   CREATE TABLE IF NOT EXISTS feedback (
                                       feedback_id VARCHAR PRIMARY KEY,
                                       session_id VARCHAR NOT NULL,
                                       span_id VARCHAR,

                                       type VARCHAR NOT NULL,
                                       value VARCHAR,
                                       comment VARCHAR,
                                       created_at TIMESTAMPTZ NOT NULL,

                                       metadata JSON
                                   );

                                   -- Indexes
                                   CREATE INDEX IF NOT EXISTS idx_spans_time ON spans (start_time);
                                   CREATE INDEX IF NOT EXISTS idx_spans_session ON spans (session_id);
                                   CREATE INDEX IF NOT EXISTS idx_spans_provider ON spans (provider_name) WHERE provider_name IS NOT NULL;
                                   CREATE INDEX IF NOT EXISTS idx_feedback_session ON feedback (session_id);
                                   """;

    private readonly CancellationTokenSource _cts;
    private readonly Channel<SpanBatch> _writeChannel;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Task _writerTask;

    public DuckDbStore(string databasePath = "qyl.duckdb")
    {
        Connection = new DuckDBConnection($"Data Source={databasePath}");
        Connection.Open();

        InitializeSchema();

        _cts = new CancellationTokenSource();

        _writeChannel = Channel.CreateBounded<SpanBatch>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _writerTask = Task.Run(WriteLoopAsync);
    }

    /// <summary>
    ///     Exposes the connection for query services that need direct DuckDB access.
    /// </summary>
    public DuckDBConnection Connection { get; }

    public async ValueTask DisposeAsync()
    {
        _writeChannel.Writer.Complete();
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
#pragma warning disable VSTHRD003 // Awaiting task from field is safe in DisposeAsync
            await _writerTask.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        }
        catch (OperationCanceledException)
        {
        }

        Connection.Dispose();
        _cts.Dispose();
        _writeLock.Dispose();
    }

    private void InitializeSchema()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = _schema;
        cmd.ExecuteNonQuery();
    }

    public ValueTask EnqueueAsync(SpanBatch batch, CancellationToken ct = default)
    {
        return _writeChannel.Writer.WriteAsync(batch, ct);
    }

    private async Task WriteLoopAsync()
    {
        await foreach (var batch in _writeChannel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            try
            {
                await WriteBatchInternalAsync(batch, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[qyl.collector] Write error: {ex.Message}");
            }
    }

    private async Task WriteBatchInternalAsync(SpanBatch batch, CancellationToken ct)
    {
        if (batch.Spans.Count == 0) return;

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var transaction = Connection.BeginTransaction();

            foreach (var span in batch.Spans)
            {
                using var cmd = Connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = _insertSpanSql;

                cmd.Parameters.Add(new DuckDBParameter("trace_id", span.TraceId));
                cmd.Parameters.Add(new DuckDBParameter("span_id", span.SpanId));
                cmd.Parameters.Add(new DuckDBParameter("parent_span_id", span.ParentSpanId ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("session_id", span.SessionId ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("name", span.Name));
                cmd.Parameters.Add(new DuckDBParameter("kind", span.Kind ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("start_time", span.StartTime));
                cmd.Parameters.Add(new DuckDBParameter("end_time", span.EndTime));
                cmd.Parameters.Add(new DuckDBParameter("status_code", span.StatusCode ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("status_message", span.StatusMessage ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("provider_name", span.ProviderName ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("request_model", span.RequestModel ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("tokens_in", span.TokensIn ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("tokens_out", span.TokensOut ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("cost_usd", span.CostUsd ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("eval_score", span.EvalScore ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("eval_reason", span.EvalReason ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("attributes", span.Attributes ?? (object)DBNull.Value));
                cmd.Parameters.Add(new DuckDBParameter("events", span.Events ?? (object)DBNull.Value));

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            transaction.Commit();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<SpanRecord>> GetSpansBySessionAsync(string sessionId,
        CancellationToken ct = default)
    {
        var spans = new List<SpanRecord>();

        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT trace_id, span_id, parent_span_id, session_id,
                                 name, kind, start_time, end_time, status_code, status_message,
                                 provider_name, request_model, tokens_in, tokens_out,
                                 cost_usd, eval_score, eval_reason, attributes, events
                          FROM spans
                          WHERE session_id = $session_id
                          ORDER BY start_time ASC
                          """;
        cmd.Parameters.Add(new DuckDBParameter("session_id", sessionId));

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) spans.Add(MapSpan(reader));

        return spans;
    }

    public async Task<IReadOnlyList<SpanRecord>> GetTraceAsync(string traceId, CancellationToken ct = default)
    {
        var spans = new List<SpanRecord>();

        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT trace_id, span_id, parent_span_id, session_id,
                                 name, kind, start_time, end_time, status_code, status_message,
                                 provider_name, request_model, tokens_in, tokens_out,
                                 cost_usd, eval_score, eval_reason, attributes, events
                          FROM spans
                          WHERE trace_id = $trace_id
                          ORDER BY start_time ASC
                          """;
        cmd.Parameters.Add(new DuckDBParameter("trace_id", traceId));

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) spans.Add(MapSpan(reader));

        return spans;
    }

    public async Task<IReadOnlyList<SpanRecord>> GetSpansAsync(
        string? sessionId = null,
        string? providerName = null,
        DateTime? startAfter = null,
        DateTime? startBefore = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        var spans = new List<SpanRecord>();
        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();

        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add("session_id = $session_id");
            parameters.Add(new DuckDBParameter("session_id", sessionId));
        }

        if (!string.IsNullOrEmpty(providerName))
        {
            conditions.Add("provider_name = $provider_name");
            parameters.Add(new DuckDBParameter("provider_name", providerName));
        }

        if (startAfter.HasValue)
        {
            conditions.Add("start_time >= $start_after");
            parameters.Add(new DuckDBParameter("start_after", startAfter.Value));
        }

        if (startBefore.HasValue)
        {
            conditions.Add("start_time <= $start_before");
            parameters.Add(new DuckDBParameter("start_before", startBefore.Value));
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        using var cmd = Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT trace_id, span_id, parent_span_id, session_id,
                                  name, kind, start_time, end_time, status_code, status_message,
                                  provider_name, request_model, tokens_in, tokens_out,
                                  cost_usd, eval_score, eval_reason, attributes, events
                           FROM spans
                           {whereClause}
                           ORDER BY start_time DESC
                           LIMIT $limit
                           """;

        foreach (var param in parameters) cmd.Parameters.Add(param);
        cmd.Parameters.Add(new DuckDBParameter("limit", limit));

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) spans.Add(MapSpan(reader));

        return spans;
    }

    private static SpanRecord MapSpan(IDataReader reader)
    {
        return new SpanRecord
        {
            TraceId = reader.GetString(0),
            SpanId = reader.GetString(1),
            ParentSpanId = reader.IsDBNull(2) ? null : reader.GetString(2),
            SessionId = reader.IsDBNull(3) ? null : reader.GetString(3),
            Name = reader.GetString(4),
            Kind = reader.IsDBNull(5) ? null : reader.GetString(5),
            StartTime = reader.GetDateTime(6),
            EndTime = reader.GetDateTime(7),
            StatusCode = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            StatusMessage = reader.IsDBNull(9) ? null : reader.GetString(9),
            ProviderName = reader.IsDBNull(10) ? null : reader.GetString(10),
            RequestModel = reader.IsDBNull(11) ? null : reader.GetString(11),
            TokensIn = reader.IsDBNull(12) ? null : reader.GetInt32(12),
            TokensOut = reader.IsDBNull(13) ? null : reader.GetInt32(13),
            CostUsd = reader.IsDBNull(14) ? null : reader.GetDecimal(14),
            EvalScore = reader.IsDBNull(15) ? null : (float)reader.GetDouble(15),
            EvalReason = reader.IsDBNull(16) ? null : reader.GetString(16),
            Attributes = reader.IsDBNull(17) ? null : reader.GetString(17),
            Events = reader.IsDBNull(18) ? null : reader.GetString(18)
        };
    }

    public async Task<int> ArchiveToParquetAsync(
        string outputDirectory,
        TimeSpan olderThan,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var now = TimeProvider.System.GetUtcNow();
        var cutoff = now.UtcDateTime - olderThan;
        var timestamp = now.ToString("yyyyMMdd_HHmmss");

        using var countCmd = Connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM spans WHERE start_time < $cutoff";
        countCmd.Parameters.Add(new DuckDBParameter("cutoff", cutoff));
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false));

        if (count == 0) return 0;

        var spansFile = Path.Combine(outputDirectory, $"spans_{timestamp}.parquet");
        using var exportCmd = Connection.CreateCommand();
        exportCmd.CommandText = $"""
                                 COPY (SELECT * FROM spans WHERE start_time < $cutoff)
                                 TO '{spansFile}'
                                 (FORMAT PARQUET, COMPRESSION ZSTD, ROW_GROUP_SIZE 100000)
                                 """;
        exportCmd.Parameters.Add(new DuckDBParameter("cutoff", cutoff));
        await exportCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        using var deleteCmd = Connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM spans WHERE start_time < $cutoff";
        deleteCmd.Parameters.Add(new DuckDBParameter("cutoff", cutoff));
        await deleteCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return count;
    }

    public async Task<IReadOnlyList<SpanRecord>> QueryParquetAsync(
        string parquetPath,
        string? sessionId = null,
        string? traceId = null,
        DateTime? startAfter = null,
        CancellationToken ct = default)
    {
        var spans = new List<SpanRecord>();
        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();

        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add("session_id = $session_id");
            parameters.Add(new DuckDBParameter("session_id", sessionId));
        }

        if (!string.IsNullOrEmpty(traceId))
        {
            conditions.Add("trace_id = $trace_id");
            parameters.Add(new DuckDBParameter("trace_id", traceId));
        }

        if (startAfter.HasValue)
        {
            conditions.Add("start_time >= $start_after");
            parameters.Add(new DuckDBParameter("start_after", startAfter.Value));
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        if (parquetPath.Contains('\'') || parquetPath.Contains(';') || parquetPath.Contains("--"))
            throw new ArgumentException("Invalid parquet path", nameof(parquetPath));

        using var cmd = Connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM read_parquet('{parquetPath}') {whereClause}";

        foreach (var param in parameters) cmd.Parameters.Add(param);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) spans.Add(MapSpan(reader));

        return spans;
    }

    public async Task<StorageStats> GetStorageStatsAsync(CancellationToken ct = default)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              (SELECT COUNT(*) FROM spans) as span_count,
                              (SELECT COUNT(*) FROM sessions) as session_count,
                              (SELECT COUNT(*) FROM feedback) as feedback_count,
                              (SELECT MIN(start_time) FROM spans) as oldest_span,
                              (SELECT MAX(start_time) FROM spans) as newest_span
                          """;

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return new StorageStats
            {
                SpanCount = reader.GetInt64(0),
                SessionCount = reader.GetInt64(1),
                FeedbackCount = reader.GetInt64(2),
                OldestSpan = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                NewestSpan = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
            };

        return new StorageStats();
    }

    public async Task<GenAiStats> GetGenAiStatsAsync(
        string? sessionId = null,
        DateTime? startAfter = null,
        CancellationToken ct = default)
    {
        var conditions = new List<string>
        {
            "provider_name IS NOT NULL"
        };
        var parameters = new List<DuckDBParameter>();

        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add("session_id = $session_id");
            parameters.Add(new DuckDBParameter("session_id", sessionId));
        }

        if (startAfter.HasValue)
        {
            conditions.Add("start_time >= $start_after");
            parameters.Add(new DuckDBParameter("start_after", startAfter.Value));
        }

        var whereClause = string.Join(" AND ", conditions);

        using var cmd = Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT
                               COUNT(*) as request_count,
                               COALESCE(SUM(tokens_in), 0) as total_input_tokens,
                               COALESCE(SUM(tokens_out), 0) as total_output_tokens,
                               COALESCE(SUM(cost_usd), 0) as total_cost_usd,
                               AVG(CASE WHEN eval_score IS NOT NULL THEN eval_score END) as avg_eval_score
                           FROM spans
                           WHERE {whereClause}
                           """;

        foreach (var param in parameters) cmd.Parameters.Add(param);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return new GenAiStats
            {
                RequestCount = reader.GetInt64(0),
                TotalInputTokens = reader.GetInt64(1),
                TotalOutputTokens = reader.GetInt64(2),
                TotalCostUsd = reader.GetDecimal(3),
                AverageEvalScore = reader.IsDBNull(4) ? null : (float)reader.GetDouble(4)
            };

        return new GenAiStats();
    }
}

public sealed record SpanBatch(IReadOnlyList<SpanRecord> Spans);

public sealed record StorageStats
{
    public long SpanCount { get; init; }
    public long SessionCount { get; init; }
    public long FeedbackCount { get; init; }
    public DateTime? OldestSpan { get; init; }
    public DateTime? NewestSpan { get; init; }
}

public sealed record GenAiStats
{
    public long RequestCount { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public decimal TotalCostUsd { get; init; }
    public float? AverageEvalScore { get; init; }
}

public sealed record SpanRecord
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? SessionId { get; init; }
    public required string Name { get; init; }
    public string? Kind { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public int? StatusCode { get; init; }
    public string? StatusMessage { get; init; }
    public string? ProviderName { get; init; }
    public string? RequestModel { get; init; }
    public int? TokensIn { get; init; }
    public int? TokensOut { get; init; }
    public decimal? CostUsd { get; init; }
    public float? EvalScore { get; init; }
    public string? EvalReason { get; init; }
    public string? Attributes { get; init; }
    public string? Events { get; init; }
}