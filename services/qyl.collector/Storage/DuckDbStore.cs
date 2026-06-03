using DuckDB.NET.Data;
using Qyl.Collector.Telemetry;

using static System.Threading.Volatile;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore : IAsyncDisposable
{

    private const int MaxSpansPerBatch = 100;

    private const int MaxLogsPerBatch = 150;

    private const int LogColumnCount = 12;

    private const string LogColumnList = """
                                         log_id, trace_id, span_id, session_id,
                                         time_unix_nano, observed_time_unix_nano,
                                         severity_number, severity_text, body,
                                         service_name, attributes_json, resource_json
                                         """;

    private const string SelectSpanColumns = """
                                             span_id, trace_id, parent_span_id, session_id,
                                             name, kind, start_time_unix_nano, end_time_unix_nano, duration_ns,
                                             status_code, service_name,
                                             gen_ai_provider_name, gen_ai_request_model, gen_ai_response_model,
                                             gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_temperature,
                                             gen_ai_stop_reason, gen_ai_tool_name,
                                             gen_ai_cost_usd, attributes_json, resource_json,
                                             schema_url, created_at
                                             """;

    private const int MaxProfilesPerBatch = 50;
    private static readonly TimeSpan s_shutdownTimeout = TimeSpan.FromSeconds(5);

    private static readonly ConcurrentDictionary<int, string> s_logInsertSqlCache = new();

    private readonly CancellationTokenSource _cts = new();
    private readonly Counter<long> _droppedJobs;
    private readonly Counter<long> _droppedSpans;
    private readonly bool _isInMemory;
    private readonly int _jobQueueCapacity;
    private readonly Channel<WriteJob> _jobs;

    private readonly Meter _meter = new(QylTelemetry.StorageMeterName, QylTelemetry.ServiceVersion);

    private readonly Channel<IReadJob>? _reads;
    private readonly Thread[] _readerThreads;
    private readonly Task _writerTask;

    private long _droppedJobCount;
    private long _droppedSpanCount;
    private int _disposed;
    private long _queuedWriteJobs;


    public DuckDbStore(
        string databasePath = "qyl.duckdb",
        int jobQueueCapacity = 1000,
        int maxConcurrentReads = 8,
        int readQueueCapacity = 1000)
    {
        DatabasePath = databasePath;
        _isInMemory = databasePath == ":memory:";
        _jobQueueCapacity = Math.Max(1, jobQueueCapacity);
        Connection = new DuckDBConnection($"DataSource={databasePath}");
        Connection.Open();
        InitializeSchema(Connection);

        _droppedJobs = _meter.CreateCounter<long>(Duckdb.DroppedJobsTotal);
        _droppedSpans = _meter.CreateCounter<long>(Duckdb.DroppedSpansTotal);

        _jobs = Channel.CreateBounded<WriteJob>(new BoundedChannelOptions(_jobQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false
        });
        _writerTask = Task.Run(WriterLoopAsync);

        // DuckDB.NET's *Async methods are synchronous-over-async: the embedded engine has no IO to
        // await, so every read blocks its calling thread for the full query. To keep that blocking
        // off the shared thread pool (Kestrel), reads run on a dedicated set of OS threads, each with
        // its own READ_ONLY connection (DuckDB MVCC allows concurrent readers). In-memory mode shares
        // the single writer connection, so its reads are serialized through the writer instead.
        if (_isInMemory)
        {
            _readerThreads = [];
            _reads = null;
        }
        else
        {
            var concurrency = Math.Max(1, maxConcurrentReads);
            _reads = Channel.CreateBounded<IReadJob>(new BoundedChannelOptions(readQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait, SingleReader = false, SingleWriter = false
            });

            // Open every reader connection up front (on this thread) so a failure surfaces loudly and
            // immediately as a startup error, not asynchronously on a background thread.
            var connections = new DuckDBConnection[concurrency];
            for (var i = 0; i < concurrency; i++)
            {
                var con = new DuckDBConnection($"DataSource={databasePath};ACCESS_MODE=READ_ONLY");
                con.Open();
                connections[i] = con;
            }

            _readerThreads = new Thread[concurrency];
            for (var i = 0; i < concurrency; i++)
            {
                var con = connections[i];
                var thread = new Thread(() => ReaderLoop(con)) { IsBackground = true, Name = $"duckdb-reader-{i}" };
                thread.Start();
                _readerThreads[i] = thread;
            }
        }
    }


    private DuckDBConnection Connection { get; }

    private string DatabasePath { get; }


    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            return;

        List<Exception>? shutdownErrors = null;

        _jobs.Writer.TryComplete();
        _reads?.Writer.TryComplete();

        // Unblock the reader threads (their WaitToReadAsync observes the token) and any stuck writer,
        // then let each reader dispose its own connection and exit.
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _writerTask.WaitAsync(s_shutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            AddShutdownError(ref shutdownErrors, new TimeoutException("DuckDB writer did not stop before shutdown timeout.", ex));
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the normal shutdown signal.
        }
        catch (Exception ex)
        {
            AddShutdownError(ref shutdownErrors, ex);
        }

        foreach (var thread in _readerThreads)
        {
            if (!thread.Join(s_shutdownTimeout))
            {
                AddShutdownError(
                    ref shutdownErrors,
                    new TimeoutException($"DuckDB reader thread '{thread.Name}' did not stop before shutdown timeout."));
            }
        }

        if (_reads is not null)
            while (_reads.Reader.TryRead(out var leftover))
                leftover.Cancel();

        Connection.Dispose();
        _cts.Dispose();
        _meter.Dispose();

        if (shutdownErrors is { Count: > 0 })
            throw new AggregateException("DuckDB store did not shut down cleanly.", shutdownErrors);
    }


    private async Task<T> ExecuteReadAsync<T>(Func<DuckDBConnection, T> read, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        // In-memory mode shares the single writer connection (not safe for concurrent use), so its
        // reads are serialized through the writer queue rather than the dedicated reader pool.
        if (_reads is null)
            return await ExecuteWriteAsync((con, _) => new ValueTask<T>(read(con)), ct).ConfigureAwait(false);

        var job = new ReadJob<T>(read, ct);
        await _reads.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }


    private async Task<T> ExecuteWriteAsync<T>(Func<DuckDBConnection, CancellationToken, ValueTask<T>> operation,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<T>(operation);
        Interlocked.Increment(ref _queuedWriteJobs);
        try
        {
            await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Decrement(ref _queuedWriteJobs);
            throw;
        }

        return await job.Task.ConfigureAwait(false);
    }

    private async Task ExecuteWriteAsync(Func<DuckDBConnection, CancellationToken, ValueTask> operation,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await operation(con, token).ConfigureAwait(false);
            return 0;
        });
        Interlocked.Increment(ref _queuedWriteJobs);
        try
        {
            await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Decrement(ref _queuedWriteJobs);
            throw;
        }

        await job.Task.ConfigureAwait(false);
    }


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
            RecordDroppedSpans);

        Interlocked.Increment(ref _queuedWriteJobs);
        if (!_jobs.Writer.TryWrite(job))
        {
            Interlocked.Decrement(ref _queuedWriteJobs);
            job.OnAborted(new InvalidOperationException("The DuckDB write queue is full."));
        }

        return ValueTask.CompletedTask;
    }

    public Task<IReadOnlyList<SpanStorageRow>> GetSpansBySessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<SpanStorageRow>>(con =>
        {
            var spans = new List<SpanStorageRow>();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT " + SelectSpanColumns
                                        + " FROM spans WHERE session_id = $1 ORDER BY start_time_unix_nano ASC";
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                spans.Add(MapSpan(reader));

            return spans;
        }, ct);
    }

    public Task<IReadOnlyList<SpanStorageRow>> GetTraceAsync(string traceId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<SpanStorageRow>>(con =>
        {
            var spans = new List<SpanStorageRow>();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT " + SelectSpanColumns
                                        + " FROM spans WHERE trace_id = $1 ORDER BY start_time_unix_nano ASC";
            cmd.Parameters.Add(new DuckDBParameter { Value = traceId });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                spans.Add(MapSpan(reader));

            return spans;
        }, ct);
    }

    public Task<IReadOnlyList<SpanStorageRow>> GetSpansAsync(
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
        return ExecuteReadAsync<IReadOnlyList<SpanStorageRow>>(con =>
        {
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
                qb.Add("(name ILIKE $N OR attributes_json ILIKE $N)", $"%{searchText}%");

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT " + SelectSpanColumns
                                        + " FROM spans " + qb.WhereClause
                                        + " ORDER BY start_time_unix_nano DESC LIMIT "
                                        + qb.NextParam.ToString(CultureInfo.InvariantCulture);

            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                spans.Add(MapSpan(reader));

            return spans;
        }, ct);
    }


    public Task<StorageStats> GetStorageStatsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT
                                  (SELECT COUNT(*) FROM spans) as span_count,
                                  (SELECT COUNT(DISTINCT COALESCE(session_id, trace_id)) FROM spans) as session_count,
                                  (SELECT COUNT(*) FROM logs) as log_count,
                                  (SELECT MIN(start_time_unix_nano) FROM spans) as oldest_span,
                                  (SELECT MAX(start_time_unix_nano) FROM spans) as newest_span
                              """;

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new StorageStats
                {
                    SpanCount = reader.Col(0).GetInt64(0),
                    SessionCount = reader.Col(1).GetInt64(0),
                    LogCount = reader.Col(2).GetInt64(0),
                    DroppedSpanCount = Read(ref _droppedSpanCount),
                    DroppedJobCount = Read(ref _droppedJobCount),
                    WriteQueueUtilization = GetWriteQueueUtilization(),
                    OldestSpanTime = reader.Col(3).AsUInt64,
                    NewestSpanTime = reader.Col(4).AsUInt64
                };
            }

            return new StorageStats();
        }, ct);
    }

    private void RecordDroppedSpans(int spanCount)
    {
        Interlocked.Increment(ref _droppedJobCount);
        Interlocked.Add(ref _droppedSpanCount, spanCount);
        _droppedJobs.Add(1);
        _droppedSpans.Add(spanCount);
    }

    private double GetWriteQueueUtilization()
    {
        var queued = Math.Max(0, Read(ref _queuedWriteJobs));
        return Math.Clamp((double)queued / _jobQueueCapacity, 0, 1);
    }

    public long GetStorageSizeBytes()
    {
        if (Read(ref _disposed) is not 0)
            return 0;

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
                Debug.WriteLine(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine(ex);
            }

            return 0;
        }

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
            Debug.WriteLine(ex);
        }

        return 0;
    }


    public Task<long> GetSpanCountAsync(CancellationToken ct = default) =>
        ExecuteReadAsync(static con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM spans";
            var result = cmd.ExecuteScalar();
            return result is long count ? count : 0;
        }, ct);

    public Task<long> GetLogCountAsync(CancellationToken ct = default) =>
        ExecuteReadAsync(static con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM logs";
            var result = cmd.ExecuteScalar();
            return result is long count ? count : 0;
        }, ct);

    public Task<long> GetModelPricingCountAsync(CancellationToken ct = default) =>
        ExecuteReadAsync(static con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM model_pricing";
            var result = cmd.ExecuteScalar();
            return result switch
            {
                long value => value,
                int value => value,
                _ => 0
            };
        }, ct);

    public Task<IReadOnlyList<ModelPricingEntry>> GetActiveModelPricingAsync(CancellationToken ct = default) =>
        ExecuteReadAsync<IReadOnlyList<ModelPricingEntry>>(static con =>
        {
            var result = new List<ModelPricingEntry>();

            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT provider, model, input_cost, output_cost, reasoning_cost,
                                     cache_read_cost, cache_write_cost
                              FROM model_pricing
                              WHERE valid_to IS NULL
                              ORDER BY valid_from DESC
                              """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new ModelPricingEntry(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetDecimal(2),
                    reader.GetDecimal(3),
                    reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                    reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    reader.IsDBNull(6) ? null : reader.GetDecimal(6)));
            }

            return result;
        }, ct);

    public async Task InsertModelPricingSeedsAsync(
        IReadOnlyList<ModelPricingSeed> entries,
        DateTime validFrom,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, wct) =>
        {
            foreach (var entry in entries)
            {
                await using var cmd = con.CreateCommand();
                cmd.CommandText = """
                                  INSERT INTO model_pricing
                                      (provider, model, input_cost, output_cost, reasoning_cost,
                                       cache_read_cost, cache_write_cost, valid_from)
                                  VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                                  ON CONFLICT DO NOTHING
                                  """;
                cmd.Parameters.Add(new DuckDBParameter { Value = entry.Provider });
                cmd.Parameters.Add(new DuckDBParameter { Value = entry.Model });
                cmd.Parameters.Add(new DuckDBParameter { Value = entry.InputCost });
                cmd.Parameters.Add(new DuckDBParameter { Value = entry.OutputCost });
                cmd.Parameters.Add(new DuckDBParameter { Value = (object?)entry.ReasoningCost ?? DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = (object?)entry.CacheReadCost ?? DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = (object?)entry.CacheWriteCost ?? DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = validFrom });
                await cmd.ExecuteNonQueryAsync(wct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);

    private static void AddShutdownError(ref List<Exception>? errors, Exception error)
    {
        errors ??= [];
        errors.Add(error);
        Debug.WriteLine(error);
    }

    public async Task InsertLogsAsync(IReadOnlyList<LogStorageRow> logs, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (logs.Count is 0)
            return;

        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);

            var totalLogs = logs.Count;
            var offset = 0;

            while (offset < totalLogs)
            {
                var chunkSize = Math.Min(MaxLogsPerBatch, totalLogs - offset);
                var sql = BuildMultiRowLogInsertSql(chunkSize);

                await using var cmd = con.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;

                for (var i = 0; i < chunkSize; i++)
                {
                    AddLogParameters(cmd, logs[offset + i]);
                }

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                offset += chunkSize;
            }

            await tx.CommitAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<LogStorageRow>> GetLogsAsync(
        string? sessionId = null,
        string? traceId = null,
        string? severityText = null,
        int? minSeverity = null,
        string? search = null,
        ulong? after = null,
        string? afterLogId = null,
        ulong? before = null,
        string? serviceName = null,
        bool ascending = false,
        int limit = 500,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<LogStorageRow>>(con =>
        {
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
            if (!string.IsNullOrWhiteSpace(search))
                qb.Add("(body ILIKE $N OR severity_text ILIKE $N OR service_name ILIKE $N OR attributes_json ILIKE $N)",
                    $"%{search}%");
            if (!string.IsNullOrEmpty(serviceName))
                qb.Add("service_name = $N", serviceName);
            if (after.HasValue)
            {
                if (string.IsNullOrEmpty(afterLogId))
                    throw new ArgumentException(
                        "A log id tie-breaker is required when querying after a timestamp.",
                        nameof(afterLogId));

                qb.Add(
                    "(time_unix_nano > $N OR (time_unix_nano = $N AND log_id > $N))",
                    (decimal)after.Value,
                    (decimal)after.Value,
                    afterLogId);
            }

            if (before.HasValue)
                qb.Add("time_unix_nano <= $N", (decimal)before.Value);

            using var cmd = con.CreateCommand();
            var sortDirection = ascending ? "ASC" : "DESC";
            cmd.CommandText = "SELECT log_id, trace_id, span_id, session_id,"
                              + " time_unix_nano, observed_time_unix_nano,"
                              + " severity_number, severity_text, body,"
                              + " service_name, attributes_json, resource_json,"
                              + " created_at"
                              + " FROM logs " + qb.WhereClause
                              + " ORDER BY time_unix_nano " + sortDirection + ", log_id " + sortDirection + " LIMIT "
                              + qb.NextParam.ToString(CultureInfo.InvariantCulture);

            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                logs.Add(MapLog(reader));

            return logs;
        }, ct);
    }


    public async Task InsertProfilesAsync(IReadOnlyList<ProfileConversionResult> results,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (results.Count is 0)
            return;

        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);

            var headers = results.Select(static r => r.Profile).ToList();
            await InsertRowsBatchedAsync(con, tx, ProfileStorageRow.TableName, ProfileStorageRow.ColumnList,
                ProfileStorageRow.ColumnCount, headers, ProfileStorageRow.AddParameters, MaxProfilesPerBatch, token);

            await InsertRowsBatchedAsync(con, tx, "profile_functions",
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
                }, 200, token);

            await InsertRowsBatchedAsync(con, tx, "profile_locations",
                "profile_id, ordinal, mapping_ordinal, address, lines_json", 5,
                results.SelectMany(static r => r.Locations).ToList(),
                static (cmd, l) =>
                {
                    cmd.Parameters.Add(new DuckDBParameter { Value = l.ProfileId });
                    cmd.Parameters.Add(new DuckDBParameter { Value = l.Ordinal });
                    cmd.Parameters.Add(new DuckDBParameter { Value = l.MappingOrdinal ?? (object)DBNull.Value });
                    cmd.Parameters.Add(new DuckDBParameter { Value = l.Address is { } a ? (decimal)a : DBNull.Value });
                    cmd.Parameters.Add(new DuckDBParameter { Value = l.LinesJson ?? (object)DBNull.Value });
                }, 200, token);

            await InsertRowsBatchedAsync(con, tx, "profile_mappings",
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
                }, 200, token);

            await InsertRowsBatchedAsync(con, tx, "profile_samples",
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
                }, 200, token);

            await InsertRowsBatchedAsync(con, tx, "profile_stacks", "profile_id, ordinal, location_ordinals_json", 3,
                results.SelectMany(static r => r.Stacks).ToList(),
                static (cmd, st) =>
                {
                    cmd.Parameters.Add(new DuckDBParameter { Value = st.ProfileId });
                    cmd.Parameters.Add(new DuckDBParameter { Value = st.Ordinal });
                    cmd.Parameters.Add(new DuckDBParameter { Value = st.LocationOrdinalsJson ?? (object)DBNull.Value });
                }, 200, token);

            await tx.CommitAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private static async Task InsertRowsBatchedAsync<T>(
        DuckDBConnection con, DbTransaction tx, string table, string columns, int colCount,
        IReadOnlyList<T> rows, Action<DuckDBCommand, T> addParams, int maxBatch, CancellationToken ct)
    {
        if (rows.Count is 0) return;
        var offset = 0;
        while (offset < rows.Count)
        {
            var chunk = Math.Min(maxBatch, rows.Count - offset);
            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = BuildMultiRowInsertSql(table, columns, colCount, chunk);
            for (var i = 0; i < chunk; i++) addParams(cmd, rows[offset + i]);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            offset += chunk;
        }
    }

    public Task<IReadOnlyList<ProfileStorageRow>> GetProfilesAsync(
        string? sessionId = null,
        string? traceId = null,
        string? spanId = null,
        string? serviceName = null,
        string? sampleType = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (!string.IsNullOrEmpty(spanId) && string.IsNullOrEmpty(traceId))
            throw new ArgumentException("A trace id is required when querying profiles by span id.", nameof(traceId));

        return ExecuteReadAsync<IReadOnlyList<ProfileStorageRow>>(con =>
        {
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

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT profile_id, trace_id, span_id, session_id,"
                              + " time_unix_nano, duration_nano, sample_count,"
                              + " sample_type, sample_unit, original_payload_format,"
                              + " service_name,"
                              + " attributes_json, resource_json,"
                              + " schema_url, created_at"
                              + " FROM profiles " + qb.WhereClause
                              + " ORDER BY time_unix_nano DESC LIMIT "
                              + qb.NextParam.ToString(CultureInfo.InvariantCulture);

            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 500) });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                profiles.Add(MapProfile(reader));
            }

            return profiles;
        }, ct);
    }

    public Task<ProfileDetail?> GetProfileDetailAsync(string profileId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<ProfileDetail?>(con =>
        {
            ProfileStorageRow? header = null;
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT profile_id, trace_id, span_id, session_id, time_unix_nano, duration_nano, sample_count, sample_type, sample_unit, original_payload_format, service_name, attributes_json, resource_json, schema_url, created_at FROM profiles WHERE profile_id = $1 LIMIT 1";
                cmd.Parameters.Add(new DuckDBParameter { Value = profileId });
                using var r = cmd.ExecuteReader();
                if (r.Read()) header = MapProfile(r);
            }

            if (header is null) return null;

            var functions = ReadChildRows(con, profileId,
                "SELECT profile_id, ordinal, name, system_name, filename, start_line FROM profile_functions WHERE profile_id = $1 ORDER BY ordinal",
                static r => new ProfileFunctionRow
                {
                    ProfileId = r.GetString(0),
                    Ordinal = r.Col(1).GetInt32(0),
                    Name = r.Col(2).AsString,
                    SystemName = r.Col(3).AsString,
                    Filename = r.Col(4).AsString,
                    StartLine = r.IsDBNull(5) ? null : r.GetInt64(5)
                });

            var locations = ReadChildRows(con, profileId,
                "SELECT profile_id, ordinal, mapping_ordinal, address, lines_json FROM profile_locations WHERE profile_id = $1 ORDER BY ordinal",
                static r => new ProfileLocationRow
                {
                    ProfileId = r.GetString(0),
                    Ordinal = r.Col(1).GetInt32(0),
                    MappingOrdinal = r.IsDBNull(2) ? null : r.Col(2).GetInt32(0),
                    Address = r.IsDBNull(3) ? null : r.Col(3).GetUInt64(0),
                    LinesJson = r.Col(4).AsString
                });

            var mappings = ReadChildRows(con, profileId,
                "SELECT profile_id, ordinal, filename, memory_start, memory_limit, file_offset FROM profile_mappings WHERE profile_id = $1 ORDER BY ordinal",
                static r => new ProfileMappingRow
                {
                    ProfileId = r.GetString(0),
                    Ordinal = r.Col(1).GetInt32(0),
                    Filename = r.Col(2).AsString,
                    MemoryStart = r.IsDBNull(3) ? null : r.Col(3).GetUInt64(0),
                    MemoryLimit = r.IsDBNull(4) ? null : r.Col(4).GetUInt64(0),
                    FileOffset = r.IsDBNull(5) ? null : r.Col(5).GetUInt64(0)
                });

            var samples = ReadChildRows(con, profileId,
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
                });

            var stacks = ReadChildRows(con, profileId,
                "SELECT profile_id, ordinal, location_ordinals_json FROM profile_stacks WHERE profile_id = $1 ORDER BY ordinal",
                static r => new ProfileStackRow
                {
                    ProfileId = r.GetString(0), Ordinal = r.Col(1).GetInt32(0), LocationOrdinalsJson = r.Col(2).AsString
                });

            return new ProfileDetail
            {
                Profile = header,
                Functions = functions,
                Locations = locations,
                Mappings = mappings,
                Samples = samples,
                Stacks = stacks
            };
        }, ct);
    }

    private static IReadOnlyList<T> ReadChildRows<T>(DuckDBConnection connection, string profileId,
        string sql, Func<DbDataReader, T> mapper)
    {
        var rows = new List<T>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new DuckDBParameter { Value = profileId });
        using var r = cmd.ExecuteReader();
        while (r.Read()) rows.Add(mapper(r));
        return rows;
    }

    private async Task WriterLoopAsync()
    {
        try
        {
            await foreach (var job in _jobs.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                Interlocked.Decrement(ref _queuedWriteJobs);
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
            Debug.WriteLine(ex);
        }
        finally
        {
            while (_jobs.Reader.TryRead(out var leftover))
            {
                Interlocked.Decrement(ref _queuedWriteJobs);
                leftover.OnAborted(new OperationCanceledException("Store is shutting down."));
            }
        }
    }

    // One dedicated OS thread per reader slot. Each owns a private READ_ONLY connection and runs the
    // synchronous (native, blocking) DuckDB read jobs here — never on a thread-pool thread.
    private void ReaderLoop(DuckDBConnection con)
    {
        var reader = _reads!.Reader;
        try
        {
            while (true)
            {
                bool waited;
                try
                {
                    waited = reader.WaitToReadAsync(_cts.Token).AsTask().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (!waited)
                    break;

                while (reader.TryRead(out var job))
                {
                    try
                    {
                        job.Execute(con);
                    }
                    catch (OperationCanceledException oce)
                    {
                        job.Abort(oce);
                    }
                    catch (Exception ex)
                    {
                        job.Abort(ex);
                    }
                }
            }
        }
        finally
        {
            con.Dispose();
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
            var sql = SpanStorageRow.BuildMultiRowInsertSql(chunkSize);

            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;

            for (var i = 0; i < chunkSize; i++)
            {
                SpanStorageRow.AddParameters(cmd, spans[offset + i]);
            }

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            offset += chunkSize;
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static string BuildMultiRowLogInsertSql(int logCount)
    {
        Debug.Assert(logCount is > 0 and <= MaxLogsPerBatch);

        return s_logInsertSqlCache.GetOrAdd(logCount, static count =>
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

    private static void InitializeSchema(DuckDBConnection con)
    {
        using var manualLogsCmd = con.CreateCommand();
        manualLogsCmd.CommandText = DuckDbSchema.ManualLogsDdl;
        manualLogsCmd.ExecuteNonQuery();

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
        cmd.CommandText = string.Concat(
            DuckDbSchema.SpansDdl, "\n",
            DuckDbSchema.CoreIndexesDdl);
        cmd.ExecuteNonQuery();

        using var costCmd = con.CreateCommand();
        costCmd.CommandText = string.Concat(
            DuckDbSchema.ModelPricingDdl, "\n",
            DuckDbSchema.ModelPricingTiersDdl, "\n",
            DuckDbSchema.CostByModelHourlyViewDdl);
        costCmd.ExecuteNonQuery();
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
            AttributesJson = reader.Col(11).AsString,
            ResourceJson = reader.Col(12).AsString,
            SchemaUrl = reader.Col(13).AsString,
            CreatedAt = reader.Col(14).AsDateTimeOffset
        };

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Read(ref _disposed) is not 0, this);


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

    private interface IReadJob
    {
        void Execute(DuckDBConnection con);
        void Cancel();
        void Abort(Exception error);
    }

    private sealed class ReadJob<TResult>(Func<DuckDBConnection, TResult> read, CancellationToken ct) : IReadJob
    {
        private readonly TaskCompletionSource<TResult> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TResult> Task => _tcs.Task;

        // Runs on a dedicated reader thread. RunContinuationsAsynchronously guarantees the awaiting
        // caller's continuation resumes on the thread pool, never inline on this reader thread.
        public void Execute(DuckDBConnection con)
        {
            if (ct.IsCancellationRequested)
            {
                _tcs.TrySetCanceled(ct);
                return;
            }

            try
            {
                _tcs.TrySetResult(read(con));
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

        public void Cancel() => _tcs.TrySetCanceled();

        public void Abort(Exception error)
        {
            if (error is OperationCanceledException oce)
                _tcs.TrySetCanceled(oce.CancellationToken);
            else
                _tcs.TrySetException(error);
        }
    }

    private struct QueryBuilder()
    {
        private readonly List<string> _conditions = [];
        private readonly List<DuckDBParameter> _parameters = [];
        private int _paramIndex = 1;

        public void Add(string condition, object value)
        {
            _conditions.Add(condition.Replace("$N", $"${_paramIndex++}"));
            _parameters.Add(new DuckDBParameter { Value = value });
        }

        public void Add(string condition, object first, object second, object third)
        {
            _conditions.Add(ReplaceNextParam(ReplaceNextParam(ReplaceNextParam(condition))));
            _parameters.Add(new DuckDBParameter { Value = first });
            _parameters.Add(new DuckDBParameter { Value = second });
            _parameters.Add(new DuckDBParameter { Value = third });
        }

        private string ReplaceNextParam(string condition)
        {
            var index = condition.IndexOf("$N", StringComparison.Ordinal);
            if (index < 0)
                throw new ArgumentException("Condition does not contain a $N parameter placeholder.", nameof(condition));

            return condition[..index] + $"${_paramIndex++}" + condition[(index + 2)..];
        }

        public readonly void AddCondition(string condition) => _conditions.Add(condition);

        public readonly string WhereClause =>
            _conditions.Count > 0 ? $"WHERE {string.Join(" AND ", _conditions)}" : "";

        public readonly string NextParam => $"${_paramIndex}";

        public readonly void ApplyTo(DuckDBCommand cmd) => cmd.Parameters.AddRange(_parameters);
    }

}
