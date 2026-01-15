namespace qyl.collector.Storage;

/// <summary>
///     DuckDB storage with separated read/write paths for optimal concurrency.
///     - Single writer connection (channel-buffered, batched)
///     - Pooled read connections (parallel queries, bounded concurrency)
///     - Schema aligned with generated DuckDbSchema.g.cs (OTel 1.39)
/// </summary>
public sealed class DuckDbStore : IAsyncDisposable
{
    // ==========================================================================
    // SQL Constants - Aligned with DuckDbSchema.g.cs
    // ==========================================================================

    // DuckDB.NET 1.4.3: Use positional parameters ($1, $2, ...) instead of named ($param_name).
    private const string InsertSpanSql = """
                                         -- noinspection SqlNoDataSourceInspectionForFile
                                         INSERT INTO spans (
                                             span_id, trace_id, parent_span_id, session_id,
                                             name, kind, start_time_unix_nano, end_time_unix_nano, duration_ns,
                                             status_code, status_message, service_name,
                                             gen_ai_system, gen_ai_request_model, gen_ai_response_model,
                                             gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_temperature,
                                             gen_ai_stop_reason, gen_ai_tool_name, gen_ai_tool_call_id,
                                             gen_ai_cost_usd, attributes_json, resource_json
                                         ) VALUES (
                                             $1, $2, $3, $4,
                                             $5, $6, $7, $8, $9,
                                             $10, $11, $12,
                                             $13, $14, $15,
                                             $16, $17, $18,
                                             $19, $20, $21,
                                             $22, $23, $24
                                         )
                                         ON CONFLICT (span_id) DO UPDATE SET
                                             end_time_unix_nano = EXCLUDED.end_time_unix_nano,
                                             duration_ns = EXCLUDED.duration_ns,
                                             status_code = EXCLUDED.status_code,
                                             status_message = EXCLUDED.status_message,
                                             gen_ai_input_tokens = EXCLUDED.gen_ai_input_tokens,
                                             gen_ai_output_tokens = EXCLUDED.gen_ai_output_tokens,
                                             gen_ai_cost_usd = EXCLUDED.gen_ai_cost_usd,
                                             attributes_json = EXCLUDED.attributes_json,
                                             resource_json = EXCLUDED.resource_json
                                         """;

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

    private const string SelectSpanColumns = """
                                             span_id, trace_id, parent_span_id, session_id,
                                             name, kind, start_time_unix_nano, end_time_unix_nano, duration_ns,
                                             status_code, status_message, service_name,
                                             gen_ai_system, gen_ai_request_model, gen_ai_response_model,
                                             gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_temperature,
                                             gen_ai_stop_reason, gen_ai_tool_name, gen_ai_tool_call_id,
                                             gen_ai_cost_usd, attributes_json, resource_json, created_at
                                             """;

    // ==========================================================================
    // Instance Fields
    // ==========================================================================

    private readonly CancellationTokenSource _cts = new();
    private readonly string _databasePath;
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

    // ==========================================================================
    // Constructor
    // ==========================================================================

    public DuckDbStore(
        string databasePath = "qyl.duckdb",
        int jobQueueCapacity = 1000,
        int maxConcurrentReads = 8,
        int maxRetainedReadConnections = 16)
    {
        _databasePath = databasePath;
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

    // ==========================================================================
    // Properties
    // ==========================================================================

    /// <summary>
    ///     Exposes the write connection for legacy compatibility (SessionQueryService).
    ///     NOTE: For new code, use RentReadConnectionAsync for read queries.
    /// </summary>
    public DuckDBConnection Connection { get; }

    // ==========================================================================
    // Lifecycle
    // ==========================================================================

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
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

    // ==========================================================================
    // Connection Management
    // ==========================================================================

    /// <summary>
    ///     Provides direct read access for SessionQueryService.
    ///     Returns a leased connection that must be disposed after use.
    /// </summary>
    public async ValueTask<IAsyncDisposable> GetReadConnectionAsync(CancellationToken ct = default) =>
        await RentReadAsync(ct).ConfigureAwait(false);

    // ==========================================================================
    // Span Operations
    // ==========================================================================

    /// <summary>
    ///     Enqueues a batch for async writing. With DropOldest, TryWrite always succeeds
    ///     but may drop the oldest queued item (whose OnAborted will be called by WriterLoop).
    /// </summary>
    public ValueTask EnqueueAsync(SpanBatch batch, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        if (batch.Spans.Count is 0) return ValueTask.CompletedTask;

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
        if (batch.Spans.Count is 0) return;

        await WriteBatchInternalAsync(Connection, batch, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SpanStorageRow>> GetSpansBySessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var spans = new List<SpanStorageRow>();
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT {SelectSpanColumns}
                           FROM spans
                           WHERE session_id = $1
                           ORDER BY start_time_unix_nano ASC
                           """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

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
        cmd.CommandText = $"""
                           SELECT {SelectSpanColumns}
                           FROM spans
                           WHERE trace_id = $1
                           ORDER BY start_time_unix_nano ASC
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
        ulong? startAfter = null,
        ulong? startBefore = null,
        byte? statusCode = null,
        string? searchText = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var spans = new List<SpanStorageRow>();
        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add($"session_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = sessionId });
        }

        if (!string.IsNullOrEmpty(providerName))
        {
            conditions.Add($"gen_ai_system = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = providerName });
        }

        if (startAfter.HasValue)
        {
            conditions.Add($"start_time_unix_nano >= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = (decimal)startAfter.Value });
        }

        if (startBefore.HasValue)
        {
            conditions.Add($"start_time_unix_nano <= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = (decimal)startBefore.Value });
        }

        if (statusCode.HasValue)
        {
            conditions.Add($"status_code = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = statusCode.Value });
        }

        if (!string.IsNullOrEmpty(searchText))
        {
            // Search in status_message, name, and attributes_json
            conditions.Add($"(status_message ILIKE ${paramIndex} OR name ILIKE ${paramIndex} OR attributes_json ILIKE ${paramIndex})");
            parameters.Add(new DuckDBParameter { Value = $"%{searchText}%" });
            paramIndex++;
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT {SelectSpanColumns}
                           FROM spans
                           {whereClause}
                           ORDER BY start_time_unix_nano DESC
                           LIMIT ${paramIndex}
                           """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            spans.Add(MapSpan(reader));

        return spans;
    }

    // ==========================================================================
    // Storage Statistics
    // ==========================================================================

    public async Task<StorageStats> GetStorageStatsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              (SELECT COUNT(*) FROM spans) as span_count,
                              (SELECT COUNT(*) FROM sessions) as session_count,
                              (SELECT COUNT(*) FROM logs) as log_count,
                              (SELECT MIN(start_time_unix_nano) FROM spans) as oldest_span,
                              (SELECT MAX(start_time_unix_nano) FROM spans) as newest_span
                          """;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new StorageStats
            {
                SpanCount = reader.Col(0).GetInt64(0),
                SessionCount = reader.Col(1).GetInt64(0),
                LogCount = reader.Col(2).GetInt64(0),
                OldestSpanTime = reader.Col(3).AsUInt64,
                NewestSpanTime = reader.Col(4).AsUInt64
            };
        }

        return new StorageStats();
    }

    public async Task<GenAiStats> GetGenAiStatsAsync(
        string? sessionId = null,
        ulong? startAfter = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var conditions = new List<string> { "gen_ai_system IS NOT NULL" };
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add($"session_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = sessionId });
        }

        if (startAfter.HasValue)
        {
            conditions.Add($"start_time_unix_nano >= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = (decimal)startAfter.Value });
        }

        var whereClause = string.Join(" AND ", conditions);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT
                               COUNT(*) as request_count,
                               COALESCE(SUM(gen_ai_input_tokens), 0) as total_input_tokens,
                               COALESCE(SUM(gen_ai_output_tokens), 0) as total_output_tokens,
                               COALESCE(SUM(gen_ai_cost_usd), 0) as total_cost_usd,
                               AVG(gen_ai_cost_usd) as avg_cost
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
                TotalCostUsd = reader.Col(3).GetDouble(0),
                AverageEvalScore = reader.Col(4).AsDouble
            };
        }

        return new GenAiStats();
    }

    /// <summary>
    ///     Gets the approximate storage size in bytes.
    ///     For file-based databases, returns the file size.
    ///     For in-memory databases, queries DuckDB's internal memory usage.
    /// </summary>
    public long GetStorageSizeBytes()
    {
        if (Volatile.Read(ref _disposed) is not 0)
            return 0;

        // For file-based databases, use file size (fast, no DB query needed)
        if (!_isInMemory)
        {
            try
            {
                var fileInfo = new FileInfo(_databasePath);
                if (fileInfo.Exists)
                    return fileInfo.Length;
            }
            catch
            {
                // Fall through to return 0 if file access fails
            }

            return 0;
        }

        // For in-memory databases, query DuckDB's memory usage via PRAGMA
        try
        {
            using var cmd = Connection.CreateCommand();
            cmd.CommandText = "SELECT database_size FROM pragma_database_size()";
            var result = cmd.ExecuteScalar();
            if (result is long size)
                return size;
            if (result is string sizeStr && long.TryParse(sizeStr, out var parsed))
                return parsed;
        }
        catch
        {
            // Return 0 if query fails
        }

        return 0;
    }

    // ==========================================================================
    // Log Operations
    // ==========================================================================

    public async Task InsertLogsAsync(IReadOnlyList<LogStorageRow> logs, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (logs.Count is 0) return;

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
            cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)log.TimeUnixNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = log.ObservedTimeUnixNano.HasValue ? (decimal)log.ObservedTimeUnixNano.Value : DBNull.Value });
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
        ulong? after = null,
        ulong? before = null,
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
            conditions.Add($"time_unix_nano >= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = (decimal)after.Value });
        }

        if (before.HasValue)
        {
            conditions.Add($"time_unix_nano <= ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = (decimal)before.Value });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT log_id, trace_id, span_id, session_id,
                                  time_unix_nano, observed_time_unix_nano,
                                  severity_number, severity_text, body,
                                  service_name, attributes_json, resource_json, created_at
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

    // ==========================================================================
    // Archiving
    // ==========================================================================

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

    // ==========================================================================
    // Private Methods - Writing
    // ==========================================================================

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

    private static async ValueTask WriteBatchInternalAsync(
        DuckDBConnection con,
        SpanBatch batch,
        CancellationToken ct)
    {
        if (batch.Spans.Count is 0) return;

        await using var tx = await con.BeginTransactionAsync(ct).ConfigureAwait(false);

        foreach (var span in batch.Spans)
        {
            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = InsertSpanSql;

            // Identity
            cmd.Parameters.Add(new DuckDBParameter { Value = span.SpanId });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.TraceId });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.ParentSpanId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.SessionId ?? (object)DBNull.Value });

            // Core fields (UBIGINT passed as decimal for DuckDB.NET)
            cmd.Parameters.Add(new DuckDBParameter { Value = span.Name });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.Kind });
            cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)span.StartTimeUnixNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)span.EndTimeUnixNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)span.DurationNs });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.StatusCode });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.StatusMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.ServiceName ?? (object)DBNull.Value });

            // GenAI fields
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiSystem ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiRequestModel ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiResponseModel ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiInputTokens ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiOutputTokens ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiTemperature ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiStopReason ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiToolName ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiToolCallId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiCostUsd ?? (object)DBNull.Value });

            // JSON storage
            cmd.Parameters.Add(new DuckDBParameter { Value = span.AttributesJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = span.ResourceJson ?? (object)DBNull.Value });

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
        var cutoffNano = (ulong)((now - olderThan).ToUnixTimeMilliseconds() * 1_000_000);
        var timestamp = now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        await using var countCmd = con.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM spans WHERE start_time_unix_nano < $1";
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)cutoffNano });
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
        if (count is 0) return 0;

        var finalPath = Path.GetFullPath(Path.Combine(outputDirectory, $"spans_{timestamp}.parquet"));
        var tempPath = finalPath + ".tmp";

        ValidateDuckDbSqlPath(finalPath);
        ValidateDuckDbSqlPath(tempPath);

        await using (var exportCmd = con.CreateCommand())
        {
            exportCmd.CommandText = $"""
                                     COPY (SELECT * FROM spans WHERE start_time_unix_nano < $1)
                                     TO '{tempPath}'
                                     (FORMAT PARQUET, COMPRESSION ZSTD, ROW_GROUP_SIZE 100000)
                                     """;
            exportCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)cutoffNano });
            await exportCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        File.Move(tempPath, finalPath, true);

        await using var tx = await con.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var deleteCmd = con.CreateCommand();
        deleteCmd.Transaction = tx;
        deleteCmd.CommandText = "DELETE FROM spans WHERE start_time_unix_nano < $1";
        deleteCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)cutoffNano });
        await deleteCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return count;
    }

    // ==========================================================================
    // Private Methods - Reading
    // ==========================================================================

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

    // ==========================================================================
    // Private Methods - Schema & Mapping
    // ==========================================================================

    private static void InitializeSchema(DuckDBConnection con)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = DuckDbSchema.GetSchemaDdl();
        cmd.ExecuteNonQuery();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SpanStorageRow MapSpan(DbDataReader reader) =>
        new()
        {
            SpanId = reader.GetString(0),
            TraceId = reader.GetString(1),
            ParentSpanId = reader.Col(2).AsString,
            SessionId = reader.Col(3).AsString,
            Name = reader.GetString(4),
            Kind = reader.Col(5).GetByte(0),
            StartTimeUnixNano = reader.Col(6).GetUInt64(0),
            EndTimeUnixNano = reader.Col(7).GetUInt64(0),
            DurationNs = reader.Col(8).GetUInt64(0),
            StatusCode = reader.Col(9).GetByte(0),
            StatusMessage = reader.Col(10).AsString,
            ServiceName = reader.Col(11).AsString,
            GenAiSystem = reader.Col(12).AsString,
            GenAiRequestModel = reader.Col(13).AsString,
            GenAiResponseModel = reader.Col(14).AsString,
            GenAiInputTokens = reader.Col(15).AsInt64,
            GenAiOutputTokens = reader.Col(16).AsInt64,
            GenAiTemperature = reader.Col(17).AsDouble,
            GenAiStopReason = reader.Col(18).AsString,
            GenAiToolName = reader.Col(19).AsString,
            GenAiToolCallId = reader.Col(20).AsString,
            GenAiCostUsd = reader.Col(21).AsDouble,
            AttributesJson = reader.Col(22).AsString,
            ResourceJson = reader.Col(23).AsString,
            CreatedAt = reader.Col(24).AsDateTimeOffset
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LogStorageRow MapLog(DbDataReader reader) =>
        new()
        {
            LogId = reader.GetString(0),
            TraceId = reader.Col(1).AsString,
            SpanId = reader.Col(2).AsString,
            SessionId = reader.Col(3).AsString,
            TimeUnixNano = reader.Col(4).GetUInt64(0),
            ObservedTimeUnixNano = reader.Col(5).AsUInt64,
            SeverityNumber = reader.Col(6).GetByte(0),
            SeverityText = reader.Col(7).AsString,
            Body = reader.Col(8).AsString,
            ServiceName = reader.Col(9).AsString,
            AttributesJson = reader.Col(10).AsString,
            ResourceJson = reader.Col(11).AsString,
            CreatedAt = reader.Col(12).AsDateTimeOffset
        };

    private static void ValidateDuckDbSqlPath(string fullPath)
    {
        if (fullPath.Contains('\'') || fullPath.Contains(';') || fullPath.Contains("--") ||
            fullPath.Contains('\n') || fullPath.Contains('\r') || fullPath.Contains('\0'))
            throw new ArgumentException("Invalid path characters detected", nameof(fullPath));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    // ==========================================================================
    // Nested Types
    // ==========================================================================

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
}
