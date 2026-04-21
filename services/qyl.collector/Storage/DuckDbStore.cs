using static System.Threading.Volatile;

namespace Qyl.Collector.Storage;

/// <summary>
///     DuckDB storage with separated read/write paths for optimal concurrency.
///     - Single writer connection (channel-buffered, batched)
///     - Pooled read connections (parallel queries, bounded concurrency)
///     - Schema aligned with generated DuckDbSchema.g.cs (OTel 1.40)
/// </summary>
public sealed partial class DuckDbStore : IAsyncDisposable
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
    ///     16 columns per log * 150 logs = 2400 parameters.
    /// </summary>
    private const int MaxLogsPerBatch = 150;

    private const int SpanColumnCount = 26;
    private const int LogColumnCount = 16;

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
                                                    service_name = COALESCE(EXCLUDED.service_name, service_name),
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
                                         service_name, attributes_json, resource_json,
                                         source_file, source_line, source_column, source_method
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
                                                  ELSE GREATEST(errors.affected_users, EXCLUDED.affected_users)
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

    // ==========================================================================
    // Private Methods - Multi-Row Insert SQL Builders (with caching)
    // ==========================================================================

    /// <summary>
    ///     Maximum profiles per multi-row INSERT statement.
    ///     Generator produces ProfileStorageRow.ColumnCount; 50 * ColumnCount stays under DuckDB parameter limit.
    /// </summary>
    private const int MaxProfilesPerBatch = 50;

    // Cache SQL statements for common batch sizes to avoid repeated StringBuilder allocations
    private static readonly ConcurrentDictionary<int, string> SSpanInsertSqlCache = new();
    private static readonly ConcurrentDictionary<int, string> SLogInsertSqlCache = new();

    // ==========================================================================
    // Clear All Operations (for dashboard controls)
    // ==========================================================================

    private static readonly FrozenSet<string> AllowedClearTables = FrozenSet.Create(
        StringComparer.Ordinal, "spans", "logs", "profiles", "session_entities");

    // ==========================================================================
    // Instance Fields
    // ==========================================================================

    private readonly CancellationTokenSource _cts = new();
    private readonly Counter<long> _droppedJobs;
    private readonly Counter<long> _droppedSpans;
    private readonly bool _isInMemory;
    private readonly Channel<WriteJob> _jobs;

    private readonly Meter _meter = new("Qyl.Collector.storage", "1.0.0");

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
        DatabasePath = databasePath;
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

    private string DatabasePath { get; }

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
            catch (TimeoutException ex)
            {
                // Writer did not finish in 1s — proceed with cleanup.
                Debug.WriteLine(ex);
            }
        }
        catch (OperationCanceledException ex)
        {
            // Expected during shutdown.
            Debug.WriteLine(ex);
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
    // Generic Write Channel
    // ==========================================================================

    /// <summary>
    ///     Executes a write operation through the serialized write channel.
    ///     Use this for any INSERT/UPDATE/DELETE that must go through the single writer.
    /// </summary>
    public async Task<T> ExecuteWriteAsync<T>(Func<DuckDBConnection, CancellationToken, ValueTask<T>> operation,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<T>(operation);
        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Executes a write operation through the serialized write channel (no return value).
    /// </summary>
    public async Task ExecuteWriteAsync(Func<DuckDBConnection, CancellationToken, ValueTask> operation,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await operation(con, token).ConfigureAwait(false);
            return 0;
        });
        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

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
        cmd.CommandText = "SELECT " + SelectSpanColumns
                                    + " FROM spans WHERE session_id = $1 ORDER BY start_time_unix_nano ASC";
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
        cmd.CommandText = "SELECT " + SelectSpanColumns
                                    + " FROM spans WHERE trace_id = $1 ORDER BY start_time_unix_nano ASC";
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
        var qb = new QueryBuilder();

        if (!string.IsNullOrEmpty(sessionId))
            qb.Add("session_id = $N", sessionId);
        if (!string.IsNullOrEmpty(providerName))
            qb.Add("gen_ai_provider_name = $N", providerName);
        if (startAfter.HasValue)
            qb.Add("start_time_unix_nano >= $N", (decimal)startAfter.Value);
        if (startBefore.HasValue)
            qb.Add("start_time_unix_nano <= $N", (decimal)startBefore.Value);
        if (statusCode.HasValue)
            qb.Add("status_code = $N", statusCode.Value);
        if (!string.IsNullOrEmpty(searchText))
            qb.Add("(status_message ILIKE $N OR name ILIKE $N OR attributes_json ILIKE $N)", $"%{searchText}%");

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT " + SelectSpanColumns
                                    + " FROM spans " + qb.WhereClause
                                    + " ORDER BY start_time_unix_nano DESC LIMIT "
                                    + qb.NextParam.ToString(CultureInfo.InvariantCulture);

        qb.ApplyTo(cmd);
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
                              (SELECT COUNT(DISTINCT COALESCE(session_id, trace_id)) FROM spans) as session_count,
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

        var qb = new QueryBuilder();
        qb.AddCondition("gen_ai_provider_name IS NOT NULL");

        if (!string.IsNullOrEmpty(sessionId))
            qb.Add("session_id = $N", sessionId);
        if (startAfter.HasValue)
            qb.Add("start_time_unix_nano >= $N", (decimal)startAfter.Value);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) as request_count,"
                          + " COALESCE(SUM(gen_ai_input_tokens), 0) as total_input_tokens,"
                          + " COALESCE(SUM(gen_ai_output_tokens), 0) as total_output_tokens,"
                          + " COALESCE(SUM(gen_ai_cost_usd), 0) as total_cost_usd,"
                          + " AVG(gen_ai_cost_usd) as avg_cost"
                          + " FROM spans " + qb.WhereClause;

        qb.ApplyTo(cmd);

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
                var fileInfo = new FileInfo(DatabasePath);
                if (fileInfo.Exists)
                    return fileInfo.Length;
            }
            catch (IOException ex)
            {
                // Fall through to return 0 if file access fails.
                Debug.WriteLine(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine(ex);
            }

            return 0;
        }

        // For in-memory databases, query DuckDB's memory usage via PRAGMA
        try
        {
            using var cmd = Connection.CreateCommand();
            cmd.CommandText = "SELECT database_size FROM pragma_database_size()";
            var result = cmd.ExecuteScalar();
            switch (result)
            {
                case long size:
                    return size;
                case string sizeStr when long.TryParse(sizeStr, out var parsed):
                    return parsed;
            }
        }
        catch (DuckDBException ex)
        {
            // Return 0 if the PRAGMA is unavailable on this DuckDB build.
            Debug.WriteLine(ex);
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

    public async Task<int> DeleteSpansBeforeAsync(ulong timestampNanos, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM spans WHERE start_time_unix_nano < $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)timestampNanos });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task<int> DeleteOldestSpansAsync(long count, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
        }, ct).ConfigureAwait(false);

    public async Task<int> DeleteOldestLogsAsync(long count, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
        }, ct).ConfigureAwait(false);

    public Task<int> ClearAllSpansAsync(CancellationToken ct = default) => ClearTableAsync("spans", ct);

    public Task<int> ClearAllLogsAsync(CancellationToken ct = default) => ClearTableAsync("logs", ct);

    public Task<int> ClearAllProfilesAsync(CancellationToken ct = default) => ClearTableAsync("profiles", ct);

    public Task<int> ClearAllSessionsAsync(CancellationToken ct = default) => ClearTableAsync("session_entities", ct);

    private async Task<int> ClearTableAsync(string tableName, CancellationToken ct) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM " + SqlBuilder.Whitelisted(tableName, AllowedClearTables);
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    ///     Clears all telemetry data (spans, logs, sessions) from the database.
    /// </summary>
    public async Task<ClearTelemetryResult> ClearAllTelemetryAsync(CancellationToken ct = default) =>
        await ExecuteWriteAsync(static async (con, token) =>
        {
            // Use transaction for atomic clear
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);

            int spansDeleted, logsDeleted, profilesDeleted, sessionsDeleted;

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
                cmd.CommandText = """
                                  DELETE FROM profile_functions;
                                  DELETE FROM profile_locations;
                                  DELETE FROM profile_mappings;
                                  DELETE FROM profile_samples;
                                  DELETE FROM profile_stacks;
                                  DELETE FROM profiles;
                                  """;
                profilesDeleted = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            await using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM session_entities";
                sessionsDeleted = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            await tx.CommitAsync(token).ConfigureAwait(false);

            return new ClearTelemetryResult(spansDeleted, logsDeleted, profilesDeleted, sessionsDeleted);
        }, ct).ConfigureAwait(false);

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
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
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
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

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
        string? serviceName = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var logs = new List<LogStorageRow>();
        var qb = new QueryBuilder();

        if (!string.IsNullOrEmpty(sessionId))
            qb.Add("session_id = $N", sessionId);
        if (!string.IsNullOrEmpty(traceId))
            qb.Add("trace_id = $N", traceId);
        if (!string.IsNullOrEmpty(severityText))
            qb.Add("severity_text = $N", severityText);
        if (minSeverity.HasValue)
            qb.Add("severity_number >= $N", minSeverity.Value);
        if (!string.IsNullOrEmpty(search))
            qb.Add("body LIKE $N", $"%{search}%");
        if (!string.IsNullOrEmpty(serviceName))
            qb.Add("service_name = $N", serviceName);
        if (after.HasValue)
            qb.Add("time_unix_nano > $N", (decimal)after.Value);
        if (before.HasValue)
            qb.Add("time_unix_nano <= $N", (decimal)before.Value);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT log_id, trace_id, span_id, session_id,"
                          + " time_unix_nano, observed_time_unix_nano,"
                          + " severity_number, severity_text, body,"
                          + " service_name, attributes_json, resource_json,"
                          + " source_file, source_line, source_column, source_method,"
                          + " created_at"
                          + " FROM logs " + qb.WhereClause
                          + " ORDER BY time_unix_nano DESC LIMIT "
                          + qb.NextParam.ToString(CultureInfo.InvariantCulture);

        qb.ApplyTo(cmd);
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            logs.Add(MapLog(reader));

        return logs;
    }

    // ==========================================================================
    // Profile Operations (OTel Profiles v1development)
    // ==========================================================================

    public async Task InsertProfilesAsync(IReadOnlyList<ProfileConversionResult> results,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (results.Count is 0)
            return;

        await using var tx = await Connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Insert header rows — uses generated AddParameters / ColumnList / ColumnCount from [DuckDbTable("profiles")]
        var headers = results.Select(static r => r.Profile).ToList();
        await InsertRowsBatchedAsync(tx, ProfileStorageRow.TableName, ProfileStorageRow.ColumnList,
            ProfileStorageRow.ColumnCount, headers, ProfileStorageRow.AddParameters, MaxProfilesPerBatch, ct);

        // Insert child rows — collect across all profiles, insert per table
        await InsertRowsBatchedAsync(tx, "profile_functions",
            "profile_id, ordinal, name, system_name, filename, start_line", 6,
            results.SelectMany(static r => r.Functions).ToList(),
            static (cmd, f) =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = f.ProfileId });
                cmd.Parameters.Add(new DuckDBParameter { Value = f.Ordinal });
                cmd.Parameters.Add(new DuckDBParameter { Value = f.Name ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = f.SystemName ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = f.Filename ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = f.StartLine ?? (object)DBNull.Value });
            }, 200, ct);

        await InsertRowsBatchedAsync(tx, "profile_locations",
            "profile_id, ordinal, mapping_ordinal, address, lines_json", 5,
            results.SelectMany(static r => r.Locations).ToList(),
            static (cmd, l) =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = l.ProfileId });
                cmd.Parameters.Add(new DuckDBParameter { Value = l.Ordinal });
                cmd.Parameters.Add(new DuckDBParameter { Value = l.MappingOrdinal ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = l.Address is { } a ? (decimal)a : DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = l.LinesJson ?? (object)DBNull.Value });
            }, 200, ct);

        await InsertRowsBatchedAsync(tx, "profile_mappings",
            "profile_id, ordinal, filename, memory_start, memory_limit, file_offset", 6,
            results.SelectMany(static r => r.Mappings).ToList(),
            static (cmd, m) =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = m.ProfileId });
                cmd.Parameters.Add(new DuckDBParameter { Value = m.Ordinal });
                cmd.Parameters.Add(new DuckDBParameter { Value = m.Filename ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter
                {
                    Value = m.MemoryStart is { } ms ? (decimal)ms : DBNull.Value
                });
                cmd.Parameters.Add(new DuckDBParameter
                {
                    Value = m.MemoryLimit is { } ml ? (decimal)ml : DBNull.Value
                });
                cmd.Parameters.Add(new DuckDBParameter { Value = m.FileOffset is { } fo ? (decimal)fo : DBNull.Value });
            }, 200, ct);

        await InsertRowsBatchedAsync(tx, "profile_samples",
            "profile_id, ordinal, stack_ordinal, link_trace_id, link_span_id, values_json, timestamps_json", 7,
            results.SelectMany(static r => r.Samples).ToList(),
            static (cmd, s) =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = s.ProfileId });
                cmd.Parameters.Add(new DuckDBParameter { Value = s.Ordinal });
                cmd.Parameters.Add(new DuckDBParameter { Value = s.StackOrdinal ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = s.LinkTraceId ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = s.LinkSpanId ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = s.ValuesJson ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = s.TimestampsJson ?? (object)DBNull.Value });
            }, 200, ct);

        await InsertRowsBatchedAsync(tx, "profile_stacks", "profile_id, ordinal, location_ordinals_json", 3,
            results.SelectMany(static r => r.Stacks).ToList(),
            static (cmd, st) =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = st.ProfileId });
                cmd.Parameters.Add(new DuckDBParameter { Value = st.Ordinal });
                cmd.Parameters.Add(new DuckDBParameter { Value = st.LocationOrdinalsJson ?? (object)DBNull.Value });
            }, 200, ct);

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private async Task InsertRowsBatchedAsync<T>(
        DbTransaction tx, string table, string columns, int colCount,
        IReadOnlyList<T> rows, Action<DuckDBCommand, T> addParams, int maxBatch, CancellationToken ct)
    {
        if (rows.Count is 0) return;
        var offset = 0;
        while (offset < rows.Count)
        {
            var chunk = Math.Min(maxBatch, rows.Count - offset);
            await using var cmd = Connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = BuildMultiRowInsertSql(table, columns, colCount, chunk);
            for (var i = 0; i < chunk; i++) addParams(cmd, rows[offset + i]);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            offset += chunk;
        }
    }

    public async Task<IReadOnlyList<ProfileStorageRow>> GetProfilesAsync(
        string? sessionId = null,
        string? traceId = null,
        string? spanId = null,
        string? serviceName = null,
        string? sampleType = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var profiles = new List<ProfileStorageRow>();
        var qb = new QueryBuilder();

        if (!string.IsNullOrEmpty(sessionId))
            qb.Add("session_id = $N", sessionId);
        if (!string.IsNullOrEmpty(traceId))
            qb.Add("trace_id = $N", traceId);
        if (!string.IsNullOrEmpty(spanId))
            qb.Add("span_id = $N", spanId);
        if (!string.IsNullOrEmpty(serviceName))
            qb.Add("service_name = $N", serviceName);
        if (!string.IsNullOrEmpty(sampleType))
            qb.Add("sample_type = $N", sampleType);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT profile_id, trace_id, span_id, session_id,"
                          + " time_unix_nano, duration_nano, sample_count,"
                          + " sample_type, sample_unit, original_payload_format,"
                          + " service_name, profile_frame_type,"
                          + " attributes_json, resource_json,"
                          + " schema_url, created_at"
                          + " FROM profiles " + qb.WhereClause
                          + " ORDER BY time_unix_nano DESC LIMIT "
                          + qb.NextParam.ToString(CultureInfo.InvariantCulture);

        qb.ApplyTo(cmd);
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 500) });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            profiles.Add(MapProfile(reader));
        }

        return profiles;
    }

    public async Task<ProfileDetail?> GetProfileDetailAsync(string profileId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        // Header
        ProfileStorageRow? header = null;
        await using (var cmd = lease.Connection.CreateCommand())
        {
            cmd.CommandText =
                "SELECT profile_id, trace_id, span_id, session_id, time_unix_nano, duration_nano, sample_count, sample_type, sample_unit, original_payload_format, service_name, profile_frame_type, attributes_json, resource_json, schema_url, created_at FROM profiles WHERE profile_id = $1 LIMIT 1";
            cmd.Parameters.Add(new DuckDBParameter { Value = profileId });
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await r.ReadAsync(ct).ConfigureAwait(false)) header = MapProfile(r);
        }

        if (header is null) return null;

        // Child tables — all keyed by profile_id
        var functions = await ReadChildRows(lease.Connection, profileId,
            "SELECT profile_id, ordinal, name, system_name, filename, start_line FROM profile_functions WHERE profile_id = $1 ORDER BY ordinal",
            static r => new ProfileFunctionRow
            {
                ProfileId = r.GetString(0),
                Ordinal = r.Col(1).GetInt32(0),
                Name = r.Col(2).AsString,
                SystemName = r.Col(3).AsString,
                Filename = r.Col(4).AsString,
                StartLine = r.IsDBNull(5) ? null : r.GetInt64(5)
            }, ct);

        var locations = await ReadChildRows(lease.Connection, profileId,
            "SELECT profile_id, ordinal, mapping_ordinal, address, lines_json FROM profile_locations WHERE profile_id = $1 ORDER BY ordinal",
            static r => new ProfileLocationRow
            {
                ProfileId = r.GetString(0),
                Ordinal = r.Col(1).GetInt32(0),
                MappingOrdinal = r.IsDBNull(2) ? null : r.Col(2).GetInt32(0),
                Address = r.IsDBNull(3) ? null : r.Col(3).GetUInt64(0),
                LinesJson = r.Col(4).AsString
            }, ct);

        var mappings = await ReadChildRows(lease.Connection, profileId,
            "SELECT profile_id, ordinal, filename, memory_start, memory_limit, file_offset FROM profile_mappings WHERE profile_id = $1 ORDER BY ordinal",
            static r => new ProfileMappingRow
            {
                ProfileId = r.GetString(0),
                Ordinal = r.Col(1).GetInt32(0),
                Filename = r.Col(2).AsString,
                MemoryStart = r.IsDBNull(3) ? null : r.Col(3).GetUInt64(0),
                MemoryLimit = r.IsDBNull(4) ? null : r.Col(4).GetUInt64(0),
                FileOffset = r.IsDBNull(5) ? null : r.Col(5).GetUInt64(0)
            }, ct);

        var samples = await ReadChildRows(lease.Connection, profileId,
            "SELECT profile_id, ordinal, stack_ordinal, link_trace_id, link_span_id, values_json, timestamps_json FROM profile_samples WHERE profile_id = $1 ORDER BY ordinal",
            static r => new ProfileSampleRow
            {
                ProfileId = r.GetString(0),
                Ordinal = r.Col(1).GetInt32(0),
                StackOrdinal = r.IsDBNull(2) ? null : r.Col(2).GetInt32(0),
                LinkTraceId = r.Col(3).AsString,
                LinkSpanId = r.Col(4).AsString,
                ValuesJson = r.Col(5).AsString,
                TimestampsJson = r.Col(6).AsString
            }, ct);

        var stacks = await ReadChildRows(lease.Connection, profileId,
            "SELECT profile_id, ordinal, location_ordinals_json FROM profile_stacks WHERE profile_id = $1 ORDER BY ordinal",
            static r => new ProfileStackRow
            {
                ProfileId = r.GetString(0), Ordinal = r.Col(1).GetInt32(0), LocationOrdinalsJson = r.Col(2).AsString
            }, ct);

        return new ProfileDetail
        {
            Profile = header,
            Functions = functions,
            Locations = locations,
            Mappings = mappings,
            Samples = samples,
            Stacks = stacks
        };
    }

    private static async Task<IReadOnlyList<T>> ReadChildRows<T>(DuckDBConnection connection, string profileId,
        string sql, Func<DbDataReader, T> mapper, CancellationToken ct)
    {
        var rows = new List<T>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new DuckDBParameter { Value = profileId });
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) rows.Add(mapper(r));
        return rows;
    }

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
        cmd.Parameters.Add(new DuckDBParameter { Value = string.IsNullOrWhiteSpace(error.UserId) ? DBNull.Value : 1L });
        cmd.Parameters.Add(new DuckDBParameter { Value = error.ServiceName });
        cmd.Parameters.Add(new DuckDBParameter { Value = "new" });
        cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = error.TraceId });
    }

    /// <summary>
    ///     Upserts an error event. On fingerprint conflict, increments occurrence_count,
    ///     updates last_seen, merges affected_services, and appends to sample_traces (max 10).
    /// </summary>
    public async Task UpsertErrorAsync(ErrorEvent error, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            var now = TimeProvider.System.GetUtcNow().UtcDateTime;
            await using var cmd = con.CreateCommand();
            cmd.CommandText = ErrorUpsertSql;
            AddErrorUpsertParameters(cmd, error, Guid.NewGuid().ToString("N"), now);
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

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
        var qb = new QueryBuilder();

        if (!string.IsNullOrEmpty(category))
            qb.Add("category = $N", category);
        if (!string.IsNullOrEmpty(status))
            qb.Add("status = $N", status);
        if (!string.IsNullOrEmpty(serviceName))
        {
            qb.Add("',' || affected_services || ',' LIKE $N ESCAPE '\'", $"%,{SqlLikeEscape.Escape(serviceName)},%");
        }

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT error_id, error_type, message, category, fingerprint,"
                          + " first_seen, last_seen, occurrence_count,"
                          + " affected_users, affected_services, status,"
                          + " assigned_to, issue_url, sample_traces"
                          + " FROM errors " + qb.WhereClause
                          + " ORDER BY last_seen DESC LIMIT "
                          + qb.NextParam.ToString(CultureInfo.InvariantCulture);

        qb.ApplyTo(cmd);
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
            byCategory.Add(new ErrorCategoryStat { Category = reader.GetString(0), Count = count });
        }

        return new ErrorStats { TotalCount = totalCount, ByCategory = byCategory };
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
            AffectedUserIds = reader.Col(8).AsInt64?.ToString(CultureInfo.InvariantCulture),
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
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = assignedTo is not null
                ? "UPDATE errors SET status = $1, assigned_to = $2 WHERE error_id = $3"
                : "UPDATE errors SET status = $1 WHERE error_id = $2";

            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            if (assignedTo is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = assignedTo });
            cmd.Parameters.Add(new DuckDBParameter { Value = errorId });

            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

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
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(
            (con, token) => ArchiveInternalAsync(con, outputDirectory, olderThan, token),
            ct).ConfigureAwait(false);

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
        catch (OperationCanceledException ex)
        {
            // Expected during shutdown.
            Debug.WriteLine(ex);
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
        IEnumerable<SpanStorageRow> spans,
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
        var cutoffNano = (ulong)cutoffMs * 1_000_000UL;
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
            exportCmd.CommandText = "COPY (SELECT * FROM spans WHERE start_time_unix_nano < $1)"
                                    + " TO '" + tempPath + "'"
                                    + " (FORMAT PARQUET, COMPRESSION ZSTD, ROW_GROUP_SIZE 100000)";
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
    ///     Builds a multi-row INSERT SQL statement for any table.
    /// </summary>
    private static string BuildMultiRowInsertSql(string table, string columns, int colCount, int rowCount)
    {
        var sb = new StringBuilder(256);
        sb.Append("INSERT INTO ").Append(table).Append(" (").Append(columns).Append(") VALUES ");
        for (var i = 0; i < rowCount; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('(');
            for (var c = 0; c < colCount; c++)
            {
                if (c > 0) sb.Append(", ");
                sb.Append('$').Append((i * colCount) + c + 1);
            }

            sb.Append(')');
        }

        return sb.ToString();
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
        cmd.Parameters.Add(new DuckDBParameter { Value = log.SourceFile ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.SourceLine ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.SourceColumn ?? (object)DBNull.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = log.SourceMethod ?? (object)DBNull.Value });
    }

    // ==========================================================================
    // Private Methods - Reading
    // ==========================================================================

    private async Task<T?> ReadOneAsync<T>(
        string sql, Action<DuckDBCommand>? addParams, Func<DbDataReader, T> mapper,
        CancellationToken ct = default) where T : class
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = sql;
        addParams?.Invoke(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? mapper(reader) : null;
    }

    private async Task<IReadOnlyList<T>> ReadManyAsync<T>(
        string sql, Action<DuckDBCommand>? addParams, Func<DbDataReader, T> mapper,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = sql;
        addParams?.Invoke(cmd);
        var results = new List<T>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(mapper(reader));
        return results;
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

    // ==========================================================================
    // Private Methods - Schema & Mapping
    // ==========================================================================

    private static void InitializeSchema(DuckDBConnection con)
    {
        // Manual DDLs run BEFORE generated schema so CREATE TABLE IF NOT EXISTS in generated DDL is a no-op
        using var manualFixRunsCmd = con.CreateCommand();
        manualFixRunsCmd.CommandText = DuckDbSchema.ManualFixRunsDdl;
        manualFixRunsCmd.ExecuteNonQuery();

        using var manualLogsCmd = con.CreateCommand();
        manualLogsCmd.CommandText = DuckDbSchema.ManualLogsDdl;
        manualLogsCmd.ExecuteNonQuery();

        // OTel Profiles (v1development) — normalized: 6 tables
        using var profilesCmd = con.CreateCommand();
        profilesCmd.CommandText = string.Concat(
            DuckDbSchema.ProfilesDdl, "\n",
            DuckDbSchema.ProfileFunctionsDdl, "\n",
            DuckDbSchema.ProfileLocationsDdl, "\n",
            DuckDbSchema.ProfileMappingsDdl, "\n",
            DuckDbSchema.ProfileSamplesDdl, "\n",
            DuckDbSchema.ProfileStacksDdl, "\n",
            DuckDbSchema.ProfilesIndexesDdl);
        profilesCmd.ExecuteNonQuery();

        using var cmd = con.CreateCommand();
        cmd.CommandText = NormalizeGeneratedSchemaDdl(DuckDbSchema.GetSchemaDdl());
        cmd.ExecuteNonQuery();

        // Manual schema extensions (not yet in TypeSpec)
        using var extCmd = con.CreateCommand();
        extCmd.CommandText = DuckDbSchema.WorkflowExecutionsDdl;
        extCmd.ExecuteNonQuery();

        using var workflowRunsCmd = con.CreateCommand();
        workflowRunsCmd.CommandText = string.Concat(
            DuckDbSchema.WorkflowRunsV2Ddl, "\n",
            DuckDbSchema.WorkflowNodesV2Ddl, "\n",
            DuckDbSchema.WorkflowCheckpointsV2Ddl, "\n",
            DuckDbSchema.WorkflowEventsV2Ddl);
        workflowRunsCmd.ExecuteNonQuery();

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

        // Identity domain: workspaces, projects, environments, handshake challenges
        using var identityCmd = con.CreateCommand();
        identityCmd.CommandText = string.Concat(
            DuckDbSchema.WorkspacesDdl, "\n",
            DuckDbSchema.ProjectsDdl, "\n",
            DuckDbSchema.ProjectEnvironmentsDdl, "\n",
            DuckDbSchema.HandshakeChallengesDdl, "\n",
            DuckDbSchema.GitHubTokensDdl);
        identityCmd.ExecuteNonQuery();

        // Provisioning domain: config selections, generation jobs
        using var provisioningCmd = con.CreateCommand();
        provisioningCmd.CommandText = string.Concat(
            DuckDbSchema.ConfigSelectionsDdl, "\n",
            DuckDbSchema.GenerationJobsDdl);
        provisioningCmd.ExecuteNonQuery();

        // Issue lifecycle events
        using var issueEventsCmd = con.CreateCommand();
        issueEventsCmd.CommandText = IssueEventsDdl;
        issueEventsCmd.ExecuteNonQuery();

        // Agent audit tables: runs, tool calls, and decision events
        using var agentRunsCmd = con.CreateCommand();
        agentRunsCmd.CommandText = string.Concat(
            DuckDbSchema.AgentRunsDdl, "\n",
            DuckDbSchema.ToolCallsDdl, "\n",
            DuckDbSchema.AgentDecisionsDdl);
        agentRunsCmd.ExecuteNonQuery();

        // Semantic span clusters (Phase 5 — EmbeddingClusterWorker)
        using var spanClustersCmd = con.CreateCommand();
        spanClustersCmd.CommandText = DuckDbSchema.SpanClustersDdl;
        spanClustersCmd.ExecuteNonQuery();

        // Coding agent runs + Loom settings
        using var codingAgentCmd = con.CreateCommand();
        codingAgentCmd.CommandText = string.Concat(
            DuckDbSchema.CodingAgentRunsDdl, "\n",
            DuckDbSchema.LoomSettingsDdl);
        codingAgentCmd.ExecuteNonQuery();

        // Loom triage pipeline
        using var triageCmd = con.CreateCommand();
        triageCmd.CommandText = DuckDbSchema.TriageResultsDdl;
        triageCmd.ExecuteNonQuery();

        // Autofix step tracking (within fix runs)
        using var autofixStepsCmd = con.CreateCommand();
        autofixStepsCmd.CommandText = DuckDbSchema.AutofixStepsDdl;
        autofixStepsCmd.ExecuteNonQuery();

        // Agent handoffs
        using var handoffsCmd = con.CreateCommand();
        handoffsCmd.CommandText = DuckDbSchema.AgentHandoffsDdl;
        handoffsCmd.ExecuteNonQuery();

        // GitHub events
        using var githubEventsCmd = con.CreateCommand();
        githubEventsCmd.CommandText = DuckDbSchema.GitHubEventsDdl;
        githubEventsCmd.ExecuteNonQuery();

        // Schema promotions
        using var schemaPromotionsCmd = con.CreateCommand();
        schemaPromotionsCmd.CommandText = DuckDbSchema.SchemaPromotionsDdl;
        schemaPromotionsCmd.ExecuteNonQuery();

        // Service registry (telemetry-derived auto-detection)
        using var serviceRegistryCmd = con.CreateCommand();
        serviceRegistryCmd.CommandText = ServiceInstancesDdl;
        serviceRegistryCmd.ExecuteNonQuery();

        using var servicesViewCmd = con.CreateCommand();
        servicesViewCmd.CommandText = ServicesViewDdl;
        servicesViewCmd.ExecuteNonQuery();

        // Artifact storage
        using var artifactsCmd = con.CreateCommand();
        artifactsCmd.CommandText = DuckDbSchema.ArtifactsDdl;
        artifactsCmd.ExecuteNonQuery();

        // Cost engine: model pricing tables + pre-aggregated view
        using var costCmd = con.CreateCommand();
        costCmd.CommandText = string.Concat(
            DuckDbSchema.ModelPricingDdl, "\n",
            DuckDbSchema.ModelPricingTiersDdl, "\n",
            DuckDbSchema.CostByModelHourlyViewDdl);
        costCmd.ExecuteNonQuery();
    }

    private static string NormalizeGeneratedSchemaDdl(string ddl)
    {
        var statements = ddl.Split(";\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var normalized = new List<string>(statements.Length);

        foreach (var statement in statements)
        {
            if (!statement.StartsWithOrdinal("CREATE TABLE IF NOT EXISTS "))
            {
                normalized.Add(statement);
                continue;
            }

            var lines = statement.Split('\n').ToList();
            var createdAtIndexes = new List<int>();

            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimStart().StartsWithIgnoreCase("created_at "))
                {
                    createdAtIndexes.Add(i);
                }
            }

            if (createdAtIndexes.Count > 1)
            {
                foreach (var index in createdAtIndexes.Take(createdAtIndexes.Count - 1).OrderDescending())
                {
                    lines.RemoveAt(index);
                }
            }

            normalized.Add(string.Join('\n', lines));
        }

        return string.Join(";\n", normalized) + ";\n";
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
            SourceFile = reader.Col(12).AsString,
            SourceLine = reader.Col(13).AsInt32,
            SourceColumn = reader.Col(14).AsInt32,
            SourceMethod = reader.Col(15).AsString,
            CreatedAt = reader.Col(16).AsDateTimeOffset
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ProfileStorageRow MapProfile(DbDataReader reader) =>
        new()
        {
            ProfileId = reader.GetString(0),
            TraceId = reader.Col(1).AsString,
            SpanId = reader.Col(2).AsString,
            SessionId = reader.Col(3).AsString,
            TimeUnixNano = reader.Col(4).GetUInt64(0),
            DurationNano = reader.Col(5).GetUInt64(0),
            SampleCount = reader.Col(6).GetInt32(0),
            SampleType = reader.Col(7).AsString,
            SampleUnit = reader.Col(8).AsString,
            OriginalPayloadFormat = reader.Col(9).AsString,
            ServiceName = reader.Col(10).AsString,
            ProfileFrameType = reader.Col(11).AsString,
            AttributesJson = reader.Col(12).AsString,
            ResourceJson = reader.Col(13).AsString,
            SchemaUrl = reader.Col(14).AsString,
            CreatedAt = reader.Col(15).AsDateTimeOffset
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
        Guard.NotNullOrWhiteSpace(outputDirectory);

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
            if (fullPath.StartsWithIgnoreCase(prefix))
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

    /// <summary>
    ///     Lightweight query builder for dynamic DuckDB WHERE clauses with positional parameters.
    /// </summary>
    private struct QueryBuilder()
    {
        private readonly List<string> _conditions = [];
        private readonly List<DuckDBParameter> _parameters = [];
        private int _paramIndex = 1;

        /// <summary>Adds a condition with a single positional parameter. $N is replaced with the next index.</summary>
        public void Add(string condition, object value)
        {
            _conditions.Add(condition.Replace("$N", $"${_paramIndex++}"));
            _parameters.Add(new DuckDBParameter { Value = value });
        }

        /// <summary>Adds a condition with no parameters (e.g. IS NOT NULL checks).</summary>
        public readonly void AddCondition(string condition) => _conditions.Add(condition);

        public readonly string WhereClause =>
            _conditions.Count > 0 ? $"WHERE {string.Join(" AND ", _conditions)}" : "";

        public readonly string NextParam => $"${_paramIndex}";

        public readonly void ApplyTo(DuckDBCommand cmd) => cmd.Parameters.AddRange(_parameters);
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
                catch (DuckDBException ex)
                {
                    // Best effort cleanup — connection may already be disposed.
                    Debug.WriteLine(ex);
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
/// <param name="ProfilesDeleted">Number of profiles deleted.</param>
/// <param name="SessionsDeleted">Number of sessions deleted.</param>
public sealed record ClearTelemetryResult(int SpansDeleted, int LogsDeleted, int ProfilesDeleted, int SessionsDeleted)
{
    /// <summary>Total number of records deleted across all tables.</summary>
    public int TotalDeleted => SpansDeleted + LogsDeleted + ProfilesDeleted + SessionsDeleted;
}
