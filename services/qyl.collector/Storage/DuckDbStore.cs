using DuckDB.NET.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Collector.Telemetry;

using static System.Threading.Volatile;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore : IQylStore
{

    private const int MaxSpansPerBatch = 100;

    private const int MaxLogsPerBatch = 150;


    private static readonly TimeSpan s_shutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _cts = new();
    private readonly Func<CancellationToken, ValueTask>? _beforeWrite;
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly bool _isInMemory;
    private readonly int _jobQueueCapacity;
    private readonly ILogger<DuckDbStore> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Channel<WriteJob> _jobs;
    private readonly Channel<IReadJob>? _reads;
    private readonly Task[] _readerTasks;
    private readonly Task _writerTask;
    private int _disposed;


    public DuckDbStore(
        string databasePath,
        int jobQueueCapacity = 1000,
        int maxConcurrentReads = 8,
        int readQueueCapacity = 1000,
        string? memoryLimit = null,
        int? threads = null,
        string? tempDirectory = null,
        Func<CancellationToken, ValueTask>? beforeWrite = null,
        ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<DuckDbStore>();
        _isInMemory = databasePath == ":memory:";
        _databasePath = _isInMemory ? databasePath : Path.GetFullPath(databasePath);
        _connectionString = $"DataSource={_databasePath};vacuum_rebuild_indexes={ulong.MaxValue}";
        _jobQueueCapacity = Math.Max(1, jobQueueCapacity);
        _beforeWrite = beforeWrite;
        var connection = new DuckDBConnection(_connectionString);
        try
        {
            connection.Open();
            ConfigureDatabase(connection, memoryLimit, threads, tempDirectory);
            InitializeSchema(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        Connection = connection;

        _jobs = Channel.CreateBounded<WriteJob>(new BoundedChannelOptions(_jobQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false
        });
        _writerTask = Task.Run(WriterLoopAsync);

        // DuckDB.NET's *Async methods are synchronous-over-async: the embedded engine has no IO to
        // await, so every read blocks its calling thread for the full query. To keep that blocking
        // off the shared thread pool (Kestrel), reads run on a dedicated set of OS threads, each with
        // its own connection. DuckDB.NET caches one native database instance per file path, so every
        // connection to that file shares the instance and reads are MVCC-concurrent with the writer.
        // In-memory mode has no shared on-disk instance, so its reads are serialized through the
        // single writer connection instead.
        if (_isInMemory)
        {
            _readerTasks = [];
            _reads = null;
        }
        else
        {
            var concurrency = Math.Max(1, maxConcurrentReads);
            _reads = Channel.CreateBounded<IReadJob>(new BoundedChannelOptions(readQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait, SingleReader = false, SingleWriter = false
            });

            // Reader connections use the same connection string as the writer on purpose: DuckDB.NET
            // keys its native-instance cache by file path alone, so an ACCESS_MODE=READ_ONLY token
            // here would be silently ignored on reuse (the writer already opened the instance
            // read-write). Sharing that one instance is exactly what makes these reads MVCC-concurrent.
            // Open every reader connection up front (on this thread) so a failure surfaces loudly and
            // immediately as a startup error, not asynchronously on a background thread.
            var connections = new DuckDBConnection[concurrency];
            for (var i = 0; i < concurrency; i++)
            {
                var con = new DuckDBConnection(_connectionString);
                con.Open();
                connections[i] = con;
            }

            _readerTasks = new Task[concurrency];
            for (var i = 0; i < concurrency; i++)
            {
                var con = connections[i];
                _readerTasks[i] = Task.Factory.StartNew(
                        static state =>
                        {
                            var owner = ((DuckDbStore Store, DuckDBConnection Connection))state!;
                            return owner.Store.ReaderLoopAsync(owner.Connection);
                        },
                        (Store: this, Connection: con),
                        CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default)
                    .Unwrap();
            }
        }
    }


    private DuckDBConnection Connection { get; }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            return;

        List<Exception>? shutdownErrors = null;

        _jobs.Writer.TryComplete();
        _reads?.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _writerTask.WaitAsync(s_shutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            AddShutdownError(ref shutdownErrors, new TimeoutException("DuckDB writer did not stop before shutdown timeout.", ex));
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            _jobs.Writer.TryComplete();
        }
        catch (Exception ex)
        {
            AddShutdownError(ref shutdownErrors, ex);
        }

        foreach (var readerTask in _readerTasks)
        {
            try
            {
                await readerTask.WaitAsync(s_shutdownTimeout).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                continue;
            }
            catch (Exception ex)
            {
                AddShutdownError(ref shutdownErrors, ex);
            }
        }

        if (_reads is not null)
            while (_reads.Reader.TryRead(out var leftover))
                leftover.Cancel();

        // Flush the WAL into the main database file and truncate it so the next start is a clean,
        // fast open instead of a WAL replay. Pointless for an in-memory database.
        if (!_isInMemory)
        {
            try
            {
                await using var checkpoint = Connection.CreateCommand();
                checkpoint.CommandText = "CHECKPOINT";
                await checkpoint.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            catch (DuckDBException ex)
            {
                // A failed final checkpoint is not data loss (the WAL is replayed on next open), but
                // it must surface and must not abort the rest of teardown — route it into the
                // shutdown AggregateException instead of throwing past Connection.Dispose().
                AddShutdownError(
                    ref shutdownErrors,
                    new InvalidOperationException("DuckDB shutdown CHECKPOINT failed.", ex));
            }
        }

        Connection.Dispose();
        _cts.Dispose();

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
        return await job.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<T> ExecuteWriteAsync<T>(Func<DuckDBConnection, CancellationToken, ValueTask<T>> operation,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        var job = new WriteJob<T>(operation, ct);
        if (!_jobs.Writer.TryWrite(job))
            throw new QylStoreUnavailableException("DuckDB write queue is at capacity.");
        job.ArmCancellation();
        return await job.Task.ConfigureAwait(false);
    }

    private async Task ExecuteWriteAsync(Func<DuckDBConnection, CancellationToken, ValueTask> operation,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await operation(con, token).ConfigureAwait(false);
            return 0;
        }, ct);
        if (!_jobs.Writer.TryWrite(job))
            throw new QylStoreUnavailableException("DuckDB write queue is at capacity.");
        job.ArmCancellation();
        await job.Task.ConfigureAwait(false);
    }

    private async Task<T> ExecuteMaintenanceWriteAsync<T>(
        Func<DuckDBConnection, CancellationToken, ValueTask<T>> operation,
        CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        var job = new WriteJob<T>(operation, ct);
        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        job.ArmCancellation();
        return await job.Task.ConfigureAwait(false);
    }


    public async ValueTask EnqueueAsync(SpanBatch batch, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (ct.IsCancellationRequested)
            await ValueTask.FromCanceled(ct).ConfigureAwait(false);
        if (batch.Spans.Count is 0)
        {
            return;
        }

        await ExecuteWriteAsync(
                (con, token) => WriteBatchInternalAsync(con, batch, token),
                ct)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<SpanStorageRow>> GetSpansBySessionAsync(
        string sessionId,
        string projectId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<SpanStorageRow>>(con =>
        {
            var spans = new List<SpanStorageRow>();
            using var cmd = con.CreateCommand();
            // A session id is COALESCE(session_id, trace_id) — the same identity the session
            // aggregation (DuckDbStore.Sessions) and GetSessionAsync use. Match it here too:
            // non-session telemetry (e.g. plain HTTP) persists with a NULL session_id and is keyed
            // by its trace_id, so a strict `session_id = $2` would drop the very spans the session's
            // trace_count/span_count were derived from.
            //
            // The membership match runs as a subquery over trace ids, not over spans directly:
            // a session key is often stamped on a single span per trace (e.g. an app tagging only
            // its request-handler span), and the session view must return the FULL traces the
            // session touched — parents, children, and gen_ai spans included — or trace trees
            // render empty and token/cost spans silently vanish from the session.
            cmd.CommandText = "SELECT " + SpanStorageRow.SelectColumnList
                                        + " FROM spans WHERE project_id = $1 AND trace_id IN ("
                                        + "SELECT DISTINCT trace_id FROM spans WHERE project_id = $1"
                                        + " AND (session_id = $2 OR (session_id IS NULL AND trace_id = $2)))"
                                        + " ORDER BY start_time_unix_nano ASC";
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                spans.Add(SpanStorageRow.MapFromReader(reader));

            return spans;
        }, ct);
    }

    public Task<IReadOnlyList<SpanStorageRow>> GetTraceAsync(
        string traceId,
        string projectId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<SpanStorageRow>>(con =>
        {
            var spans = new List<SpanStorageRow>();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT " + SpanStorageRow.SelectColumnList
                                        + " FROM spans WHERE project_id = $1 AND trace_id = $2 ORDER BY start_time_unix_nano ASC";
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
            cmd.Parameters.Add(new DuckDBParameter { Value = traceId });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                spans.Add(SpanStorageRow.MapFromReader(reader));

            return spans;
        }, ct);
    }

    public Task<IReadOnlyList<SpanStorageRow>> GetSpansAsync(
        string projectId,
        int limit = 100,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<SpanStorageRow>>(con =>
        {
            var spans = new List<SpanStorageRow>();
            var qb = new QueryBuilder();

            qb.Add("project_id = $N", projectId);

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT " + SpanStorageRow.SelectColumnList
                                        + " FROM spans " + qb.WhereClause
                                        + " ORDER BY start_time_unix_nano DESC LIMIT "
                                        + qb.NextParam.ToString(CultureInfo.InvariantCulture);

            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                spans.Add(SpanStorageRow.MapFromReader(reader));

            return spans;
        }, ct);
    }

    public Task<TraceStoragePage> GetTracePageAsync(
        string projectId,
        TracePageCursor? cursor,
        int limit,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Trace page limit must be positive.");

        return ExecuteReadAsync(con =>
        {
            var heads = new List<(string TraceId, ulong ActivityUnixNano)>(limit + 1);
            using (var headCommand = con.CreateCommand())
            {
                headCommand.CommandText = cursor.HasValue
                    ? """
                      WITH trace_heads AS (
                          SELECT trace_id, MAX(end_time_unix_nano) AS activity_unix_nano
                          FROM spans
                          WHERE project_id = $1
                          GROUP BY trace_id
                      )
                      SELECT trace_id, activity_unix_nano
                      FROM trace_heads
                      WHERE activity_unix_nano < $2
                         OR (activity_unix_nano = $2 AND trace_id < $3)
                      ORDER BY activity_unix_nano DESC, trace_id DESC
                      LIMIT $4
                      """
                    : """
                      WITH trace_heads AS (
                          SELECT trace_id, MAX(end_time_unix_nano) AS activity_unix_nano
                          FROM spans
                          WHERE project_id = $1
                          GROUP BY trace_id
                      )
                      SELECT trace_id, activity_unix_nano
                      FROM trace_heads
                      ORDER BY activity_unix_nano DESC, trace_id DESC
                      LIMIT $2
                      """;
                headCommand.Parameters.Add(new DuckDBParameter { Value = projectId });
                if (cursor is { } after)
                {
                    headCommand.Parameters.Add(new DuckDBParameter { Value = (decimal)after.ActivityUnixNano });
                    headCommand.Parameters.Add(new DuckDBParameter { Value = after.TraceId });
                }

                headCommand.Parameters.Add(new DuckDBParameter { Value = limit + 1 });
                using var headReader = headCommand.ExecuteReader();
                while (headReader.Read())
                {
                    heads.Add((
                        DuckDbValueReader.ReadString(headReader, 0, string.Empty),
                        DuckDbValueReader.ReadUInt64(headReader, 1, 0)));
                }
            }

            var hasMore = heads.Count > limit;
            if (hasMore) heads.RemoveRange(limit, heads.Count - limit);
            if (heads.Count is 0) return new TraceStoragePage([], HasMore: false);

            var spansByTrace = heads.ToDictionary(
                static head => head.TraceId,
                static _ => new List<SpanStorageRow>(),
                StringComparer.Ordinal);
            using (var spansCommand = con.CreateCommand())
            {
                var placeholders = string.Join(", ",
                    Enumerable.Range(2, heads.Count).Select(static index =>
                        "$" + index.ToString(CultureInfo.InvariantCulture)));
                spansCommand.CommandText = "SELECT " + SpanStorageRow.SelectColumnList
                                           + " FROM spans WHERE project_id = $1 AND trace_id IN ("
                                           + placeholders
                                           + ") ORDER BY trace_id DESC, start_time_unix_nano ASC, span_id ASC";
                spansCommand.Parameters.Add(new DuckDBParameter { Value = projectId });
                foreach (var head in heads)
                    spansCommand.Parameters.Add(new DuckDBParameter { Value = head.TraceId });

                using var spanReader = spansCommand.ExecuteReader();
                while (spanReader.Read())
                {
                    var span = SpanStorageRow.MapFromReader(spanReader);
                    spansByTrace[span.TraceId].Add(span);
                }
            }

            return new TraceStoragePage(
                [.. heads.Select(head => new TraceStoragePageItem(
                    head.TraceId,
                    head.ActivityUnixNano,
                    spansByTrace[head.TraceId]))],
                hasMore);
        }, ct);
    }


    public Task<StorageStats> GetStorageStatsAsync(
        string projectId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT
                                  (SELECT COUNT(*) FROM spans WHERE project_id = $1) as span_count,
                                  (SELECT COUNT(DISTINCT COALESCE(session_id, trace_id)) FROM spans WHERE project_id = $1) as session_count
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new StorageStats
                {
                    SpanCount = DuckDbValueReader.ReadInt64(reader, 0, 0),
                    SessionCount = DuckDbValueReader.ReadInt64(reader, 1, 0)
                };
            }

            return new StorageStats();
        }, ct);
    }

    private void AddShutdownError(ref List<Exception>? errors, Exception error)
    {
        errors ??= [];
        errors.Add(error);
        DuckDbStoreLog.ShutdownFailed(_logger, error);
    }

    public async Task InsertLogsAsync(IReadOnlyList<LogStorageRow> logs, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (logs.Count is 0)
            return;

        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            await InsertRowsBatchedAsync(con, tx, logs, LogStorageRow.AddParameters,
                LogStorageRow.BuildMultiRowInsertSql, MaxLogsPerBatch, token);
            await tx.CommitAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<LogStorageRow>> GetLogsAsync(
        string projectId,
        string? sessionId = null,
        string? traceId = null,
        string? severityText = null,
        int? minSeverity = null,
        string? search = null,
        ulong? start = null,
        ulong? before = null,
        string? serviceName = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<LogStorageRow>>(con =>
        {
            var logs = new List<LogStorageRow>();
            var qb = new QueryBuilder();

            qb.Add("project_id = $N", projectId);
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
            if (start.HasValue)
                qb.Add("time_unix_nano >= $N", (decimal)start.Value);
            if (before.HasValue)
                qb.Add("time_unix_nano <= $N", (decimal)before.Value);

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT " + LogStorageRow.SelectColumnList
                              + " FROM logs " + qb.WhereClause
                              + " ORDER BY time_unix_nano DESC, log_id DESC LIMIT "
                              + qb.NextParam.ToString(CultureInfo.InvariantCulture);

            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                logs.Add(LogStorageRow.MapFromReader(reader));

            return logs;
        }, ct);
    }

    public Task<IReadOnlyList<LogStorageRow>> GetLogStreamPageAsync(
        string projectId,
        string? serviceName = null,
        int? minSeverity = null,
        string? search = null,
        long? afterIngestSequence = null,
        int limit = 250,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<LogStorageRow>>(con =>
        {
            var logs = new List<LogStorageRow>();
            var qb = new QueryBuilder();

            qb.Add("project_id = $N", projectId);
            if (!string.IsNullOrEmpty(serviceName))
                qb.Add("service_name = $N", serviceName);
            if (minSeverity.HasValue)
                qb.Add("severity_number >= $N", minSeverity.Value);
            if (!string.IsNullOrWhiteSpace(search))
                qb.Add("(body ILIKE $N OR severity_text ILIKE $N OR service_name ILIKE $N OR attributes_json ILIKE $N)",
                    $"%{search}%");
            if (afterIngestSequence.HasValue)
                qb.Add("ingest_sequence > $N", afterIngestSequence.Value);

            using var cmd = con.CreateCommand();
            if (afterIngestSequence.HasValue)
            {
                cmd.CommandText = "SELECT " + LogStorageRow.SelectColumnList
                                  + " FROM logs " + qb.WhereClause
                                  + " ORDER BY ingest_sequence ASC LIMIT "
                                  + qb.NextParam.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                cmd.CommandText = "SELECT " + LogStorageRow.SelectColumnList
                                  + " FROM (SELECT " + LogStorageRow.SelectColumnList
                                  + " FROM logs " + qb.WhereClause
                                  + " ORDER BY ingest_sequence DESC LIMIT "
                                  + qb.NextParam.ToString(CultureInfo.InvariantCulture)
                                  + ") AS latest_logs ORDER BY ingest_sequence ASC";
            }

            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                logs.Add(LogStorageRow.MapFromReader(reader));

            return logs;
        }, ct);
    }


    private static async Task InsertRowsBatchedAsync<T>(
        DuckDBConnection con, DbTransaction tx, IReadOnlyList<T> rows,
        Action<DuckDBCommand, T> addParams, Func<int, string> buildSql, int maxBatch, CancellationToken ct)
    {
        if (rows.Count is 0) return;
        var offset = 0;
        while (offset < rows.Count)
        {
            var chunk = Math.Min(maxBatch, rows.Count - offset);
            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = buildSql(chunk);
            for (var i = 0; i < chunk; i++) addParams(cmd, rows[offset + i]);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            offset += chunk;
        }
    }

    private async Task WriterLoopAsync()
    {
        try
        {
            await foreach (var job in _jobs.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                if (!job.TryClaim())
                    continue;
                try
                {
                    if (_beforeWrite is not null)
                        await _beforeWrite(_cts.Token).ConfigureAwait(false);
                    await job.ExecuteAsync(Connection, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    job.OnAborted(ex);
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Store disposal cancels the worker; that is the expected exit path.
        }
        catch (Exception error)
        {
            DuckDbStoreLog.StorageWorkerFailed(_logger, error);
        }
        finally
        {
            while (_jobs.Reader.TryRead(out var leftover))
            {
                leftover.OnAborted(new OperationCanceledException("Store is shutting down."));
            }
        }
    }

    // One long-running reader task per slot owns a private connection. Synchronous
    // point reads begin on its dedicated thread; Arrow jobs may yield between
    // batches without blocking a request thread or sharing the connection.
    private async Task ReaderLoopAsync(DuckDBConnection con)
    {
        var reader = _reads!.Reader;
        try
        {
            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var job))
                {
                    try
                    {
                        await job.ExecuteAsync(con).ConfigureAwait(false);
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

        await InsertRowsBatchedAsync(con, tx, batch.Spans, SpanStorageRow.AddParameters,
            SpanStorageRow.BuildMultiRowInsertSql, MaxSpansPerBatch, ct);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static void ConfigureDatabase(DuckDBConnection con, string? memoryLimit, int? threads, string? tempDirectory)
    {
        // Tuning for a write-heavy embedded store. preserve_insertion_order=false drops the
        // bookkeeping that keeps physical row order — telemetry never needs it — which cuts ingest
        // memory and lifts bulk-write throughput. memory_limit / threads / temp_directory are
        // operator-supplied (trusted config); when unset, DuckDB's own defaults apply.
        ExecutePragma(con, "SET preserve_insertion_order = false");

        if (!string.IsNullOrWhiteSpace(memoryLimit))
            ExecutePragma(con, $"SET memory_limit = '{EscapeSqlLiteral(memoryLimit)}'");

        if (threads is > 0)
            ExecutePragma(con, $"SET threads = {threads.Value.ToString(CultureInfo.InvariantCulture)}");

        if (!string.IsNullOrWhiteSpace(tempDirectory))
            ExecutePragma(con, $"SET temp_directory = '{EscapeSqlLiteral(tempDirectory)}'");
    }

    private static void ExecutePragma(DuckDBConnection con, string sql)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // SET ... pragmas take no bound parameters, so operator-supplied values are interpolated.
    // The values are trusted config, but escape single quotes anyway so a stray quote can't break
    // the statement.
    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

    private static void InitializeSchema(DuckDBConnection con)
    {
        using var transaction = con.BeginTransaction();
        ExecuteSchemaSql(con, transaction, DuckDbGeneratedSchema.BootstrapDdl);

        var stored = ReadSchemaIdentity(con, transaction);
        var authoritativeMismatches = FindSchemaMismatches(
            con,
            transaction,
            DuckDbGeneratedSchema.AuthoritativeTables);
        if (stored is { AuthoritativeHash: not DuckDbGeneratedSchema.AuthoritativeHash })
        {
            authoritativeMismatches = DuckDbGeneratedSchema.AuthoritativeTableNames;
        }
        var derivedMismatches = FindSchemaMismatches(
            con,
            transaction,
            DuckDbGeneratedSchema.DerivedTables);

        if (authoritativeMismatches.Count > 0)
        {
            if (HasRows(
                    con,
                    transaction,
                    DuckDbGeneratedSchema.AuthoritativeTableNames))
            {
                var difference = stored is { AuthoritativeHash: not DuckDbGeneratedSchema.AuthoritativeHash }
                    ? $"stored authoritative DDL hash {stored.AuthoritativeHash} does not match " +
                      DuckDbGeneratedSchema.AuthoritativeHash
                    : $"incompatible tables: {string.Join(", ", authoritativeMismatches)}";
                throw new QylSchemaMismatchException(
                    "The authoritative DuckDB schema differs from this build while persisted data exists. " +
                    "Start with a new database and replay or import the authoritative journal; qyl will not " +
                    $"ALTER or delete that data automatically. Difference: {difference}");
            }

            DropExpectedIndexes(
                con,
                transaction,
                DuckDbGeneratedSchema.AuthoritativeTables.Where(
                    table => authoritativeMismatches.Contains(table.Name, StringComparer.Ordinal)));
            DropTables(con, transaction, authoritativeMismatches);
            if (authoritativeMismatches.Contains(LogStorageRow.TableName, StringComparer.Ordinal))
                ExecuteSchemaSql(con, transaction, "DROP SEQUENCE IF EXISTS logs_ingest_sequence");
            CreateSchema(con, transaction, DuckDbGeneratedSchema.AuthoritativeDdl);
            CreateSchema(con, transaction, DuckDbGeneratedSchema.DerivedDdl);
        }
        else
        {
            // A schema created before qyl_schema_meta can be adopted only after its
            // complete generated structure has been checked above. Derived state is
            // never migrated: a mismatch is repaired by deterministic recreation.
            CreateSchema(con, transaction, DuckDbGeneratedSchema.AuthoritativeDdl);
            if (derivedMismatches.Count > 0 ||
                stored is { DerivedHash: not DuckDbGeneratedSchema.DerivedHash })
            {
                DropExpectedIndexes(con, transaction, DuckDbGeneratedSchema.DerivedTables);
                DropGeneratedTables(con, transaction, includeAuthoritative: false);
            }
            CreateSchema(con, transaction, DuckDbGeneratedSchema.DerivedDdl);
        }

        DropRetiredTables(con, transaction);
        WriteSchemaIdentity(con, transaction);
        transaction.Commit();

        VerifyPersistedPrimaryKeys(con);
    }

    private static SchemaIdentity? ReadSchemaIdentity(
        DuckDBConnection con,
        DbTransaction transaction)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              SELECT authoritative_schema_hash, derived_schema_hash
                              FROM qyl_schema_meta
                              WHERE singleton = 0
                              """;
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new SchemaIdentity(reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static void WriteSchemaIdentity(
        DuckDBConnection con,
        DbTransaction transaction)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              INSERT INTO qyl_schema_meta
                                  (singleton, authoritative_schema_hash, derived_schema_hash)
                              VALUES (0, $1, $2)
                              ON CONFLICT (singleton) DO UPDATE SET
                                  authoritative_schema_hash = excluded.authoritative_schema_hash,
                                  derived_schema_hash = excluded.derived_schema_hash,
                                  updated_at = now()
                              """;
        AddParameters(
            command,
            DuckDbGeneratedSchema.AuthoritativeHash,
            DuckDbGeneratedSchema.DerivedHash);
        command.ExecuteNonQuery();
    }

    private static IReadOnlyList<string> FindSchemaMismatches(
        DuckDBConnection con,
        DbTransaction transaction,
        IReadOnlyList<DuckDbExpectedTable> expectedTables)
    {
        var mismatches = new List<string>();
        foreach (var table in expectedTables)
        {
            var ordinal = 0;
            var tableMatches = true;
            {
                using var command = con.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                                      SELECT column_name, data_type, is_nullable, column_default
                                      FROM duckdb_columns()
                                      WHERE schema_name = 'main' AND table_name = $1
                                      ORDER BY column_index
                                      """;
                AddParameters(command, table.Name);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (ordinal >= table.Columns.Length)
                    {
                        tableMatches = false;
                        continue;
                    }

                    var expected = table.Columns[ordinal++];
                    var actualName = reader.GetString(0);
                    var actualType = reader.GetString(1);
                    var actualNullable = reader.GetBoolean(2);
                    var actualDefault = reader.IsDBNull(3) ? null : reader.GetString(3);
                    if (!string.Equals(actualName, expected.Name, StringComparison.Ordinal) ||
                        !string.Equals(
                            NormalizeDuckDbType(actualType),
                            NormalizeDuckDbType(expected.SqlType),
                            StringComparison.Ordinal) ||
                        actualNullable == expected.Required ||
                        !DefaultsMatch(actualDefault, expected.DefaultSql))
                    {
                        tableMatches = false;
                    }
                }
            }

            if (!tableMatches || ordinal != table.Columns.Length ||
                !IndexesMatch(con, transaction, table))
                mismatches.Add(table.Name);
        }

        return mismatches;
    }

    private static bool IndexesMatch(
        DuckDBConnection con,
        DbTransaction transaction,
        DuckDbExpectedTable table)
    {
        var actualIndexes = new Dictionary<
            string,
            (string Table, bool Unique, string[] Columns)>(StringComparer.Ordinal);
        using (var command = con.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                                  SELECT index_name, table_name, is_unique, expressions
                                  FROM duckdb_indexes()
                                  WHERE schema_name = 'main' AND table_name = $1
                                  """;
            AddParameters(command, table.Name);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!actualIndexes.TryAdd(
                        reader.GetString(0),
                        (reader.GetString(1),
                            reader.GetBoolean(2),
                            ParseDuckDbIndexExpressions(reader.GetString(3)))))
                {
                    return false;
                }
            }
        }

        if (actualIndexes.Count != table.Indexes.Length)
            return false;

        foreach (var expected in table.Indexes)
        {
            if (!actualIndexes.TryGetValue(expected.Name, out var actual) ||
                !string.Equals(actual.Table, table.Name, StringComparison.Ordinal) ||
                actual.Unique != expected.Unique ||
                !actual.Columns.SequenceEqual(expected.Columns, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] ParseDuckDbIndexExpressions(string expressions)
    {
        var value = expressions.Trim();
        if (value.Length >= 2 && value[0] == '[' && value[^1] == ']')
            value = value[1..^1];
        if (value.Length is 0)
            return [];

        return value.Split(',')
            .Select(static column => column.Trim().Trim('"'))
            .ToArray();
    }

    private static string NormalizeDuckDbType(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.StartsWith("VARCHAR", StringComparison.Ordinal))
            return "VARCHAR";
        return normalized switch
        {
            "TIMESTAMP WITH TIME ZONE" => "TIMESTAMPTZ",
            "TIMESTAMP WITHOUT TIME ZONE" => "TIMESTAMP",
            _ => normalized
        };
    }

    private static bool DefaultsMatch(string? actual, string? expected)
    {
        if (actual is null || expected is null)
            return actual is null && expected is null;

        static string Normalize(string value) => string.Concat(
            value.Where(static character => !char.IsWhiteSpace(character)))
            .Trim('(', ')')
            .ToLowerInvariant()
            .Replace("\"", "", StringComparison.Ordinal)
            .Replace("cast(uuid()asvarchar)", "uuid()::varchar", StringComparison.Ordinal);

        return string.Equals(Normalize(actual), Normalize(expected), StringComparison.Ordinal);
    }

    private static bool HasRows(
        DuckDBConnection con,
        DbTransaction transaction,
        IReadOnlyList<string> tables)
    {
        foreach (var table in tables)
        {
            if (!TableExists(con, transaction, table))
                continue;

            using var command = con.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = string.Concat(
                "SELECT EXISTS (SELECT 1 FROM ",
                QuoteIdentifier(table),
                " LIMIT 1)");
            if (command.ExecuteScalar() is true)
                return true;
        }

        return false;
    }

    private static bool TableExists(
        DuckDBConnection con,
        DbTransaction transaction,
        string table)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              SELECT count(*)
                              FROM duckdb_tables()
                              WHERE schema_name = 'main' AND table_name = $1
                              """;
        AddParameters(command, table);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) is 1;
    }

    private static void CreateSchema(
        DuckDBConnection con,
        DbTransaction transaction,
        IEnumerable<string> statements)
    {
        foreach (var statement in statements)
            ExecuteSchemaSql(con, transaction, statement);
    }

    private static void DropGeneratedTables(
        DuckDBConnection con,
        DbTransaction transaction,
        bool includeAuthoritative)
    {
        DropTables(con, transaction, DuckDbGeneratedSchema.DerivedTableNames);

        if (!includeAuthoritative)
            return;

        DropTables(con, transaction, DuckDbGeneratedSchema.AuthoritativeTableNames);
        ExecuteSchemaSql(con, transaction, "DROP SEQUENCE IF EXISTS logs_ingest_sequence");
    }

    private static void DropTables(
        DuckDBConnection con,
        DbTransaction transaction,
        IEnumerable<string> tables)
    {
        foreach (var table in tables)
            ExecuteSchemaSql(con, transaction, $"DROP TABLE IF EXISTS {QuoteIdentifier(table)}");
    }

    private static void DropExpectedIndexes(
        DuckDBConnection con,
        DbTransaction transaction,
        IEnumerable<DuckDbExpectedTable> tables)
    {
        foreach (var index in tables.SelectMany(static table => table.Indexes))
            ExecuteSchemaSql(con, transaction, $"DROP INDEX IF EXISTS {QuoteIdentifier(index.Name)}");
    }

    private static void DropRetiredTables(
        DuckDBConnection con,
        DbTransaction transaction)
    {
        foreach (var table in DuckDbGeneratedSchema.RetiredTableNames)
            ExecuteSchemaSql(con, transaction, $"DROP TABLE IF EXISTS {QuoteIdentifier(table)}");
    }

    private static void ExecuteSchemaSql(
        DuckDBConnection con,
        DbTransaction transaction,
        string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AddParameters(DuckDBCommand command, params object[] values)
    {
        foreach (var value in values)
            command.Parameters.Add(new DuckDBParameter { Value = value });
    }

    private static string QuoteIdentifier(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private sealed record SchemaIdentity(string AuthoritativeHash, string DerivedHash);

    private static void VerifyPersistedPrimaryKeys(DuckDBConnection con)
    {
        foreach (var table in DuckDbGeneratedSchema.AuthoritativeTables
                     .Concat(DuckDbGeneratedSchema.DerivedTables))
        {
            var columns = table.Columns
                .Where(static column => column.PrimaryKeyOrdinal >= 0)
                .OrderBy(static column => column.PrimaryKeyOrdinal)
                .Select(static column => column.Name);
            VerifyPersistedPrimaryKey(con, table.Name, string.Join(',', columns));
        }
    }

    private static void VerifyPersistedPrimaryKey(DuckDBConnection con, string table, string expectedCsv)
    {
        if (expectedCsv.Length is 0)
            return;

        var expected = expectedCsv.Split(',').ToHashSet(StringComparer.Ordinal);
        var actual = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
                          SELECT unnest(constraint_column_names)
                          FROM duckdb_constraints()
                          WHERE table_name = $1 AND constraint_type = 'PRIMARY KEY'
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = table });
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            actual.Add(reader.GetString(0));

        if (!expected.SetEquals(actual))
        {
            throw new InvalidOperationException(
                $"Persisted table '{table}' has primary key ({string.Join(", ", actual.Order(StringComparer.Ordinal))}); " +
                $"this build requires ({string.Join(", ", expected.Order(StringComparer.Ordinal))}).");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Read(ref _disposed) is not 0, this);

    private abstract class WriteJob
    {
        public abstract bool TryClaim();

        public abstract ValueTask ExecuteAsync(DuckDBConnection con, CancellationToken ct);

        public virtual void OnAborted(Exception error)
        {
        }
    }

    private sealed class WriteJob<TResult>(
        Func<DuckDBConnection, CancellationToken, ValueTask<TResult>> action,
        CancellationToken cancellationToken)
        : WriteJob
    {
        private readonly TaskCompletionSource<TResult> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _cancellationRegistration;
        private int _state;

        public Task<TResult> Task => _tcs.Task;

        public void ArmCancellation()
        {
            _cancellationRegistration = cancellationToken.Register(
                static state => ((WriteJob<TResult>)state!).CancelBeforeClaim(),
                this);
            if (Volatile.Read(ref _state) is not 0)
                _cancellationRegistration.Dispose();
        }

        public override bool TryClaim()
        {
            if (Interlocked.CompareExchange(ref _state, 1, 0) is not 0)
            {
                _cancellationRegistration.Dispose();
                return false;
            }
            _cancellationRegistration.Dispose();
            return true;
        }

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
            finally
            {
                _cancellationRegistration.Dispose();
            }
        }

        public override void OnAborted(Exception error)
        {
            _cancellationRegistration.Dispose();
            if (error is OperationCanceledException oce)
                _tcs.TrySetCanceled(oce.CancellationToken);
            else
                _tcs.TrySetException(error);
        }

        private void CancelBeforeClaim()
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) is 0)
                _tcs.TrySetCanceled(cancellationToken);
        }
    }

    private interface IReadJob
    {
        ValueTask ExecuteAsync(DuckDBConnection con);
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
        public ValueTask ExecuteAsync(DuckDBConnection con)
        {
            if (ct.IsCancellationRequested)
            {
                _tcs.TrySetCanceled(ct);
                return ValueTask.CompletedTask;
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
            return ValueTask.CompletedTask;
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

        public readonly string WhereClause =>
            _conditions.Count > 0 ? $"WHERE {string.Join(" AND ", _conditions)}" : "";

        public readonly string NextParam => $"${_paramIndex}";

        public readonly void ApplyTo(DuckDBCommand cmd) => cmd.Parameters.AddRange(_parameters);
    }

}
