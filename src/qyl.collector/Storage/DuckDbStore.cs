using qyl.collector.Errors;
using qyl.protocol.Copilot;
using static System.Threading.Volatile;

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
    // Multi-Row Batch Insert Constants
    // DuckDB.NET 1.4.3: Use positional parameters ($1, $2, ...) instead of named.
    // ==========================================================================

    /// <summary>
    ///     Maximum spans per multi-row INSERT statement.
    ///     24 columns per span * 100 spans = 2400 parameters (well under DuckDB limits).
    /// </summary>
    private const int MaxSpansPerBatch = 100;

    /// <summary>
    ///     Maximum logs per multi-row INSERT statement.
    ///     12 columns per log * 200 logs = 2400 parameters.
    /// </summary>
    private const int MaxLogsPerBatch = 200;

    private const int SpanColumnCount = 26;
    private const int LogColumnCount = 12;

    private const string SpanColumnList = """
                                          span_id, trace_id, parent_span_id, session_id,
                                          name, kind, start_time_unix_nano, end_time_unix_nano, duration_ns,
                                          status_code, status_message, service_name,
                                          gen_ai_provider_name, gen_ai_request_model, gen_ai_response_model,
                                          gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_temperature,
                                          gen_ai_stop_reason, gen_ai_tool_name, gen_ai_tool_call_id,
                                          gen_ai_cost_usd, attributes_json, resource_json,
                                          baggage_json, schema_url
                                          """;

    private const string SpanOnConflictClause = """
                                                ON CONFLICT (span_id) DO UPDATE SET
                                                    end_time_unix_nano = EXCLUDED.end_time_unix_nano,
                                                    duration_ns = EXCLUDED.duration_ns,
                                                    status_code = EXCLUDED.status_code,
                                                    status_message = EXCLUDED.status_message,
                                                    gen_ai_input_tokens = EXCLUDED.gen_ai_input_tokens,
                                                    gen_ai_output_tokens = EXCLUDED.gen_ai_output_tokens,
                                                    gen_ai_cost_usd = EXCLUDED.gen_ai_cost_usd,
                                                    attributes_json = EXCLUDED.attributes_json,
                                                    resource_json = EXCLUDED.resource_json,
                                                    baggage_json = EXCLUDED.baggage_json,
                                                    schema_url = EXCLUDED.schema_url
                                                """;

    private const string LogColumnList = """
                                         log_id, trace_id, span_id, session_id,
                                         time_unix_nano, observed_time_unix_nano,
                                         severity_number, severity_text, body,
                                         service_name, attributes_json, resource_json
                                         """;

    private const string SelectSpanColumns = """
                                             span_id, trace_id, parent_span_id, session_id,
                                             name, kind, start_time_unix_nano, end_time_unix_nano, duration_ns,
                                             status_code, status_message, service_name,
                                             gen_ai_provider_name, gen_ai_request_model, gen_ai_response_model,
                                             gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_temperature,
                                             gen_ai_stop_reason, gen_ai_tool_name, gen_ai_tool_call_id,
                                             gen_ai_cost_usd, attributes_json, resource_json,
                                             baggage_json, schema_url, created_at
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

        // For in-memory databases, share the write connection for reads (DuckDB isolation issue)
        // Each :memory: connection creates a separate isolated database instance
        _readPolicy = new ReadConnectionPolicy(databasePath, _isInMemory ? Connection : null);
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

    internal string DatabasePath => _databasePath;

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
    ///     Provides pooled read connection for query services.
    ///     Returns a leased connection that must be disposed after use.
    /// </summary>
    public ValueTask<ReadLease> GetReadConnectionAsync(CancellationToken ct = default) =>
        RentReadAsync(ct);

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
        if (ct.IsCancellationRequested)
            return ValueTask.FromCanceled(ct);
        if (batch.Spans.Count is 0)
            return ValueTask.CompletedTask;

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
        if (batch.Spans.Count is 0)
            return;

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
            conditions.Add($"gen_ai_provider_name = ${paramIndex++}");
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
            conditions.Add(
                $"(status_message ILIKE ${paramIndex} OR name ILIKE ${paramIndex} OR attributes_json ILIKE ${paramIndex})");
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
                              (SELECT COUNT(*) FROM session_entities) as session_count,
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

        var conditions = new List<string> { "gen_ai_provider_name IS NOT NULL" };
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(sessionId))
        {
            conditions.Add($"session_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = sessionId });
        }

        if (startAfter.HasValue)
        {
            conditions.Add($"start_time_unix_nano >= ${paramIndex}");
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
        if (Read(ref _disposed) is not 0)
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
    // Cleanup Operations
    // ==========================================================================

    public async Task<long> GetSpanCountAsync(CancellationToken ct = default)
    {
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM spans";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is long count ? count : 0;
    }

    public async Task<long> GetLogCountAsync(CancellationToken ct = default)
    {
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM logs";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is long count ? count : 0;
    }

    public async Task<int> DeleteSpansBeforeAsync(ulong timestampNanos, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM spans WHERE start_time_unix_nano < $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)timestampNanos });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    public async Task<int> DeleteOldestSpansAsync(long count, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                DELETE FROM spans
                WHERE span_id IN (
                    SELECT span_id FROM spans
                    ORDER BY start_time_unix_nano ASC
                    LIMIT $1
                )
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = count });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    public async Task<int> DeleteOldestLogsAsync(long count, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                DELETE FROM logs
                WHERE rowid IN (
                    SELECT rowid FROM logs
                    ORDER BY time_unix_nano ASC
                    LIMIT $1
                )
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = count });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    // ==========================================================================
    // Clear All Operations (for dashboard controls)
    // ==========================================================================

    /// <summary>
    ///     Clears all spans from the database.
    /// </summary>
    public async Task<int> ClearAllSpansAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(static async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM spans";
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears all logs from the database.
    /// </summary>
    public async Task<int> ClearAllLogsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(static async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM logs";
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears all sessions from the database.
    /// </summary>
    public async Task<int> ClearAllSessionsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(static async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM session_entities";
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears all telemetry data (spans, logs, sessions) from the database.
    /// </summary>
    public async Task<ClearTelemetryResult> ClearAllTelemetryAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<ClearTelemetryResult>(static async (con, token) =>
        {
            // Use transaction for atomic clear
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);

            int spansDeleted, logsDeleted, sessionsDeleted;

            await using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM spans";
                spansDeleted = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            await using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM logs";
                logsDeleted = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            await using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM session_entities";
                sessionsDeleted = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            await tx.CommitAsync(token).ConfigureAwait(false);

            return new ClearTelemetryResult(spansDeleted, logsDeleted, sessionsDeleted);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    // ==========================================================================
    // Workflow Execution Operations
    // ==========================================================================

    /// <summary>
    ///     Inserts a new workflow execution record.
    /// </summary>
    public async Task InsertExecutionAsync(WorkflowExecution execution, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO workflow_executions
                    (execution_id, workflow_name, status, started_at, completed_at,
                     result, error, parameters_json, input_tokens, output_tokens, trace_id)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
                ON CONFLICT (execution_id) DO NOTHING
                """;
            AddExecutionInsertParameters(cmd, execution);
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates an existing workflow execution (status, result, tokens, etc.).
    /// </summary>
    public async Task UpdateExecutionAsync(WorkflowExecution execution, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                UPDATE workflow_executions SET
                    status = $1,
                    completed_at = $2,
                    result = $3,
                    error = $4,
                    input_tokens = $5,
                    output_tokens = $6,
                    trace_id = $7
                WHERE execution_id = $8
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = execution.Status.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = execution.CompletedAt.HasValue
                    ? execution.CompletedAt.Value.UtcDateTime
                    : DBNull.Value
            });
            cmd.Parameters.Add(new DuckDBParameter { Value = execution.Result ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = execution.Error ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = execution.InputTokens ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = execution.OutputTokens ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = execution.TraceId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = execution.Id });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a single workflow execution by ID.
    /// </summary>
    public async Task<WorkflowExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT execution_id, workflow_name, status, started_at, completed_at,
                   result, error, parameters_json, input_tokens, output_tokens, trace_id
            FROM workflow_executions
            WHERE execution_id = $1
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = executionId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return MapExecution(reader);

        return null;
    }

    /// <summary>
    ///     Gets workflow executions with optional filtering.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowExecution>> GetExecutionsAsync(
        string? workflowName = null,
        string? status = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(workflowName))
        {
            conditions.Add($"workflow_name = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = workflowName });
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add($"status = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = status });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT execution_id, workflow_name, status, started_at, completed_at,
                   result, error, parameters_json, input_tokens, output_tokens, trace_id
            FROM workflow_executions
            {whereClause}
            ORDER BY started_at DESC
            LIMIT ${paramIndex}
            """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var executions = new List<WorkflowExecution>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            executions.Add(MapExecution(reader));

        return executions;
    }

    private static void AddExecutionInsertParameters(DuckDBCommand cmd, WorkflowExecution execution)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = execution.Id });
        cmd.Parameters.Add(new DuckDBParameter { Value = execution.WorkflowName });
        cmd.Parameters.Add(new DuckDBParameter { Value = execution.Status.ToString().ToLowerInvariant() });
        cmd.Parameters.Add(new DuckDBParameter { Value = execution.StartedAt.UtcDateTime });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = execution.CompletedAt.HasValue
                ? execution.CompletedAt.Value.UtcDateTime
                : DBNull.Value
        });
        cmd.Parameters.Add(new DuckDBParameter { Value = execution.Result ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = execution.Error ?? (object)DBNull.Value });
        // Serialize parameters as JSON if present
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = execution.Parameters is not null
                ? JsonSerializer.Serialize(execution.Parameters)
                : DBNull.Value
        });
        cmd.Parameters.Add(new DuckDBParameter { Value = execution.InputTokens ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = execution.OutputTokens ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = execution.TraceId ?? (object)DBNull.Value });
    }

    private static WorkflowExecution MapExecution(DbDataReader reader)
    {
        var parametersJson = reader.Col(7).AsString;
        Dictionary<string, string>? parameters = null;
        if (parametersJson is not null)
        {
            parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(parametersJson);
        }

        return new WorkflowExecution
        {
            Id = reader.GetString(0),
            WorkflowName = reader.GetString(1),
            Status = Enum.TryParse<WorkflowStatus>(reader.GetString(2), true, out var s) ? s : WorkflowStatus.Pending,
            StartedAt = new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
            CompletedAt = reader.Col(4).AsDateTimeOffset,
            Result = reader.Col(5).AsString,
            Error = reader.Col(6).AsString,
            Parameters = parameters,
            InputTokens = reader.Col(8).AsInt64,
            OutputTokens = reader.Col(9).AsInt64,
            TraceId = reader.Col(10).AsString
        };
    }

    // ==========================================================================
    // Insight Materialization Operations
    // ==========================================================================

    /// <summary>
    ///     Gets the content hash for a specific insight tier. Returns null if not yet materialized.
    /// </summary>
    public async Task<string?> GetInsightHashAsync(string tier, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT content_hash FROM materialized_insights WHERE tier = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = tier });

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result as string;
    }

    /// <summary>
    ///     Gets all materialized insight rows.
    /// </summary>
    public async Task<IReadOnlyList<InsightRow>> GetAllInsightsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var rows = new List<InsightRow>();
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT tier, content_markdown, content_hash, materialized_at,
                   span_count_at_materialization, duration_ms
            FROM materialized_insights
            ORDER BY tier
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new InsightRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
                reader.Col(4).GetInt64(0),
                reader.Col(5).GetDouble(0)));
        }

        return rows;
    }

    /// <summary>
    ///     Upserts a materialized insight tier via the write queue.
    /// </summary>
    public async Task UpsertInsightAsync(
        string tier,
        string markdown,
        string hash,
        long spanCount,
        double durationMs,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO materialized_insights
                    (tier, content_markdown, content_hash, materialized_at, span_count_at_materialization, duration_ms)
                VALUES ($1, $2, $3, CURRENT_TIMESTAMP, $4, $5)
                ON CONFLICT (tier) DO UPDATE SET
                    content_markdown = EXCLUDED.content_markdown,
                    content_hash = EXCLUDED.content_hash,
                    materialized_at = EXCLUDED.materialized_at,
                    span_count_at_materialization = EXCLUDED.span_count_at_materialization,
                    duration_ms = EXCLUDED.duration_ms
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = tier });
            cmd.Parameters.Add(new DuckDBParameter { Value = markdown });
            cmd.Parameters.Add(new DuckDBParameter { Value = hash });
            cmd.Parameters.Add(new DuckDBParameter { Value = spanCount });
            cmd.Parameters.Add(new DuckDBParameter { Value = durationMs });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    // ==========================================================================
    // Log Operations
    // ==========================================================================

    public async Task InsertLogsAsync(IReadOnlyList<LogStorageRow> logs, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (logs.Count is 0)
            return;

        await using var tx = await Connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        var totalLogs = logs.Count;
        var offset = 0;

        while (offset < totalLogs)
        {
            var chunkSize = Math.Min(MaxLogsPerBatch, totalLogs - offset);
            var sql = BuildMultiRowLogInsertSql(chunkSize);

            await using var cmd = Connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;

            for (var i = 0; i < chunkSize; i++)
            {
                AddLogParameters(cmd, logs[offset + i]);
            }

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            offset += chunkSize;
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
    // Error Operations
    // ==========================================================================

    private const string ErrorUpsertSql = """
        INSERT INTO errors
            (error_id, error_type, message, category, fingerprint,
             first_seen, last_seen, occurrence_count,
             affected_users, affected_services, status,
             assigned_to, issue_url, sample_traces)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
        ON CONFLICT (fingerprint) DO UPDATE SET
            last_seen = EXCLUDED.last_seen,
            occurrence_count = errors.occurrence_count + 1,
            affected_users = CASE
                WHEN EXCLUDED.affected_users IS NULL THEN errors.affected_users
                WHEN errors.affected_users IS NULL THEN EXCLUDED.affected_users
                ELSE errors.affected_users + EXCLUDED.affected_users
            END,
            affected_services = CASE
                WHEN errors.affected_services IS NULL THEN EXCLUDED.affected_services
                WHEN EXCLUDED.affected_services IS NULL THEN errors.affected_services
                WHEN ',' || errors.affected_services || ',' LIKE '%,' || EXCLUDED.affected_services || ',%'
                    THEN errors.affected_services
                ELSE errors.affected_services || ',' || EXCLUDED.affected_services
            END,
            sample_traces = CASE
                WHEN errors.sample_traces IS NULL THEN EXCLUDED.sample_traces
                WHEN EXCLUDED.sample_traces IS NULL THEN errors.sample_traces
                WHEN ',' || errors.sample_traces || ',' LIKE '%,' || EXCLUDED.sample_traces || ',%'
                    THEN errors.sample_traces
                WHEN length(errors.sample_traces) - length(replace(errors.sample_traces, ',', '')) >= 9
                    THEN errors.sample_traces
                ELSE errors.sample_traces || ',' || EXCLUDED.sample_traces
            END
        """;

    private static void AddErrorUpsertParameters(DuckDBCommand cmd, ErrorEvent error, string errorId, DateTime now)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = errorId });
        cmd.Parameters.Add(new DuckDBParameter { Value = error.ErrorType });
        cmd.Parameters.Add(new DuckDBParameter { Value = error.Message });
        cmd.Parameters.Add(new DuckDBParameter { Value = error.Category });
        cmd.Parameters.Add(new DuckDBParameter { Value = error.Fingerprint });
        cmd.Parameters.Add(new DuckDBParameter { Value = now });
        cmd.Parameters.Add(new DuckDBParameter { Value = now });
        cmd.Parameters.Add(new DuckDBParameter { Value = 1L });
        cmd.Parameters.Add(new DuckDBParameter { Value = error.UserId is not null ? 1L : (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = error.ServiceName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = "new" });
        cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = error.TraceId ?? (object)DBNull.Value });
    }

    /// <summary>
    ///     Upserts an error event. On fingerprint conflict, increments occurrence_count,
    ///     updates last_seen, merges affected_services, and appends to sample_traces (max 10).
    /// </summary>
    public async Task UpsertErrorAsync(ErrorEvent error, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            var now = TimeProvider.System.GetUtcNow().UtcDateTime;
            await using var cmd = con.CreateCommand();
            cmd.CommandText = ErrorUpsertSql;
            AddErrorUpsertParameters(cmd, error, Guid.NewGuid().ToString("N"), now);
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets errors with optional filtering by category, status, and service name.
    /// </summary>
    public async Task<IReadOnlyList<ErrorRow>> GetErrorsAsync(
        string? category = null,
        string? status = null,
        string? serviceName = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var rows = new List<ErrorRow>();
        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(category))
        {
            conditions.Add($"category = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = category });
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add($"status = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = status });
        }

        if (!string.IsNullOrEmpty(serviceName))
        {
            conditions.Add($"',' || affected_services || ',' LIKE ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = $"%,{serviceName},%" });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT error_id, error_type, message, category, fingerprint,
                                  first_seen, last_seen, occurrence_count,
                                  affected_users, affected_services, status,
                                  assigned_to, issue_url, sample_traces
                           FROM errors
                           {whereClause}
                           ORDER BY last_seen DESC
                           LIMIT ${paramIndex}
                           """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(MapErrorRow(reader));

        return rows;
    }

    /// <summary>
    ///     Gets aggregated error statistics grouped by category.
    /// </summary>
    public async Task<ErrorStats> GetErrorStatsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var byCategory = new List<ErrorCategoryStat>();
        long totalCount = 0;

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT category, SUM(occurrence_count) as total
            FROM errors
            GROUP BY category
            ORDER BY total DESC
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var count = reader.GetInt64(1);
            totalCount += count;
            byCategory.Add(new ErrorCategoryStat
            {
                Category = reader.GetString(0),
                Count = count
            });
        }

        return new ErrorStats
        {
            TotalCount = totalCount,
            ByCategory = byCategory
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ErrorRow MapErrorRow(DbDataReader reader) =>
        new()
        {
            ErrorId = reader.GetString(0),
            ErrorType = reader.GetString(1),
            Message = reader.GetString(2),
            Category = reader.GetString(3),
            Fingerprint = reader.GetString(4),
            FirstSeen = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
            LastSeen = new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
            OccurrenceCount = reader.GetInt64(7),
            AffectedUsers = reader.Col(8).AsInt64,
            AffectedServices = reader.Col(9).AsString,
            Status = reader.GetString(10),
            AssignedTo = reader.Col(11).AsString,
            IssueUrl = reader.Col(12).AsString,
            SampleTraces = reader.Col(13).AsString
        };

    /// <summary>
    ///     Updates the status (and optionally assigned_to) for a specific error.
    /// </summary>
    public async Task UpdateErrorStatusAsync(string errorId, string status, string? assignedTo = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = assignedTo is not null
                ? "UPDATE errors SET status = $1, assigned_to = $2 WHERE error_id = $3"
                : "UPDATE errors SET status = $1 WHERE error_id = $2";

            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            if (assignedTo is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = assignedTo });
            cmd.Parameters.Add(new DuckDBParameter { Value = errorId });

            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a single error by its ID. Returns null if not found.
    /// </summary>
    public async Task<ErrorRow?> GetErrorByIdAsync(string errorId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT error_id, error_type, message, category, fingerprint,
                   first_seen, last_seen, occurrence_count, affected_users,
                   affected_services, status, assigned_to, issue_url, sample_traces
            FROM errors WHERE error_id = $1
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = errorId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapErrorRow(reader) : null;
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
        if (ct.IsCancellationRequested)
            return await Task.FromCanceled<int>(ct).ConfigureAwait(false);

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
        if (batch.Spans.Count is 0)
            return;

        await using var tx = await con.BeginTransactionAsync(ct).ConfigureAwait(false);

        var spans = batch.Spans;
        var totalSpans = spans.Count;
        var offset = 0;

        while (offset < totalSpans)
        {
            var chunkSize = Math.Min(MaxSpansPerBatch, totalSpans - offset);
            var sql = BuildMultiRowSpanInsertSql(chunkSize);

            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;

            for (var i = 0; i < chunkSize; i++)
            {
                AddSpanParameters(cmd, spans[offset + i]);
            }

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            offset += chunkSize;
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);

        // Writer-side error extraction: runs on the single writer thread after span commit.
        // Zero additional channel pressure — errors are extracted inline within the existing job.
        await ExtractAndUpsertErrorsAsync(con, batch.Spans, ct).ConfigureAwait(false);
    }

    private static async ValueTask ExtractAndUpsertErrorsAsync(
        DuckDBConnection con,
        IReadOnlyList<SpanStorageRow> spans,
        CancellationToken ct)
    {
        List<ErrorEvent>? errors = null;
        foreach (var span in spans)
        {
            if (ErrorExtractor.Extract(span) is { } errorEvent)
            {
                errors ??= [];
                errors.Add(errorEvent);
            }
        }

        if (errors is null) return;

        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        await using var tx = await con.BeginTransactionAsync(ct).ConfigureAwait(false);
        foreach (var error in errors)
        {
            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = ErrorUpsertSql;
            AddErrorUpsertParameters(cmd, error, Guid.NewGuid().ToString("N"), now);
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
        ValidateArchiveDirectory(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var now = TimeProvider.System.GetUtcNow();
        var cutoffMs = (now - olderThan).ToUnixTimeMilliseconds();
        var cutoffNano = (ulong)cutoffMs * 1_000_000UL; // UL suffix forces unsigned arithmetic
        var timestamp = now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        await using var countCmd = con.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM spans WHERE start_time_unix_nano < $1";
        countCmd.Parameters.Add(new DuckDBParameter { Value = (decimal)cutoffNano });
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
        if (count is 0)
            return 0;

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
    // Private Methods - Multi-Row Insert SQL Builders (with caching)
    // ==========================================================================

    // Cache SQL statements for common batch sizes to avoid repeated StringBuilder allocations
    private static readonly ConcurrentDictionary<int, string> SSpanInsertSqlCache = new();
    private static readonly ConcurrentDictionary<int, string> SLogInsertSqlCache = new();

    /// <summary>
    ///     Gets or builds a multi-row INSERT statement for spans with ON CONFLICT DO UPDATE.
    ///     Caches SQL for common batch sizes to reduce allocations.
    /// </summary>
    private static string BuildMultiRowSpanInsertSql(int spanCount)
    {
        Debug.Assert(spanCount is > 0 and <= MaxSpansPerBatch);

        return SSpanInsertSqlCache.GetOrAdd(spanCount, static count =>
        {
            var sb = new StringBuilder(2048);
            sb.Append("INSERT INTO spans (").Append(SpanColumnList).Append(") VALUES ");

            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                var baseParam = i * SpanColumnCount;
                sb.Append('(');
                for (var col = 0; col < SpanColumnCount; col++)
                {
                    if (col > 0)
                        sb.Append(", ");
                    sb.Append('$').Append(baseParam + col + 1);
                }

                sb.Append(')');
            }

            sb.Append(' ').Append(SpanOnConflictClause);
            return sb.ToString();
        });
    }

    /// <summary>
    ///     Gets or builds a multi-row INSERT statement for logs (no ON CONFLICT).
    ///     Caches SQL for common batch sizes to reduce allocations.
    /// </summary>
    private static string BuildMultiRowLogInsertSql(int logCount)
    {
        Debug.Assert(logCount is > 0 and <= MaxLogsPerBatch);

        return SLogInsertSqlCache.GetOrAdd(logCount, static count =>
        {
            var sb = new StringBuilder(1024);
            sb.Append("INSERT INTO logs (").Append(LogColumnList).Append(") VALUES ");

            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                var baseParam = i * LogColumnCount;
                sb.Append('(');
                for (var col = 0; col < LogColumnCount; col++)
                {
                    if (col > 0)
                        sb.Append(", ");
                    sb.Append('$').Append(baseParam + col + 1);
                }

                sb.Append(')');
            }

            return sb.ToString();
        });
    }

    /// <summary>
    ///     Adds span parameters to command in column order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddSpanParameters(DuckDBCommand cmd, SpanStorageRow span)
    {
        // Identity (4 columns)
        cmd.Parameters.Add(new DuckDBParameter { Value = span.SpanId });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.TraceId });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.ParentSpanId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.SessionId ?? (object)DBNull.Value });

        // Core fields (8 columns) - UBIGINT passed as decimal for DuckDB.NET
        cmd.Parameters.Add(new DuckDBParameter { Value = span.Name });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.Kind });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)span.StartTimeUnixNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)span.EndTimeUnixNano });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)span.DurationNs });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.StatusCode });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.StatusMessage ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.ServiceName ?? (object)DBNull.Value });

        // GenAI fields (10 columns)
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiProviderName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiRequestModel ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiResponseModel ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiInputTokens ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiOutputTokens ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiTemperature ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiStopReason ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiToolName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiToolCallId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.GenAiCostUsd ?? (object)DBNull.Value });

        // JSON storage (2 columns)
        cmd.Parameters.Add(new DuckDBParameter { Value = span.AttributesJson ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.ResourceJson ?? (object)DBNull.Value });

        // W3C Baggage and OTel Schema URL (2 columns)
        cmd.Parameters.Add(new DuckDBParameter { Value = span.BaggageJson ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.SchemaUrl ?? (object)DBNull.Value });
    }

    /// <summary>
    ///     Adds log parameters to command in column order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddLogParameters(DuckDBCommand cmd, LogStorageRow log)
    {
        cmd.Parameters.Add(new DuckDBParameter { Value = log.LogId });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.TraceId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.SpanId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.SessionId ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)log.TimeUnixNano });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = log.ObservedTimeUnixNano.HasValue ? (decimal)log.ObservedTimeUnixNano.Value : DBNull.Value
        });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.SeverityNumber });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.SeverityText ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.Body ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.ServiceName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.AttributesJson ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.ResourceJson ?? (object)DBNull.Value });
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

    internal void ReturnRead(DuckDBConnection con)
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

    /// <summary>
    ///     Releases the read gate for shared connections (in-memory mode).
    /// </summary>
    internal void ReleaseReadGate() => _readGate.Release();

    // ==========================================================================
    // Private Methods - Schema & Mapping
    // ==========================================================================

    private static void InitializeSchema(DuckDBConnection con)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = DuckDbSchema.GetSchemaDdl();
        cmd.ExecuteNonQuery();

        // Manual schema extensions (not yet in TypeSpec)
        using var extCmd = con.CreateCommand();
        extCmd.CommandText = DuckDbSchema.WorkflowExecutionsDdl;
        extCmd.ExecuteNonQuery();

        using var insightsCmd = con.CreateCommand();
        insightsCmd.CommandText = DuckDbSchema.MaterializedInsightsDdl;
        insightsCmd.ExecuteNonQuery();

        using var errorsCmd = con.CreateCommand();
        errorsCmd.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_errors_fingerprint ON errors(fingerprint);
            CREATE INDEX IF NOT EXISTS idx_errors_category ON errors(category);
            CREATE INDEX IF NOT EXISTS idx_errors_status ON errors(status);
            CREATE INDEX IF NOT EXISTS idx_errors_last_seen ON errors(last_seen);
            """;
        errorsCmd.ExecuteNonQuery();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SpanStorageRow MapSpan(DbDataReader reader) => SpanStorageRow.MapFromReader(reader);

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

    /// <summary>
    ///     Validates archive output directory to prevent path traversal attacks.
    ///     Defense-in-depth: validates before AND after path canonicalization.
    /// </summary>
    private static void ValidateArchiveDirectory(string outputDirectory)
    {
        Throw.IfNull(outputDirectory);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(outputDirectory));

        // Block path traversal sequences before canonicalization
        if (outputDirectory.Contains("..") || outputDirectory.Contains('\0'))
        {
            throw new ArgumentException("Output directory contains invalid traversal sequences",
                nameof(outputDirectory));
        }

        // Canonicalize and verify path
        var fullPath = Path.GetFullPath(outputDirectory);

        // Post-canonicalization check (defense-in-depth against symlink attacks)
        if (fullPath.Contains(".."))
            throw new ArgumentException("Canonicalized path still contains traversal", nameof(outputDirectory));

        // Block system directories
        string[] dangerousPrefixes = OperatingSystem.IsWindows()
            ? [@"C:\Windows", @"C:\Program Files", @"C:\Program Files (x86)"]
            : ["/etc", "/bin", "/sbin", "/usr/bin", "/usr/sbin", "/var/run", "/System"];

        foreach (var prefix in dangerousPrefixes)
        {
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Output directory cannot be under system path: {prefix}",
                    nameof(outputDirectory));
            }
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Read(ref _disposed) is not 0, this);

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

    private sealed class FireAndForgetJob(
        int spanCount,
        Func<DuckDBConnection, CancellationToken, ValueTask> action,
        Action<int>? onDropped = null)
        : WriteJob
    {
        public override ValueTask ExecuteAsync(DuckDBConnection con, CancellationToken ct) => action(con, ct);

        public override void OnAborted(Exception error) => onDropped?.Invoke(spanCount);
    }

    private sealed class WriteJob<TResult>(Func<DuckDBConnection, CancellationToken, ValueTask<TResult>> action)
        : WriteJob
    {
        private readonly TaskCompletionSource<TResult> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TResult> Task => _tcs.Task;

        public override async ValueTask ExecuteAsync(DuckDBConnection con, CancellationToken ct)
        {
            try
            {
                var result = await action(con, ct).ConfigureAwait(false);
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

    private sealed class ReadConnectionPolicy(string path, DuckDBConnection? sharedConnection = null)
        : PooledObjectPolicy<DuckDBConnection>
    {
        private readonly ConcurrentBag<DuckDBConnection> _created = [];

        public override DuckDBConnection Create()
        {
            // For in-memory databases, reuse the shared (write) connection
            // Each :memory: connection creates a separate isolated database instance
            if (sharedConnection is not null)
                return sharedConnection;

            // For file-based databases, create read-only connections
            var connString = $"DataSource={path};ACCESS_MODE=READ_ONLY";
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

// ==========================================================================
// Insight Materialization Operations
// ==========================================================================

/// <summary>
///     Row from the materialized_insights table.
/// </summary>
public sealed record InsightRow(
    string Tier,
    string ContentMarkdown,
    string ContentHash,
    DateTimeOffset MaterializedAt,
    long SpanCountAtMaterialization,
    double DurationMs);

/// <summary>
///     Result of clearing all telemetry data from the database.
/// </summary>
/// <param name="SpansDeleted">Number of spans deleted.</param>
/// <param name="LogsDeleted">Number of logs deleted.</param>
/// <param name="SessionsDeleted">Number of sessions deleted.</param>
public sealed record ClearTelemetryResult(int SpansDeleted, int LogsDeleted, int SessionsDeleted)
{
    /// <summary>Total number of records deleted across all tables.</summary>
    public int TotalDeleted => SpansDeleted + LogsDeleted + SessionsDeleted;
}
