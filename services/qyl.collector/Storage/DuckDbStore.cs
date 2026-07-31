using DuckDB.NET.Data;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Qyl.Collector.Telemetry;
using Qyl.Collector.Workflow;

using static System.Threading.Volatile;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore : IQylStore
{

    private const int MaxSpansPerBatch = 100;

    private const int MaxLogsPerBatch = 150;


    private static readonly TimeSpan s_shutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _cts = new();
    private readonly Func<CancellationToken, ValueTask>? _beforeWrite;
    private readonly Func<WorkflowProjectionKey, ulong, CancellationToken, ValueTask>?
        _beforeProjectionQuantum;
    private readonly Func<
        WorkflowCheckpointReconciliationStage,
        CancellationToken,
        ValueTask>? _beforeCheckpointReconciliation;
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly bool _isInMemory;
    private readonly int _jobQueueCapacity;
    private readonly ILogger<DuckDbStore> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Channel<WriteJob> _jobs;
    private readonly Channel<IReadJob>? _reads;
    private readonly Thread[] _readerThreads;
    private readonly Task _writerTask;
    private readonly WorkflowContentProtector _workflowContentProtector;
    private readonly WorkflowProjectionLimits _workflowProjectionLimits;
    private readonly WorkflowCheckpointStore _workflowCheckpointStore;
    private readonly WorkflowProjectionRuntime _workflowProjectionRuntime;
    private readonly Task _checkpointReconciliationTask;
    private readonly SemaphoreSlim _checkpointReconciliationGate = new(1, 1);
    private readonly SemaphoreSlim _checkpointManifestMutationGate = new(1, 1);
    private readonly ConcurrentDictionary<WorkflowProjectionKey, byte>
        _activatedCheckpointRepairs = new();

    private string? _checkpointManifestProjectCursor;
    private string? _checkpointManifestRunCursor;
    private ulong _checkpointReconciliationEpoch;
    private WorkflowCheckpointReconciliationPhase _checkpointReconciliationPhase;
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
        WorkflowContentProtector? workflowContentProtector = null,
        WorkflowProjectionLimits? workflowProjectionLimits = null,
        Func<WorkflowProjectionKey, ulong, CancellationToken, ValueTask>?
            beforeProjectionQuantum = null,
        Func<WorkflowCheckpointReconciliationStage, CancellationToken, ValueTask>?
            beforeCheckpointReconciliation = null,
        ILoggerFactory? loggerFactory = null,
        Action<int>? observeWorkflowMigrationRetainedRuns = null)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<DuckDbStore>();
        _isInMemory = databasePath == ":memory:";
        _databasePath = _isInMemory ? databasePath : Path.GetFullPath(databasePath);
        _connectionString = $"DataSource={_databasePath};vacuum_rebuild_indexes={ulong.MaxValue}";
        _jobQueueCapacity = Math.Max(1, jobQueueCapacity);
        _beforeWrite = beforeWrite;
        _beforeProjectionQuantum = beforeProjectionQuantum;
        _beforeCheckpointReconciliation = beforeCheckpointReconciliation;
        _workflowContentProtector = workflowContentProtector ??
            new WorkflowContentProtector(
                SHA256.HashData(Encoding.UTF8.GetBytes("qyl-workflow-storage-tests")));
        _workflowProjectionLimits = workflowProjectionLimits ?? new WorkflowProjectionLimits();
        _workflowCheckpointStore = new WorkflowCheckpointStore(
            _isInMemory ? null : $"{_databasePath}.workflow-checkpoints",
            _workflowProjectionLimits,
            _beforeCheckpointReconciliation);
        var connection = new DuckDBConnection(_connectionString);
        try
        {
            connection.Open();
            ConfigureDatabase(connection, memoryLimit, threads, tempDirectory);
            InitializeSchema(connection, observeWorkflowMigrationRetainedRuns);
        }
        catch
        {
            connection.Dispose();
            _workflowCheckpointStore.Dispose();
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

            _readerThreads = new Thread[concurrency];
            for (var i = 0; i < concurrency; i++)
            {
                var con = connections[i];
                var thread = new Thread(() => ReaderLoop(con)) { IsBackground = true, Name = $"duckdb-reader-{i}" };
                thread.Start();
                _readerThreads[i] = thread;
            }
        }

        _workflowProjectionRuntime = new WorkflowProjectionRuntime(
            this,
            _workflowProjectionLimits,
            _loggerFactory,
            _cts.Token);
        _checkpointReconciliationTask = Task.Run(
            () => RunWorkflowCheckpointReconciliationLoopAsync(_cts.Token));
    }


    private DuckDBConnection Connection { get; }

    internal WorkflowProjectionRuntimeSnapshot WorkflowProjectionRuntimeSnapshot =>
        _workflowProjectionRuntime.Snapshot;

    internal Task RetireWorkflowProjectionAsync(WorkflowProjectionKey key) =>
        _workflowProjectionRuntime.RetireAsync(key);

    internal Task<WorkflowProjectionCheckpoint> WaitForWorkflowProjectionAsync(
        WorkflowProjectionKey key,
        ulong sequence,
        CancellationToken ct) =>
        _workflowProjectionRuntime.WaitForAsync(key, sequence, ct);

    internal string? WorkflowCheckpointRoot => _workflowCheckpointStore.Root;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            return;

        List<Exception>? shutdownErrors = null;

        _jobs.Writer.TryComplete();
        _reads?.Writer.TryComplete();
        var projectionShutdown = _workflowProjectionRuntime.DisposeAsync().AsTask();
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await projectionShutdown
                .WaitAsync(s_shutdownTimeout)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AddShutdownError(ref shutdownErrors, ex);
        }

        try
        {
            await _checkpointReconciliationTask
                .WaitAsync(s_shutdownTimeout)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            AddShutdownError(ref shutdownErrors, ex);
        }

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
        _workflowCheckpointStore.Dispose();
        _checkpointManifestMutationGate.Dispose();
        _checkpointReconciliationGate.Dispose();
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
        var reason = error.GetType().Name;
        WorkflowLifecycleLog.ShutdownFailed(_logger, reason);
        QylTelemetry.RecordWorkflowLifecycleOutcome("failed", $"shutdown_{reason}");
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
            QylTelemetry.RecordWorkflowLifecycleOutcome(
                "cancelled",
                "writer_shutdown");
        }
        catch (Exception error)
        {
            var reason = error.GetType().Name;
            WorkflowLifecycleLog.StorageWorkerFailed(_logger, reason);
            QylTelemetry.RecordWorkflowLifecycleOutcome(
                "failed",
                $"writer_{reason}");
        }
        finally
        {
            while (_jobs.Reader.TryRead(out var leftover))
            {
                leftover.OnAborted(new OperationCanceledException("Store is shutting down."));
            }
        }
    }

    // One dedicated OS thread per reader slot. Each owns a private connection (sharing the writer's
    // cached native instance) and runs the synchronous (native, blocking) DuckDB read jobs here —
    // never on a thread-pool thread.
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

    private static void InitializeSchema(
        DuckDBConnection con,
        Action<int>? observeWorkflowMigrationRetainedRuns)
    {
        // The live log stream needs a collector-owned monotonic cursor. Producer event timestamps
        // are routinely delayed/out of order, so assign arrival order inside DuckDB instead.
        using var logSequenceCmd = con.CreateCommand();
        logSequenceCmd.CommandText = "CREATE SEQUENCE IF NOT EXISTS logs_ingest_sequence START 1";
        logSequenceCmd.ExecuteNonQuery();

        using var logsCmd = con.CreateCommand();
        logsCmd.CommandText = string.Concat(
            LogStorageRow.CreateTableDdl, "\n",
            LogStorageRow.MigrateTableDdl, "\n",
            LogStorageRow.IndexesDdl);
        logsCmd.ExecuteNonQuery();

        using var cmd = con.CreateCommand();
        cmd.CommandText = string.Concat(
            SpanStorageRow.CreateTableDdl, "\n",
            SpanStorageRow.MigrateTableDdl, "\n",
            SpanStorageRow.IndexesDdl);
        cmd.ExecuteNonQuery();

        using var workflowCmd = con.CreateCommand();
        workflowCmd.CommandText = string.Concat(
            WorkflowRunDbRow.CreateTableDdl, "\n",
            WorkflowRunDbRow.MigrateTableDdl, "\n",
            WorkflowEventDbRow.CreateTableDdl, "\n",
            WorkflowEventDbRow.IndexesDdl, "\n",
            WorkflowContentDbRow.CreateTableDdl, "\n",
            WorkflowContentReferenceDbRow.CreateTableDdl, "\n",
            WorkflowContentReferenceDbRow.IndexesDdl, "\n",
            WorkflowClientJournalDbRow.CreateTableDdl, "\n",
            WorkflowClientJournalRangeDbRow.CreateTableDdl, "\n",
            WorkflowClientJournalRangeDbRow.IndexesDdl, "\n",
            WorkflowCommandDbRow.CreateTableDdl, "\n",
            WorkflowCommandDbRow.IndexesDdl);
        workflowCmd.ExecuteNonQuery();

        MigrateWorkflowLifecycle(con, observeWorkflowMigrationRetainedRuns);

        // DuckDB refuses ALTER TABLE while indexes depend on the table, so the
        // workflow_runs indexes are created only after the lifecycle migration
        // has applied its column constraints. The CHECKPOINT flushes the
        // migration's row versions first; DuckDB also refuses CREATE INDEX over
        // outstanding updates.
        using var workflowRunIndexCmd = con.CreateCommand();
        workflowRunIndexCmd.CommandText = string.Concat(
            "CHECKPOINT;\n",
            WorkflowRunDbRow.IndexesDdl);
        workflowRunIndexCmd.ExecuteNonQuery();

        using var obsoleteWorkflowProjectionCmd = con.CreateCommand();
        obsoleteWorkflowProjectionCmd.CommandText = """
                                                    DROP TABLE IF EXISTS workflow_projection_nodes;
                                                    DROP TABLE IF EXISTS workflow_projection_edges;
                                                    DROP TABLE IF EXISTS workflow_projection_state;
                                                    """;
        obsoleteWorkflowProjectionCmd.ExecuteNonQuery();

        VerifyPersistedPrimaryKeys(con);
    }

    private static void MigrateWorkflowLifecycle(
        DuckDBConnection con,
        Action<int>? observeWorkflowMigrationRetainedRuns)
    {
        using var transaction = con.BeginTransaction();
        using (var prepare = con.CreateCommand())
        {
            prepare.Transaction = transaction;
            prepare.CommandText = """
                                  CREATE TABLE IF NOT EXISTS qyl_storage_migrations (
                                      migration_id VARCHAR PRIMARY KEY,
                                      applied_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
                                  );
                                  CREATE TABLE IF NOT EXISTS workflow_checkpoint_repairs (
                                      project_id VARCHAR NOT NULL,
                                      run_id VARCHAR NOT NULL,
                                      run_generation VARCHAR NOT NULL,
                                      latest_journal_sequence UBIGINT NOT NULL,
                                      PRIMARY KEY (project_id, run_id, run_generation)
                                  );
                                  CREATE TABLE IF NOT EXISTS workflow_checkpoint_clock (
                                      singleton BOOLEAN PRIMARY KEY CHECK (singleton),
                                      current_epoch UBIGINT NOT NULL
                                  );
                                  INSERT INTO workflow_checkpoint_clock
                                  VALUES (TRUE, 0)
                                  ON CONFLICT DO NOTHING;
                                  UPDATE workflow_runs
                                  SET run_generation =
                                          replace(lower(uuid()::VARCHAR), '-', ''),
                                      active_checkpoint_sequence = 0,
                                      active_checkpoint_id = NULL,
                                      active_checkpoint_storage_key = NULL,
                                      projection_failure_sequence = NULL,
                                      projection_failure_kind = NULL,
                                      projection_failure_configuration = NULL,
                                      projection_failure_semantic = NULL
                                  WHERE NOT regexp_full_match(
                                      COALESCE(run_generation, ''),
                                      '^[0-9a-f]{12}4[0-9a-f]{3}[89ab][0-9a-f]{15}$');
                                  UPDATE workflow_runs
                                  SET active_checkpoint_sequence = 0,
                                      active_checkpoint_id = NULL,
                                      active_checkpoint_storage_key = NULL
                                  WHERE active_checkpoint_id IS NOT NULL
                                    AND NOT regexp_full_match(
                                        active_checkpoint_id,
                                        '^[0-9a-f]{16}-[0-9a-f]{64}\.json$');
                                  UPDATE workflow_runs
                                  SET last_activity_at =
                                      COALESCE(
                                          updated_at,
                                          created_at,
                                          started_at,
                                          current_timestamp)
                                  WHERE last_activity_at IS NULL;
                                  UPDATE workflow_runs
                                  SET latest_journal_sequence =
                                          coalesce(latest_journal_sequence, 0),
                                      event_count = coalesce(event_count, 0),
                                      projection_input_bytes =
                                          coalesce(projection_input_bytes, 0),
                                      immutable_projection_input_bytes =
                                          coalesce(immutable_projection_input_bytes, 0),
                                      dynamic_projection_input_bytes =
                                          coalesce(dynamic_projection_input_bytes, 0),
                                      next_command_sequence =
                                          coalesce(next_command_sequence, 1),
                                      next_control_event_source_sequence =
                                          coalesce(next_control_event_source_sequence, 1),
                                      active_checkpoint_sequence =
                                          coalesce(active_checkpoint_sequence, 0),
                                      checkpoint_manifest_epoch =
                                          coalesce(checkpoint_manifest_epoch, 0);
                                  ALTER TABLE workflow_runs
                                      ALTER COLUMN last_activity_at SET DEFAULT current_timestamp;
                                  """;
            prepare.ExecuteNonQuery();
        }

        RepairWorkflowManifestIdentities(con, transaction);

        const string migrationId = "workflow-lifecycle-v2";
        var applied = false;
        using (var marker = con.CreateCommand())
        {
            marker.Transaction = transaction;
            marker.CommandText = """
                                 SELECT count(*)
                                 FROM qyl_storage_migrations
                                 WHERE migration_id = $1
                                 """;
            AddParameters(marker, migrationId);
            applied = Convert.ToInt32(
                marker.ExecuteScalar(),
                CultureInfo.InvariantCulture) is not 0;
        }

        if (!applied)
        {
            BackfillWorkflowLifecycle(
                con,
                transaction,
                observeWorkflowMigrationRetainedRuns);
            using var marker = con.CreateCommand();
            marker.Transaction = transaction;
            marker.CommandText = """
                                 INSERT INTO qyl_storage_migrations (migration_id)
                                 VALUES ($1)
                                 """;
            AddParameters(marker, migrationId);
            marker.ExecuteNonQuery();
        }

        transaction.Commit();
        ApplyWorkflowLifecycleRequiredConstraints(con);
    }

    private static void ApplyWorkflowLifecycleRequiredConstraints(
        DuckDBConnection con)
    {
        const string migrationId = "workflow-lifecycle-required-columns-v2";
        using (var marker = con.CreateCommand())
        {
            marker.CommandText = """
                                 SELECT count(*)
                                 FROM qyl_storage_migrations
                                 WHERE migration_id = $1
                                 """;
            AddParameters(marker, migrationId);
            if (Convert.ToInt32(
                    marker.ExecuteScalar(),
                    CultureInfo.InvariantCulture) is not 0)
            {
                return;
            }
        }

        using (var constraints = con.CreateCommand())
        {
            // Runs outside the backfill transaction: SET NOT NULL rebuilds the
            // primary-key index, and DuckDB refuses that while the transaction
            // holds outstanding updates. The CHECKPOINT flushes those versions.
            constraints.CommandText = """
                                      CHECKPOINT;
                                      DROP INDEX IF EXISTS idx_workflow_runs_active_checkpoint_storage_key;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN run_generation SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN latest_journal_sequence SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN event_count SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN projection_input_bytes SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN immutable_projection_input_bytes SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN dynamic_projection_input_bytes SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN next_command_sequence SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN next_control_event_source_sequence SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN active_checkpoint_sequence SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN checkpoint_manifest_epoch SET NOT NULL;
                                      ALTER TABLE workflow_runs
                                          ALTER COLUMN last_activity_at SET NOT NULL;
                                      """;
            constraints.ExecuteNonQuery();
        }

        using var record = con.CreateCommand();
        record.CommandText = """
                             INSERT INTO qyl_storage_migrations (migration_id)
                             VALUES ($1)
                             """;
        AddParameters(record, migrationId);
        record.ExecuteNonQuery();
    }

    private static void RepairWorkflowManifestIdentities(
        DuckDBConnection con,
        DbTransaction transaction)
    {
        const int pageSize = 256;
        string? projectCursor = null;
        string? runCursor = null;
        while (true)
        {
            var manifests = new List<WorkflowManifestMigrationRow>(pageSize);
            using (var read = con.CreateCommand())
            {
                read.Transaction = transaction;
                if (projectCursor is null)
                {
                    read.CommandText = """
                                       SELECT project_id,
                                              run_id,
                                              run_generation,
                                              active_checkpoint_sequence,
                                              active_checkpoint_id,
                                              active_checkpoint_storage_key
                                       FROM workflow_runs
                                       WHERE active_checkpoint_sequence <> 0
                                          OR active_checkpoint_id IS NOT NULL
                                          OR active_checkpoint_storage_key IS NOT NULL
                                       ORDER BY project_id, run_id
                                       LIMIT $1
                                       """;
                    AddParameters(read, pageSize);
                }
                else
                {
                    read.CommandText = """
                                       SELECT project_id,
                                              run_id,
                                              run_generation,
                                              active_checkpoint_sequence,
                                              active_checkpoint_id,
                                              active_checkpoint_storage_key
                                       FROM workflow_runs
                                       WHERE (
                                               active_checkpoint_sequence <> 0
                                            OR active_checkpoint_id IS NOT NULL
                                            OR active_checkpoint_storage_key IS NOT NULL
                                       )
                                         AND (
                                             project_id > $1 OR
                                             (project_id = $1 AND run_id > $2)
                                         )
                                       ORDER BY project_id, run_id
                                       LIMIT $3
                                       """;
                    AddParameters(read, projectCursor, runCursor!, pageSize);
                }

                using var reader = read.ExecuteReader();
                while (reader.Read())
                {
                    manifests.Add(new WorkflowManifestMigrationRow(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        DuckDbValueReader.ReadUInt64(reader, 3, 0),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5)));
                }
            }

            if (manifests.Count is 0)
                return;

            foreach (var manifest in manifests)
            {
                var valid = manifest.Sequence is not 0 &&
                            manifest.CheckpointId is not null &&
                            WorkflowCheckpointStore.IsCanonicalCheckpointId(
                                manifest.CheckpointId,
                                manifest.Sequence);
                var canonicalStorageKey = valid
                    ? WorkflowCheckpointStore.CanonicalStorageIdentity(
                        manifest.ProjectId,
                        manifest.RunId,
                        manifest.RunGeneration,
                        manifest.Sequence,
                        manifest.CheckpointId)
                    : null;
                using var repair = con.CreateCommand();
                repair.Transaction = transaction;
                repair.CommandText = """
                                     UPDATE workflow_runs
                                     SET active_checkpoint_sequence = $1,
                                         active_checkpoint_id = $2,
                                         active_checkpoint_storage_key = $3
                                     WHERE project_id = $4
                                       AND run_id = $5
                                       AND run_generation = $6
                                       AND active_checkpoint_sequence = $7
                                       AND active_checkpoint_id IS NOT DISTINCT FROM $8
                                       AND active_checkpoint_storage_key
                                           IS NOT DISTINCT FROM $9
                                     """;
                AddParameters(
                    repair,
                    valid ? (decimal)manifest.Sequence : 0m,
                    DbValue(valid ? manifest.CheckpointId : null),
                    DbValue(canonicalStorageKey),
                    manifest.ProjectId,
                    manifest.RunId,
                    manifest.RunGeneration,
                    (decimal)manifest.Sequence,
                    DbValue(manifest.CheckpointId),
                    DbValue(manifest.StorageKey));
                repair.ExecuteNonQuery();
            }

            var last = manifests[^1];
            projectCursor = last.ProjectId;
            runCursor = last.RunId;
        }
    }

    private sealed record WorkflowManifestMigrationRow(
        string ProjectId,
        string RunId,
        string RunGeneration,
        ulong Sequence,
        string? CheckpointId,
        string? StorageKey);

    private static void BackfillWorkflowLifecycle(
        DuckDBConnection con,
        DbTransaction transaction,
        Action<int>? observeRetainedRuns)
    {
        using (var validate = con.CreateCommand())
        {
            validate.Transaction = transaction;
            validate.CommandText = """
                                   SELECT count(*)
                                   FROM workflow_events AS event
                                   LEFT JOIN workflow_runs AS run
                                     ON run.project_id = event.project_id
                                    AND run.run_id = event.run_id
                                   WHERE run.run_id IS NULL
                                   """;
            if (Convert.ToInt64(
                    validate.ExecuteScalar(),
                    CultureInfo.InvariantCulture) is not 0)
            {
                throw new InvalidDataException(
                    "Workflow journal contains an event without its owning run.");
            }
        }

        using (var create = con.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = """
                                 CREATE TEMP TABLE workflow_lifecycle_backfill (
                                     project_id VARCHAR NOT NULL,
                                     run_id VARCHAR NOT NULL,
                                     latest_journal_sequence UBIGINT NOT NULL,
                                     event_count BIGINT NOT NULL,
                                     projection_input_bytes BIGINT NOT NULL,
                                     immutable_projection_input_bytes BIGINT NOT NULL,
                                     dynamic_projection_input_bytes BIGINT NOT NULL,
                                     next_command_sequence UBIGINT NOT NULL,
                                     next_control_event_source_sequence UBIGINT NOT NULL,
                                     active_checkpoint_storage_key VARCHAR,
                                     PRIMARY KEY (project_id, run_id)
                                 )
                                 """;
            create.ExecuteNonQuery();
        }

        string? projectCursor = null;
        string? runCursor = null;
        while (true)
        {
            WorkflowRunStorageRow? run;
            using (var readRun = con.CreateCommand())
            {
                readRun.Transaction = transaction;
                if (projectCursor is null)
                {
                    readRun.CommandText =
                        "SELECT " + WorkflowRunDbRow.SelectColumnList + """
                        FROM workflow_runs
                        ORDER BY project_id, run_id
                        LIMIT 1
                        """;
                }
                else
                {
                    readRun.CommandText =
                        "SELECT " + WorkflowRunDbRow.SelectColumnList + """
                        FROM workflow_runs
                        WHERE project_id > $1
                           OR (project_id = $1 AND run_id > $2)
                        ORDER BY project_id, run_id
                        LIMIT 1
                        """;
                    AddParameters(readRun, projectCursor, runCursor!);
                }
                using var reader = readRun.ExecuteReader();
                run = reader.Read() ? ReadWorkflowRun(reader) : null;
            }
            if (run is null)
                break;

            observeRetainedRuns?.Invoke(1);
            try
            {
                var state = new WorkflowLifecycleMigrationState(run);
                using (var readEvents = con.CreateCommand())
                {
                    readEvents.Transaction = transaction;
                    readEvents.CommandText =
                        "SELECT " + WorkflowEventDbRow.SelectColumnList + """
                        FROM workflow_events
                        WHERE project_id = $1 AND run_id = $2
                        ORDER BY journal_sequence
                        """;
                    AddParameters(readEvents, run.ProjectId, run.RunId);
                    using var reader = readEvents.ExecuteReader();
                    while (reader.Read())
                    {
                        state.AddEvent(ReadWorkflowEvent(
                            WorkflowEventDbRow.MapFromReader(reader)));
                    }
                }

                using (var readCommandSequence = con.CreateCommand())
                {
                    readCommandSequence.Transaction = transaction;
                    readCommandSequence.CommandText = """
                                                      SELECT max(command_sequence)
                                                      FROM workflow_commands
                                                      WHERE project_id = $1 AND run_id = $2
                                                      """;
                    AddParameters(
                        readCommandSequence,
                        run.ProjectId,
                        run.RunId);
                    var maximum = readCommandSequence.ExecuteScalar();
                    if (maximum is not null and not DBNull)
                    {
                        state.NextCommandSequence = checked(
                            Convert.ToUInt64(
                                maximum,
                                CultureInfo.InvariantCulture) + 1);
                    }
                }

                using var insert = con.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                                     INSERT INTO workflow_lifecycle_backfill
                                     VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
                                     """;
                AddParameters(
                    insert,
                    state.Run.ProjectId,
                    state.Run.RunId,
                    (decimal)state.LatestJournalSequence,
                    state.EventCount,
                    state.ProjectionInputBytes,
                    state.ImmutableProjectionInputBytes,
                    state.DynamicProjectionInputBytes,
                    (decimal)state.NextCommandSequence,
                    (decimal)state.NextControlEventSourceSequence,
                    DbValue(state.ActiveCheckpointStorageKey));
                insert.ExecuteNonQuery();
            }
            finally
            {
                observeRetainedRuns?.Invoke(0);
            }

            projectCursor = run.ProjectId;
            runCursor = run.RunId;
        }

        using (var apply = con.CreateCommand())
        {
            apply.Transaction = transaction;
            apply.CommandText = """
                                UPDATE workflow_runs AS run
                                SET latest_journal_sequence = migration.latest_journal_sequence,
                                    event_count = migration.event_count,
                                    projection_input_bytes = migration.projection_input_bytes,
                                    immutable_projection_input_bytes =
                                        migration.immutable_projection_input_bytes,
                                    dynamic_projection_input_bytes =
                                        migration.dynamic_projection_input_bytes,
                                    next_command_sequence = migration.next_command_sequence,
                                    next_control_event_source_sequence =
                                        migration.next_control_event_source_sequence,
                                    active_checkpoint_storage_key =
                                        migration.active_checkpoint_storage_key
                                FROM workflow_lifecycle_backfill AS migration
                                WHERE run.project_id = migration.project_id
                                  AND run.run_id = migration.run_id;

                                DELETE FROM workflow_client_journal_ranges;
                                DELETE FROM workflow_client_journal;

                                INSERT INTO workflow_client_journal
                                    (project_id, run_id, client_id, acknowledged_source_sequence)
                                WITH distinct_sources AS (
                                    SELECT DISTINCT
                                        project_id,
                                        run_id,
                                        client_id,
                                        source_sequence
                                    FROM workflow_events
                                    WHERE source_sequence > 0
                                ),
                                ranked AS (
                                    SELECT
                                        project_id,
                                        run_id,
                                        client_id,
                                        source_sequence,
                                        row_number() OVER (
                                            PARTITION BY project_id, run_id, client_id
                                            ORDER BY source_sequence) AS source_rank
                                    FROM distinct_sources
                                )
                                SELECT
                                    project_id,
                                    run_id,
                                    client_id,
                                    coalesce(
                                        min(source_rank - 1)
                                            FILTER (WHERE source_sequence <> source_rank),
                                        max(source_rank),
                                        0)::UBIGINT
                                FROM ranked
                                GROUP BY project_id, run_id, client_id;

                                INSERT INTO workflow_client_journal_ranges
                                    (project_id, run_id, client_id, range_start, range_end)
                                WITH distinct_sources AS (
                                    SELECT DISTINCT
                                        event.project_id,
                                        event.run_id,
                                        event.client_id,
                                        event.source_sequence
                                    FROM workflow_events AS event
                                    JOIN workflow_client_journal AS journal
                                      ON journal.project_id = event.project_id
                                     AND journal.run_id = event.run_id
                                     AND journal.client_id = event.client_id
                                    WHERE event.source_sequence >
                                          journal.acknowledged_source_sequence
                                ),
                                islands AS (
                                    SELECT
                                        project_id,
                                        run_id,
                                        client_id,
                                        source_sequence,
                                        source_sequence::HUGEINT -
                                            row_number() OVER (
                                                PARTITION BY project_id, run_id, client_id
                                                ORDER BY source_sequence) AS island
                                    FROM distinct_sources
                                )
                                SELECT
                                    project_id,
                                    run_id,
                                    client_id,
                                    min(source_sequence),
                                    max(source_sequence)
                                FROM islands
                                GROUP BY project_id, run_id, client_id, island;

                                DROP TABLE workflow_lifecycle_backfill;
                                """;
            apply.ExecuteNonQuery();
        }
    }

    private sealed class WorkflowLifecycleMigrationState
    {
        public WorkflowLifecycleMigrationState(WorkflowRunStorageRow run)
        {
            Run = run;
            ImmutableProjectionInputBytes =
                WorkflowCanonicalization.MeasureImmutableRunInput(run);
            DynamicProjectionInputBytes =
                WorkflowCanonicalization.MeasureDynamicRunInput(run);
            ProjectionInputBytes = checked(
                ImmutableProjectionInputBytes + DynamicProjectionInputBytes);
            ActiveCheckpointStorageKey = run.ActiveCheckpointId is null
                ? null
                : WorkflowCheckpointStore.CanonicalStorageIdentity(run);
        }

        public WorkflowRunStorageRow Run { get; }

        public ulong LatestJournalSequence { get; private set; }

        public long EventCount { get; private set; }

        public long ProjectionInputBytes { get; private set; }

        public long ImmutableProjectionInputBytes { get; }

        public long DynamicProjectionInputBytes { get; }

        public ulong NextCommandSequence { get; set; } = 1;

        public ulong NextControlEventSourceSequence { get; private set; } = 1;

        public string? ActiveCheckpointStorageKey { get; }

        public void AddEvent(WorkflowEventStorageRow workflowEvent)
        {
            var expectedSequence = checked((ulong)EventCount + 1);
            if (workflowEvent.JournalSequence != expectedSequence)
            {
                throw new InvalidDataException(
                    "Workflow journal sequence is not contiguous.");
            }
            LatestJournalSequence = workflowEvent.JournalSequence;
            EventCount = checked(EventCount + 1);
            ProjectionInputBytes = checked(
                ProjectionInputBytes +
                WorkflowCanonicalization.MeasureEventInput(workflowEvent));
            if (workflowEvent.ClientId == "collector-control")
            {
                NextControlEventSourceSequence = Math.Max(
                    NextControlEventSourceSequence,
                    checked(workflowEvent.SourceSequence + 1));
            }
        }
    }

    private static void VerifyPersistedPrimaryKeys(DuckDBConnection con)
    {
        (string Table, string Columns)[] expected =
        [
            (SpanStorageRow.TableName, SpanStorageRow.PrimaryKeyColumnsCsv),
            (LogStorageRow.TableName, LogStorageRow.PrimaryKeyColumnsCsv),
            (WorkflowRunDbRow.TableName, WorkflowRunDbRow.PrimaryKeyColumnsCsv),
            (WorkflowEventDbRow.TableName, WorkflowEventDbRow.PrimaryKeyColumnsCsv),
            (WorkflowContentDbRow.TableName, WorkflowContentDbRow.PrimaryKeyColumnsCsv),
            (WorkflowContentReferenceDbRow.TableName, WorkflowContentReferenceDbRow.PrimaryKeyColumnsCsv),
            (WorkflowClientJournalDbRow.TableName, WorkflowClientJournalDbRow.PrimaryKeyColumnsCsv),
            (WorkflowClientJournalRangeDbRow.TableName, WorkflowClientJournalRangeDbRow.PrimaryKeyColumnsCsv),
            (WorkflowCommandDbRow.TableName, WorkflowCommandDbRow.PrimaryKeyColumnsCsv)
        ];

        foreach (var (table, columns) in expected)
            VerifyPersistedPrimaryKey(con, table, columns);
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

        public void AddDescendingCursor(
            string primaryColumn,
            string tieBreakerColumn,
            object primaryValue,
            object tieBreakerValue)
        {
            var primaryParameter = $"${_paramIndex++}";
            var tieBreakerParameter = $"${_paramIndex++}";
            _conditions.Add(
                $"({primaryColumn} < {primaryParameter} OR " +
                $"({primaryColumn} = {primaryParameter} AND {tieBreakerColumn} < {tieBreakerParameter}))");
            _parameters.Add(new DuckDBParameter { Value = primaryValue });
            _parameters.Add(new DuckDBParameter { Value = tieBreakerValue });
        }

        public readonly string WhereClause =>
            _conditions.Count > 0 ? $"WHERE {string.Join(" AND ", _conditions)}" : "";

        public readonly string NextParam => $"${_paramIndex}";

        public readonly void ApplyTo(DuckDBCommand cmd) => cmd.Parameters.AddRange(_parameters);
    }

}
