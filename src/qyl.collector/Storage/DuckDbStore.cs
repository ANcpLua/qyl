namespace qyl.collector.Storage;

/// <summary>
///     DuckDB storage with separated read/write paths for optimal concurrency.
///     - Single writer connection (channel-buffered, batched)
///     - Pooled read connections (parallel queries, bounded concurrency)
/// </summary>
public sealed class DuckDbStore : IAsyncDisposable
{
    // DuckDB.NET 1.4.3: Use positional parameters ($1, $2, ...) instead of named ($param_name).
    private const string InsertSpanSql = """
                                         INSERT INTO spans (
                                             trace_id, span_id, parent_span_id,
                                             name, kind, start_time, end_time, status_code, status_message,
                                             service_name, session_id,
                                             genai_provider, genai_request_model,
                                             genai_input_tokens, genai_output_tokens,
                                             cost_usd, eval_score, eval_reason, attributes, events
                                         ) VALUES (
                                             $1, $2, $3,
                                             $4, $5, $6, $7, $8, $9,
                                             $10, $11,
                                             $12, $13,
                                             $14, $15,
                                             $16, $17, $18, $19, $20
                                         )
                                         ON CONFLICT (trace_id, span_id) DO UPDATE SET
                                             end_time = EXCLUDED.end_time,
                                             status_code = EXCLUDED.status_code,
                                             status_message = EXCLUDED.status_message,
                                             genai_input_tokens = EXCLUDED.genai_input_tokens,
                                             genai_output_tokens = EXCLUDED.genai_output_tokens,
                                             cost_usd = EXCLUDED.cost_usd,
                                             eval_score = EXCLUDED.eval_score,
                                             eval_reason = EXCLUDED.eval_reason,
                                             attributes = EXCLUDED.attributes,
                                             events = EXCLUDED.events
                                         """;

    private const string Schema = """
                                  CREATE TABLE IF NOT EXISTS sessions (
                                      session_id VARCHAR PRIMARY KEY,
                                      name VARCHAR,
                                      user_id VARCHAR,
                                      started_at TIMESTAMPTZ NOT NULL,
                                      ended_at TIMESTAMPTZ,
                                      metadata JSON
                                  );

                                  CREATE TABLE IF NOT EXISTS spans (
                                      trace_id VARCHAR NOT NULL,
                                      span_id VARCHAR NOT NULL,
                                      parent_span_id VARCHAR,

                                      name VARCHAR NOT NULL,
                                      kind VARCHAR,
                                      start_time TIMESTAMPTZ NOT NULL,
                                      end_time TIMESTAMPTZ NOT NULL,
                                      duration_ms DOUBLE GENERATED ALWAYS AS (
                                          EXTRACT(EPOCH FROM (end_time - start_time)) * 1000
                                      ),
                                      status_code INT,
                                      status_message VARCHAR,

                                      service_name VARCHAR,
                                      session_id VARCHAR,

                                      genai_provider VARCHAR,
                                      genai_request_model VARCHAR,
                                      genai_response_model VARCHAR,
                                      genai_operation VARCHAR,
                                      genai_input_tokens BIGINT,
                                      genai_output_tokens BIGINT,

                                      cost_usd DECIMAL(10,6),
                                      eval_score FLOAT,
                                      eval_reason VARCHAR,

                                      attributes JSON,
                                      events JSON,

                                      PRIMARY KEY (trace_id, span_id)
                                  );

                                  CREATE TABLE IF NOT EXISTS logs (
                                      log_id VARCHAR PRIMARY KEY,
                                      trace_id VARCHAR,
                                      span_id VARCHAR,
                                      session_id VARCHAR,

                                      time_unix_nano BIGINT NOT NULL,
                                      observed_time_unix_nano BIGINT,
                                      severity_number INT NOT NULL,
                                      severity_text VARCHAR,
                                      body VARCHAR,

                                      service_name VARCHAR,
                                      attributes_json VARCHAR,
                                      resource_json VARCHAR,

                                      created_at TIMESTAMPTZ DEFAULT now()
                                  );

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

                                  CREATE INDEX IF NOT EXISTS idx_spans_time ON spans (start_time);
                                  CREATE INDEX IF NOT EXISTS idx_spans_session ON spans (session_id);
                                  CREATE INDEX IF NOT EXISTS idx_spans_provider ON spans (genai_provider);
                                  CREATE INDEX IF NOT EXISTS idx_spans_service ON spans (service_name);
                                  CREATE INDEX IF NOT EXISTS idx_logs_time ON logs (time_unix_nano);
                                  CREATE INDEX IF NOT EXISTS idx_logs_session ON logs (session_id);
                                  CREATE INDEX IF NOT EXISTS idx_logs_trace ON logs (trace_id);
                                  CREATE INDEX IF NOT EXISTS idx_logs_severity ON logs (severity_number);
                                  CREATE INDEX IF NOT EXISTS idx_logs_service ON logs (service_name);
                                  """;

    private const string SelectColumns = """
                                         trace_id, span_id, parent_span_id,
                                         name, kind, start_time, end_time, status_code, status_message,
                                         service_name, session_id,
                                         genai_provider, genai_request_model,
                                         genai_input_tokens, genai_output_tokens,
                                         cost_usd, eval_score, eval_reason, attributes, events
                                         """;

    private readonly CancellationTokenSource _cts = new();
    private readonly Counter<long> _droppedJobs;
    private readonly Counter<long> _droppedSpans;
    private readonly bool _isInMemory;
    private readonly Channel<WriteJob> _jobs;

    private readonly Meter _meter = new("qyl.collector.storage", "1.0.0");

    private readonly SemaphoreSlim _readGate;
    private readonly ReadConnectionPolicy _readPolicy;

    private readonly DefaultObjectPool<DuckDBConnection> _readPool;
    private readonly Task _writerTask;

    private int _disposed;

    public DuckDbStore(
        string databasePath = "qyl.duckdb",
        int jobQueueCapacity = 1000,
        int maxConcurrentReads = 8,
        int maxRetainedReadConnections = 16)
    {
        _isInMemory = databasePath == ":memory:";
        Connection = new DuckDBConnection($"DataSource={databasePath}");
        Connection.Open();
        InitializeSchema(Connection);

        _droppedJobs = _meter.CreateCounter<long>("qyl.duckdb.dropped_jobs_total");
        _droppedSpans = _meter.CreateCounter<long>("qyl.duckdb.dropped_spans_total");

        _jobs = Channel.CreateBounded<WriteJob>(new BoundedChannelOptions(jobQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false
        });

        _readPolicy = new ReadConnectionPolicy(databasePath);
        _readPool = new DefaultObjectPool<DuckDBConnection>(_readPolicy, maxRetainedReadConnections);
        _readGate = new SemaphoreSlim(maxConcurrentReads, maxConcurrentReads);

        _writerTask = Task.Run(WriterLoopAsync);
    }

    /// <summary>
    ///     Exposes the write connection for legacy compatibility (SessionQueryService).
    ///     NOTE: For new code, use RentReadConnectionAsync for read queries.
    /// </summary>
    public DuckDBConnection Connection { get; }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _jobs.Writer.TryComplete();

        // Graceful shutdown: 3s to drain, then cancel
        try
        {
            await _writerTask.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            try
            {
                await _writerTask.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
            catch
            {
                // Best effort - proceed with cleanup
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }

        Connection.Dispose();
        _cts.Dispose();
        _readGate.Dispose();
        _readPolicy.DisposeAll();
        _meter.Dispose();
    }

    /// <summary>
    ///     Provides direct read access for SessionQueryService.
    ///     Returns a leased connection that must be disposed after use.
    /// </summary>
    public async ValueTask<IAsyncDisposable> GetReadConnectionAsync(CancellationToken ct = default) =>
        await RentReadAsync(ct).ConfigureAwait(false);

    /// <summary>
    ///     Enqueues a batch for async writing. With DropOldest, TryWrite always succeeds
    ///     but may drop the oldest queued item (whose OnAborted will be called by WriterLoop).
    /// </summary>
    public ValueTask EnqueueAsync(SpanBatch batch, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        if (batch.Spans.Count == 0) return ValueTask.CompletedTask;

        var job = new FireAndForgetJob(
            batch.Spans.Count,
            (con, token) => WriteBatchInternalAsync(con, batch, token),
            spanCount =>
            {
                _droppedJobs.Add(1);
                _droppedSpans.Add(spanCount);
            });

        // DropOldest: Always succeeds, drops oldest if full (acceptable for telemetry)
        _jobs.Writer.TryWrite(job);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Writes a batch directly (for testing). Bypasses the queue and writes synchronously.
    /// </summary>
    public async Task WriteBatchAsync(SpanBatch batch, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (batch.Spans.Count == 0) return;

        // Write directly to the connection (bypasses background queue)
        await WriteBatchInternalAsync(Connection, batch, ct).ConfigureAwait(false);
    }

    public async Task<int> ArchiveToParquetAsync(
        string outputDirectory,
        TimeSpan olderThan,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (ct.IsCancellationRequested) return await Task.FromCanceled<int>(ct).ConfigureAwait(false);

        var job = new WriteJob<int>((con, token) =>
            ArchiveInternalAsync(con, outputDirectory, olderThan, token));

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SpanStorageRow>> GetSpansBySessionAsync(string sessionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        // DuckDB.NET 1.4.3 BUG: = operator doesn't work correctly for session_id column
        // Even with identical hex values, WHERE session_id = 'value' returns 0 rows
        // But WHERE session_id LIKE '%value%' works. Using this as workaround.
        // Escape LIKE special characters and SQL injection characters
        var escapedSessionId = sessionId
            .Replace("'", "''")
            .Replace("%", @"\%")
            .Replace("_", @"\_");

        var spans = new List<SpanStorageRow>();
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT {SelectColumns}
                           FROM spans
                           WHERE session_id LIKE '%{escapedSessionId}%' ESCAPE '\'
                           ORDER BY start_time ASC
                           """;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            spans.Add(MapSpan(reader));

        return spans;
    }

    public async Task<IReadOnlyList<SpanStorageRow>> GetTraceAsync(string traceId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var spans = new List<SpanStorageRow>();
        await using var cmd = lease.Connection.CreateCommand();
        // DuckDB.NET 1.4.3: Use positional parameters ($1) instead of named ($trace_id)
        cmd.CommandText = $"""
                           SELECT {SelectColumns}
                           FROM spans
                           WHERE trace_id = $1
                           ORDER BY start_time ASC
                           """;
        cmd.Parameters.Add(new DuckDBParameter { Value = traceId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            spans.Add(MapSpan(reader));

        return spans;
    }

    public async Task<IReadOnlyList<SpanStorageRow>> GetSpansAsync(
        string? sessionId = null,
        string? providerName = null,
        DateTime? startAfter = null,
        DateTime? startBefore = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var spans = new List<SpanStorageRow>();
        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        // DuckDB.NET 1.4.3: Use positional parameters ($1, $2, ...) instead of named
        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add($"session_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = sessionId });
        }

        if (!string.IsNullOrEmpty(providerName))
        {
            conditions.Add($"genai_provider = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = providerName });
        }

        if (startAfter.HasValue)
        {
            conditions.Add($"start_time >= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = startAfter.Value });
        }

        if (startBefore.HasValue)
        {
            conditions.Add($"start_time <= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = startBefore.Value });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT {SelectColumns}
                           FROM spans
                           {whereClause}
                           ORDER BY start_time DESC
                           LIMIT ${paramIndex}
                           """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            spans.Add(MapSpan(reader));

        return spans;
    }

    public async Task<IReadOnlyList<SpanStorageRow>> QueryParquetAsync(
        string parquetPath,
        string? sessionId = null,
        string? traceId = null,
        DateTime? startAfter = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var spans = new List<SpanStorageRow>();
        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        // DuckDB.NET 1.4.3: Use positional parameters ($1, $2, ...) instead of named
        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add($"session_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = sessionId });
        }

        if (!string.IsNullOrEmpty(traceId))
        {
            conditions.Add($"trace_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = traceId });
        }

        if (startAfter.HasValue)
        {
            conditions.Add($"start_time >= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = startAfter.Value });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        var full = Path.GetFullPath(parquetPath);
        ValidateDuckDbSqlPath(full);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"SELECT {SelectColumns} FROM read_parquet('{full}') {whereClause}";
        cmd.Parameters.AddRange(parameters);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            spans.Add(MapSpan(reader));

        return spans;
    }

    public async Task<StorageStats> GetStorageStatsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              (SELECT COUNT(*) FROM spans) as span_count,
                              (SELECT COUNT(*) FROM sessions) as session_count,
                              (SELECT COUNT(*) FROM feedback) as feedback_count,
                              (SELECT MIN(start_time) FROM spans) as oldest_span,
                              (SELECT MAX(start_time) FROM spans) as newest_span
                          """;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new StorageStats
            {
                SpanCount = reader.Col(0).GetInt64(0),
                SessionCount = reader.Col(1).GetInt64(0),
                FeedbackCount = reader.Col(2).GetInt64(0),
                OldestSpan = reader.Col(3).AsDateTime,
                NewestSpan = reader.Col(4).AsDateTime
            };
        }

        return new StorageStats();
    }

    public async Task<GenAiStats> GetGenAiStatsAsync(
        string? sessionId = null,
        DateTime? startAfter = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var conditions = new List<string> { "genai_provider IS NOT NULL" };
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        // DuckDB.NET 1.4.3: Use positional parameters ($1, $2, ...) instead of named
        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add($"session_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = sessionId });
        }

        if (startAfter.HasValue)
        {
            conditions.Add($"start_time >= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = startAfter.Value });
        }

        var whereClause = string.Join(" AND ", conditions);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT
                               COUNT(*) as request_count,
                               COALESCE(SUM(genai_input_tokens), 0) as total_input_tokens,
                               COALESCE(SUM(genai_output_tokens), 0) as total_output_tokens,
                               COALESCE(SUM(cost_usd), 0) as total_cost_usd,
                               AVG(CASE WHEN eval_score IS NOT NULL THEN eval_score END) as avg_eval_score
                           FROM spans
                           WHERE {whereClause}
                           """;

        cmd.Parameters.AddRange(parameters);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new GenAiStats
            {
                RequestCount = reader.Col(0).GetInt64(0),
                TotalInputTokens = reader.Col(1).GetInt64(0),
                TotalOutputTokens = reader.Col(2).GetInt64(0),
                TotalCostUsd = reader.Col(3).GetDecimal(0),
                AverageEvalScore = reader.Col(4).AsFloat
            };
        }

        return new GenAiStats();
    }

    #region Logs Methods

    private const string InsertLogSql = """
                                        INSERT INTO logs (
                                            log_id, trace_id, span_id, session_id,
                                            time_unix_nano, observed_time_unix_nano,
                                            severity_number, severity_text, body,
                                            service_name, attributes_json, resource_json
                                        ) VALUES (
                                            $1, $2, $3, $4,
                                            $5, $6,
                                            $7, $8, $9,
                                            $10, $11, $12
                                        )
                                        """;

    public async Task InsertLogsAsync(IReadOnlyList<LogStorageRow> logs, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (logs.Count == 0) return;

        await using var tx = await Connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        foreach (var log in logs)
        {
            await using var cmd = Connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = InsertLogSql;

            cmd.Parameters.Add(new DuckDBParameter { Value = log.LogId });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.TraceId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.SpanId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.SessionId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.TimeUnixNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.ObservedTimeUnixNano ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.SeverityNumber });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.SeverityText ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.Body ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.ServiceName ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.AttributesJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.ResourceJson ?? (object)DBNull.Value });

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LogStorageRow>> GetLogsAsync(
        string? sessionId = null,
        string? traceId = null,
        string? severityText = null,
        int? minSeverity = null,
        string? search = null,
        DateTime? after = null,
        DateTime? before = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var logs = new List<LogStorageRow>();
        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add($"session_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = sessionId });
        }

        if (!string.IsNullOrEmpty(traceId))
        {
            conditions.Add($"trace_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = traceId });
        }

        if (!string.IsNullOrEmpty(severityText))
        {
            conditions.Add($"severity_text = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = severityText });
        }

        if (minSeverity.HasValue)
        {
            conditions.Add($"severity_number >= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = minSeverity.Value });
        }

        if (!string.IsNullOrEmpty(search))
        {
            conditions.Add($"body LIKE ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = $"%{search}%" });
        }

        if (after.HasValue)
        {
            var nanos = new DateTimeOffset(after.Value, TimeSpan.Zero).ToUnixTimeMilliseconds() * 1_000_000;
            conditions.Add($"time_unix_nano >= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = nanos });
        }

        if (before.HasValue)
        {
            var nanos = new DateTimeOffset(before.Value, TimeSpan.Zero).ToUnixTimeMilliseconds() * 1_000_000;
            conditions.Add($"time_unix_nano <= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = nanos });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT log_id, trace_id, span_id, session_id,
                                  time_unix_nano, observed_time_unix_nano,
                                  severity_number, severity_text, body,
                                  service_name, attributes_json, resource_json
                           FROM logs
                           {whereClause}
                           ORDER BY time_unix_nano DESC
                           LIMIT ${paramIndex}
                           """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            logs.Add(MapLog(reader));

        return logs;
    }

    private static LogStorageRow MapLog(DbDataReader reader) =>
        new()
        {
            LogId = reader.GetString(0),
            TraceId = reader.Col(1).AsString,
            SpanId = reader.Col(2).AsString,
            SessionId = reader.Col(3).AsString,
            TimeUnixNano = reader.Col(4).GetInt64(0),
            ObservedTimeUnixNano = reader.Col(5).AsInt64,
            SeverityNumber = reader.Col(6).GetInt32(0),
            SeverityText = reader.Col(7).AsString,
            Body = reader.Col(8).AsString,
            ServiceName = reader.Col(9).AsString,
            AttributesJson = reader.Col(10).AsString,
            ResourceJson = reader.Col(11).AsString
        };

    #endregion

    private async Task WriterLoopAsync()
    {
        try
        {
            await foreach (var job in _jobs.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    await job.ExecuteAsync(Connection, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    job.OnAborted(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        finally
        {
            // Drain remaining jobs on shutdown
            while (_jobs.Reader.TryRead(out var leftover))
                leftover.OnAborted(new OperationCanceledException("Store is shutting down."));
        }
    }

    private async ValueTask<ReadLease> RentReadAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        await _readGate.WaitAsync(ct).ConfigureAwait(false);

        // In-memory databases cannot share connections, so use the main connection
        if (_isInMemory)
            return new ReadLease(this, Connection, true);

        DuckDBConnection con;
        try
        {
            con = _readPool.Get();
            if (con.State != ConnectionState.Open)
            {
                con.Dispose();
                con = _readPolicy.Create();
            }
        }
        catch
        {
            _readGate.Release();
            throw;
        }

        return new ReadLease(this, con);
    }

    private void ReturnRead(DuckDBConnection con)
    {
        try
        {
            if (con.State == ConnectionState.Open)
                _readPool.Return(con);
            else
                con.Dispose();
        }
        finally
        {
            _readGate.Release();
        }
    }

    private static void InitializeSchema(DuckDBConnection con)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = Schema;
        cmd.ExecuteNonQuery();
    }

    private static async ValueTask WriteBatchInternalAsync(DuckDBConnection con, SpanBatch batch, CancellationToken ct)
    {
        if (batch.Spans.Count == 0) return;

        await using var tx = await con.BeginTransactionAsync(ct).ConfigureAwait(false);

        foreach (var span in batch.Spans)
        {
            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = InsertSpanSql;

            // DuckDB.NET 1.4.3: Use positional parameters (order must match ? placeholders in InsertSpanSql)
            cmd.Parameters.Add(new DuckDBParameter { Value = span.TraceId });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.SpanId });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.ParentSpanId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.Name });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.Kind ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.StartTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.EndTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.StatusCode ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.StatusMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.ServiceName ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.SessionId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.ProviderName ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.RequestModel ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.TokensIn ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.TokensOut ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.CostUsd ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.EvalScore ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.EvalReason ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.Attributes ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.Events ?? (object)DBNull.Value });

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static async ValueTask<int> ArchiveInternalAsync(
        DuckDBConnection con,
        string outputDirectory,
        TimeSpan olderThan,
        CancellationToken ct)
    {
        Directory.CreateDirectory(outputDirectory);

        var now = TimeProvider.System.GetUtcNow();
        var cutoff = now.UtcDateTime - olderThan;
        var timestamp = now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        // DuckDB.NET 1.4.3: Use positional parameters ($1)
        await using var countCmd = con.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM spans WHERE start_time < $1";
        countCmd.Parameters.Add(new DuckDBParameter { Value = cutoff });
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
        if (count == 0) return 0;

        var finalPath = Path.GetFullPath(Path.Combine(outputDirectory, $"spans_{timestamp}.parquet"));
        var tempPath = finalPath + ".tmp";

        ValidateDuckDbSqlPath(finalPath);
        ValidateDuckDbSqlPath(tempPath);

        await using (var exportCmd = con.CreateCommand())
        {
            exportCmd.CommandText = $"""
                                     COPY (SELECT * FROM spans WHERE start_time < $1)
                                     TO '{tempPath}'
                                     (FORMAT PARQUET, COMPRESSION ZSTD, ROW_GROUP_SIZE 100000)
                                     """;
            exportCmd.Parameters.Add(new DuckDBParameter { Value = cutoff });
            await exportCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        File.Move(tempPath, finalPath, true);

        await using var tx = await con.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var deleteCmd = con.CreateCommand();
        deleteCmd.Transaction = tx;
        deleteCmd.CommandText = "DELETE FROM spans WHERE start_time < $1";
        deleteCmd.Parameters.Add(new DuckDBParameter { Value = cutoff });
        await deleteCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return count;
    }

    private static void ValidateDuckDbSqlPath(string fullPath)
    {
        if (fullPath.Contains('\'') || fullPath.Contains(';') || fullPath.Contains("--") ||
            fullPath.Contains('\n') || fullPath.Contains('\r') || fullPath.Contains('\0'))
            throw new ArgumentException("Invalid path characters detected", nameof(fullPath));
    }

    private static SpanStorageRow MapSpan(DbDataReader reader) =>
        new()
        {
            // Required fields
            TraceId = reader.GetString(0),
            SpanId = reader.GetString(1),

            // Nullable fields - fluent access
            ParentSpanId = reader.Col(2).AsString,
            Name = reader.GetString(3),
            Kind = reader.Col(4).AsString,

            // DateTime with fallback (OTLP always has valid timestamps)
            StartTime = reader.Col(5).GetDateTime(default),
            EndTime = reader.Col(6).GetDateTime(default),
            StatusCode = reader.Col(7).AsInt32,
            StatusMessage = reader.Col(8).AsString,

            // OTel 1.38 attributes
            ServiceName = reader.Col(9).AsString,
            SessionId = reader.Col(10).AsString,
            ProviderName = reader.Col(11).AsString,
            RequestModel = reader.Col(12).AsString,
            TokensIn = reader.Col(13).AsInt64,
            TokensOut = reader.Col(14).AsInt64,

            // qyl extensions
            CostUsd = reader.Col(15).AsDecimal,
            EvalScore = reader.Col(16).AsFloat,
            EvalReason = reader.Col(17).AsString,

            // Flexible storage
            Attributes = reader.Col(18).AsString,
            Events = reader.Col(19).AsString
        };

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    #region Nested Types

    /// <summary>
    ///     RAII-style lease for pooled read connections.
    /// </summary>
    public readonly struct ReadLease : IAsyncDisposable, IDisposable
    {
        private readonly DuckDbStore _store;
        private readonly bool _isShared;
        public DuckDBConnection Connection { get; }

        internal ReadLease(DuckDbStore store, DuckDBConnection con, bool isShared = false)
        {
            _store = store;
            Connection = con;
            _isShared = isShared;
        }

        public void Dispose()
        {
            // Don't return shared connections (in-memory mode)
            if (_isShared)
            {
                _store._readGate.Release();
                return;
            }

            _store.ReturnRead(Connection);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private abstract class WriteJob
    {
        public abstract ValueTask ExecuteAsync(DuckDBConnection con, CancellationToken ct);

        public virtual void OnAborted(Exception error)
        {
        }
    }

    private sealed class FireAndForgetJob : WriteJob
    {
        private readonly Func<DuckDBConnection, CancellationToken, ValueTask> _action;
        private readonly Action<int>? _onDropped;
        private readonly int _spanCount;

        public FireAndForgetJob(
            int spanCount,
            Func<DuckDBConnection, CancellationToken, ValueTask> action,
            Action<int>? onDropped = null)
        {
            _spanCount = spanCount;
            _action = action;
            _onDropped = onDropped;
        }

        public override ValueTask ExecuteAsync(DuckDBConnection con, CancellationToken ct) => _action(con, ct);

        public override void OnAborted(Exception error) => _onDropped?.Invoke(_spanCount);
    }

    private sealed class WriteJob<TResult> : WriteJob
    {
        private readonly Func<DuckDBConnection, CancellationToken, ValueTask<TResult>> _action;

        private readonly TaskCompletionSource<TResult> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WriteJob(Func<DuckDBConnection, CancellationToken, ValueTask<TResult>> action) => _action = action;

        public Task<TResult> Task => _tcs.Task;

        public override async ValueTask ExecuteAsync(DuckDBConnection con, CancellationToken ct)
        {
            try
            {
                var result = await _action(con, ct).ConfigureAwait(false);
                _tcs.TrySetResult(result);
            }
            catch (OperationCanceledException oce)
            {
                _tcs.TrySetCanceled(oce.CancellationToken);
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }

        public override void OnAborted(Exception error)
        {
            if (error is OperationCanceledException oce)
                _tcs.TrySetCanceled(oce.CancellationToken);
            else
                _tcs.TrySetException(error);
        }
    }

    private sealed class ReadConnectionPolicy : PooledObjectPolicy<DuckDBConnection>
    {
        private readonly ConcurrentBag<DuckDBConnection> _created = new();
        private readonly string _path;

        public ReadConnectionPolicy(string path) => _path = path;

        public override DuckDBConnection Create()
        {
            // In-memory databases cannot use READ_ONLY mode
            var connString = _path == ":memory:"
                ? $"DataSource={_path}"
                : $"DataSource={_path};ACCESS_MODE=READ_ONLY";

            var con = new DuckDBConnection(connString);
            con.Open();
            _created.Add(con);
            return con;
        }

        public override bool Return(DuckDBConnection obj) => obj.State == ConnectionState.Open;

        public void DisposeAll()
        {
            while (_created.TryTake(out var con))
            {
                try
                {
                    con.Dispose();
                }
                catch
                {
                    /* Best effort cleanup */
                }
            }
        }
    }

    #endregion
}
