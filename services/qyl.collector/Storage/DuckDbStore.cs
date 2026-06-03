using DuckDB.NET.Data;

using static System.Threading.Volatile;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore : IAsyncDisposable
{

    private const int MaxSpansPerBatch = 100;

    private const int MaxLogsPerBatch = 150;

    private const int MaxProfilesPerBatch = 50;
    private static readonly TimeSpan s_shutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _cts = new();
    private readonly bool _isInMemory;
    private readonly int _jobQueueCapacity;
    private readonly Channel<WriteJob> _jobs;

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
        _isInMemory = databasePath == ":memory:";
        _jobQueueCapacity = Math.Max(1, jobQueueCapacity);
        Connection = new DuckDBConnection($"DataSource={databasePath}");
        Connection.Open();
        InitializeSchema(Connection);

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
        string projectId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<SpanStorageRow>>(con =>
        {
            var spans = new List<SpanStorageRow>();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT " + SpanStorageRow.SelectColumnList
                                        + " FROM spans WHERE project_id = $1 AND session_id = $2 ORDER BY start_time_unix_nano ASC";
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

            qb.Add("project_id = $N", projectId);
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
                                  (SELECT COUNT(DISTINCT COALESCE(session_id, trace_id)) FROM spans WHERE project_id = $1) as session_count,
                                  (SELECT COUNT(*) FROM logs WHERE project_id = $1) as log_count,
                                  (SELECT MIN(start_time_unix_nano) FROM spans WHERE project_id = $1) as oldest_span,
                                  (SELECT MAX(start_time_unix_nano) FROM spans WHERE project_id = $1) as newest_span
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });

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
    }

    private double GetWriteQueueUtilization()
    {
        var queued = Math.Max(0, Read(ref _queuedWriteJobs));
        return Math.Clamp((double)queued / _jobQueueCapacity, 0, 1);
    }

    public Task<long> GetSpanCountAsync(
        string projectId,
        CancellationToken ct = default) =>
        ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM spans WHERE project_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
            var result = cmd.ExecuteScalar();
            return result is long count ? count : 0;
        }, ct);

    public Task<long> GetLogCountAsync(
        string projectId,
        CancellationToken ct = default) =>
        ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM logs WHERE project_id = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = projectId });
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

    public Task<IReadOnlyList<ModelPricingRow>> GetActiveModelPricingAsync(CancellationToken ct = default) =>
        ExecuteReadAsync<IReadOnlyList<ModelPricingRow>>(static con =>
        {
            var result = new List<ModelPricingRow>();

            using var cmd = con.CreateCommand();
            cmd.CommandText = $"""
                               SELECT {ModelPricingRow.SelectColumnList}
                               FROM model_pricing
                               WHERE valid_to IS NULL
                               ORDER BY valid_from DESC
                               """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(ModelPricingRow.MapFromReader(reader));
            }

            return result;
        }, ct);

    public async Task InsertModelPricingSeedsAsync(
        IReadOnlyList<ModelPricingRow> entries,
        DateTimeOffset validFrom,
        CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, wct) =>
        {
            await using var tx = await con.BeginTransactionAsync(wct).ConfigureAwait(false);
            var rows = entries.Select(entry => entry with { ValidFrom = validFrom, ValidTo = null }).ToList();
            await InsertRowsBatchedAsync(con, tx, rows, ModelPricingRow.AddParameters,
                ModelPricingRow.BuildMultiRowInsertSql, rows.Count, wct);
            await tx.CommitAsync(wct).ConfigureAwait(false);
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
        ulong? after = null,
        string? afterLogId = null,
        ulong? before = null,
        string? serviceName = null,
        bool ascending = false,
        bool latestPageAscending = false,
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
            if (latestPageAscending)
            {
                if (after.HasValue)
                    throw new ArgumentException(
                        "Latest-page ascending log queries cannot be combined with an after cursor.",
                        nameof(after));

                cmd.CommandText = "SELECT " + LogStorageRow.SelectColumnList
                                  + " FROM (SELECT " + LogStorageRow.SelectColumnList
                                  + " FROM logs " + qb.WhereClause
                                  + " ORDER BY time_unix_nano DESC, log_id DESC LIMIT "
                                  + qb.NextParam.ToString(CultureInfo.InvariantCulture)
                                  + ") AS latest_logs ORDER BY time_unix_nano ASC, log_id ASC";
            }
            else
            {
                cmd.CommandText = "SELECT " + LogStorageRow.SelectColumnList
                                  + " FROM logs " + qb.WhereClause
                                  + " ORDER BY time_unix_nano " + sortDirection + ", log_id " + sortDirection + " LIMIT "
                                  + qb.NextParam.ToString(CultureInfo.InvariantCulture);
            }

            qb.ApplyTo(cmd);
            cmd.Parameters.Add(new DuckDBParameter { Value = limit });

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                logs.Add(LogStorageRow.MapFromReader(reader));

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
            cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 500) });

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

        await InsertRowsBatchedAsync(con, tx, batch.Spans, SpanStorageRow.AddParameters,
            SpanStorageRow.BuildMultiRowInsertSql, MaxSpansPerBatch, ct);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static void InitializeSchema(DuckDBConnection con)
    {
        using var logsCmd = con.CreateCommand();
        logsCmd.CommandText = string.Concat(
            LogStorageRow.CreateTableDdl, "\n",
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
            ProfileStorageRow.IndexesDdl, "\n",
            ProfileSampleRow.IndexesDdl);
        profilesCmd.ExecuteNonQuery();

        using var cmd = con.CreateCommand();
        cmd.CommandText = string.Concat(
            SpanStorageRow.CreateTableDdl, "\n",
            SpanStorageRow.IndexesDdl);
        cmd.ExecuteNonQuery();

        using var costCmd = con.CreateCommand();
        costCmd.CommandText = ModelPricingRow.CreateTableDdl;
        costCmd.ExecuteNonQuery();
    }

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

        public readonly string WhereClause =>
            _conditions.Count > 0 ? $"WHERE {string.Join(" AND ", _conditions)}" : "";

        public readonly string NextParam => $"${_paramIndex}";

        public readonly void ApplyTo(DuckDBCommand cmd) => cmd.Parameters.AddRange(_parameters);
    }

}
