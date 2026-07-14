using DuckDB.NET.Data;

using static System.Threading.Volatile;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore : IQylStore
{

    private const int MaxSpansPerBatch = 100;

    private const int MaxLogsPerBatch = 150;

    private const int MaxProfilesPerBatch = 50;
    private static readonly TimeSpan s_shutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _cts = new();
    private readonly Func<CancellationToken, ValueTask>? _beforeWrite;
    private readonly bool _isInMemory;
    private readonly int _jobQueueCapacity;
    private readonly Channel<WriteJob> _jobs;
    private readonly Channel<IReadJob>? _reads;
    private readonly Thread[] _readerThreads;
    private readonly Task _writerTask;

    private int _disposed;
    private long _queuedWriteJobs;


    public DuckDbStore(
        string databasePath = "qyl.duckdb",
        int jobQueueCapacity = 1000,
        int maxConcurrentReads = 8,
        int readQueueCapacity = 1000,
        string? memoryLimit = null,
        int? threads = null,
        string? tempDirectory = null,
        Func<CancellationToken, ValueTask>? beforeWrite = null)
    {
        _isInMemory = databasePath == ":memory:";
        _jobQueueCapacity = Math.Max(1, jobQueueCapacity);
        _beforeWrite = beforeWrite;
        Connection = new DuckDBConnection($"DataSource={databasePath}");
        Connection.Open();
        ConfigureDatabase(Connection, memoryLimit, threads, tempDirectory);
        InitializeSchema(Connection);

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
                var con = new DuckDBConnection($"DataSource={databasePath}");
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
        var job = new WriteJob<T>(operation);
        Interlocked.Increment(ref _queuedWriteJobs);
        try
        {
            if (!_jobs.Writer.TryWrite(job))
                throw new QylStoreUnavailableException("DuckDB write queue is at capacity.");
        }
        catch
        {
            Interlocked.Decrement(ref _queuedWriteJobs);
            throw;
        }

        return await job.Task.WaitAsync(ct).ConfigureAwait(false);
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
        });
        Interlocked.Increment(ref _queuedWriteJobs);
        try
        {
            if (!_jobs.Writer.TryWrite(job))
                throw new QylStoreUnavailableException("DuckDB write queue is at capacity.");
        }
        catch
        {
            Interlocked.Decrement(ref _queuedWriteJobs);
            throw;
        }

        await job.Task.WaitAsync(ct).ConfigureAwait(false);
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


    public async Task InsertProfilesAsync(IReadOnlyList<ProfileDetail> results,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (results.Count is 0)
            return;

        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var tx = await con.BeginTransactionAsync(token).ConfigureAwait(false);

            var headers = results.Select(static r => r.Profile).ToList();
            await InsertRowsBatchedAsync(con, tx, headers, ProfileStorageRow.AddParameters,
                ProfileStorageRow.BuildMultiRowInsertSql, MaxProfilesPerBatch, token);

            await InsertRowsBatchedAsync(con, tx, results.SelectMany(static r => r.Functions).ToList(),
                ProfileFunctionRow.AddParameters, ProfileFunctionRow.BuildMultiRowInsertSql, 200, token);

            await InsertRowsBatchedAsync(con, tx, results.SelectMany(static r => r.Locations).ToList(),
                ProfileLocationRow.AddParameters, ProfileLocationRow.BuildMultiRowInsertSql, 200, token);

            await InsertRowsBatchedAsync(con, tx, results.SelectMany(static r => r.Mappings).ToList(),
                ProfileMappingRow.AddParameters, ProfileMappingRow.BuildMultiRowInsertSql, 200, token);

            await InsertRowsBatchedAsync(con, tx, results.SelectMany(static r => r.Samples).ToList(),
                ProfileSampleRow.AddParameters, ProfileSampleRow.BuildMultiRowInsertSql, 200, token);

            await InsertRowsBatchedAsync(con, tx, results.SelectMany(static r => r.Stacks).ToList(),
                ProfileStackRow.AddParameters, ProfileStackRow.BuildMultiRowInsertSql, 200, token);

            await tx.CommitAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
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

    public Task<IReadOnlyList<ProfileStorageRow>> GetProfilesAsync(
        string projectId,
        string? sessionId = null,
        string? traceId = null,
        string? spanId = null,
        string? serviceName = null,
        string? sampleType = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (limit is < 1 or > 1_000)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Profile limit must be between 1 and 1000.");

        return ExecuteReadAsync<IReadOnlyList<ProfileStorageRow>>(con =>
        {
            var profiles = new List<ProfileStorageRow>();
            var qb = new QueryBuilder();

            qb.Add("project_id = $N", projectId);
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
            cmd.CommandText = "SELECT " + ProfileStorageRow.SelectColumnList
                              + " FROM profiles " + qb.WhereClause
                              + " ORDER BY time_unix_nano DESC LIMIT "
                              + qb.NextParam.ToString(CultureInfo.InvariantCulture);

            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                profiles.Add(ProfileStorageRow.MapFromReader(reader));
            }

            return profiles;
        }, ct);
    }

    public Task<ProfileDetail?> GetProfileDetailAsync(
        string profileId,
        string projectId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<ProfileDetail?>(con =>
        {
            ProfileStorageRow? header = null;
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT " + ProfileStorageRow.SelectColumnList + " FROM profiles WHERE project_id = $1 AND profile_id = $2 LIMIT 1";
                cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
                cmd.Parameters.Add(new DuckDBParameter { Value = profileId });
                using var r = cmd.ExecuteReader();
                if (r.Read()) header = ProfileStorageRow.MapFromReader(r);
            }

            if (header is null) return null;

            var functions = ReadChildRows(con, header.ProjectId, profileId,
                "SELECT " + ProfileFunctionRow.SelectColumnList + " FROM profile_functions WHERE project_id = $1 AND profile_id = $2 ORDER BY ordinal",
                static r => ProfileFunctionRow.MapFromReader(r));

            var locations = ReadChildRows(con, header.ProjectId, profileId,
                "SELECT " + ProfileLocationRow.SelectColumnList + " FROM profile_locations WHERE project_id = $1 AND profile_id = $2 ORDER BY ordinal",
                static r => ProfileLocationRow.MapFromReader(r));

            var mappings = ReadChildRows(con, header.ProjectId, profileId,
                "SELECT " + ProfileMappingRow.SelectColumnList + " FROM profile_mappings WHERE project_id = $1 AND profile_id = $2 ORDER BY ordinal",
                static r => ProfileMappingRow.MapFromReader(r));

            var samples = ReadChildRows(con, header.ProjectId, profileId,
                "SELECT " + ProfileSampleRow.SelectColumnList + " FROM profile_samples WHERE project_id = $1 AND profile_id = $2 ORDER BY ordinal",
                static r => ProfileSampleRow.MapFromReader(r));

            var stacks = ReadChildRows(con, header.ProjectId, profileId,
                "SELECT " + ProfileStackRow.SelectColumnList + " FROM profile_stacks WHERE project_id = $1 AND profile_id = $2 ORDER BY ordinal",
                static r => ProfileStackRow.MapFromReader(r));

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

    private static IReadOnlyList<T> ReadChildRows<T>(DuckDBConnection connection, string projectId, string profileId,
        string sql, Func<DbDataReader, T> mapper)
    {
        var rows = new List<T>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
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

    // Per table: CREATE TABLE IF NOT EXISTS (full schema for new databases), then the generated
    // ALTER TABLE ... ADD COLUMN IF NOT EXISTS migration (existing databases persisted by an older
    // schema gain missing columns), then CREATE INDEX IF NOT EXISTS — indexes last, because an
    // index may target a column the migration just added.
    private static void InitializeSchema(DuckDBConnection con)
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

        using var profilesCmd = con.CreateCommand();
        profilesCmd.CommandText = string.Concat(
            ProfileStorageRow.CreateTableDdl, "\n",
            ProfileFunctionRow.CreateTableDdl, "\n",
            ProfileLocationRow.CreateTableDdl, "\n",
            ProfileMappingRow.CreateTableDdl, "\n",
            ProfileSampleRow.CreateTableDdl, "\n",
            ProfileStackRow.CreateTableDdl, "\n",
            ProfileStorageRow.MigrateTableDdl, "\n",
            ProfileFunctionRow.MigrateTableDdl, "\n",
            ProfileLocationRow.MigrateTableDdl, "\n",
            ProfileMappingRow.MigrateTableDdl, "\n",
            ProfileSampleRow.MigrateTableDdl, "\n",
            ProfileStackRow.MigrateTableDdl, "\n",
            ProfileStorageRow.IndexesDdl, "\n",
            ProfileSampleRow.IndexesDdl);
        profilesCmd.ExecuteNonQuery();

        using var cmd = con.CreateCommand();
        cmd.CommandText = string.Concat(
            SpanStorageRow.CreateTableDdl, "\n",
            SpanStorageRow.MigrateTableDdl, "\n",
            SpanStorageRow.IndexesDdl);
        cmd.ExecuteNonQuery();

        VerifyPersistedPrimaryKeys(con);
    }

    // ALTER can add missing columns, but it cannot rewrite a primary key — and the generated
    // inserts hard-depend on the current key via ON CONFLICT. A database persisted before a key
    // change would boot green and then fail every insert with a binder error the fire-and-forget
    // writer swallows: silent total data loss behind a healthy /health. Refuse such a database
    // loudly at boot instead.
    private static void VerifyPersistedPrimaryKeys(DuckDBConnection con)
    {
        VerifyPersistedPrimaryKey(con, SpanStorageRow.TableName, SpanStorageRow.PrimaryKeyColumnsCsv);
        VerifyPersistedPrimaryKey(con, LogStorageRow.TableName, LogStorageRow.PrimaryKeyColumnsCsv);
        VerifyPersistedPrimaryKey(con, ProfileStorageRow.TableName, ProfileStorageRow.PrimaryKeyColumnsCsv);
        VerifyPersistedPrimaryKey(con, ProfileFunctionRow.TableName, ProfileFunctionRow.PrimaryKeyColumnsCsv);
        VerifyPersistedPrimaryKey(con, ProfileLocationRow.TableName, ProfileLocationRow.PrimaryKeyColumnsCsv);
        VerifyPersistedPrimaryKey(con, ProfileMappingRow.TableName, ProfileMappingRow.PrimaryKeyColumnsCsv);
        VerifyPersistedPrimaryKey(con, ProfileSampleRow.TableName, ProfileSampleRow.PrimaryKeyColumnsCsv);
        VerifyPersistedPrimaryKey(con, ProfileStackRow.TableName, ProfileStackRow.PrimaryKeyColumnsCsv);
    }

    private static void VerifyPersistedPrimaryKey(DuckDBConnection con, string tableName, string expectedCsv)
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
        cmd.Parameters.Add(new DuckDBParameter { Value = tableName });
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            actual.Add(reader.GetString(0));

        if (!expected.SetEquals(actual))
        {
            throw new InvalidOperationException(
                $"The persisted database's '{tableName}' table declares primary key " +
                $"({string.Join(", ", actual.Order(StringComparer.Ordinal))}) but this build requires " +
                $"({string.Join(", ", expected.Order(StringComparer.Ordinal))}). Startup migration adds columns but never " +
                "rewrites keys, and a drifted key breaks the ON CONFLICT upsert target silently. " +
                "Move the database file aside (dev data) or re-ingest into a fresh one.");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Read(ref _disposed) is not 0, this);

    private abstract class WriteJob
    {
        public abstract ValueTask ExecuteAsync(DuckDBConnection con, CancellationToken ct);

        public virtual void OnAborted(Exception error)
        {
        }
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

        public readonly string WhereClause =>
            _conditions.Count > 0 ? $"WHERE {string.Join(" AND ", _conditions)}" : "";

        public readonly string NextParam => $"${_paramIndex}";

        public readonly void ApplyTo(DuckDBCommand cmd) => cmd.Parameters.AddRange(_parameters);
    }

}
