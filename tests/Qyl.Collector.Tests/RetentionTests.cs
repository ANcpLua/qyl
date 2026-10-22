using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Qyl.Collector.Health;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion.Primitives;
using Qyl.Collector.Retention;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class RetentionTests(TestContext testContext)
{
    private static readonly DateTimeOffset s_now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task One_cycle_removes_expired_telemetry_without_orphans_or_boundary_loss()
    {
        var databasePath = DatabasePath("boundary");
        var cutoff = TimeConversions.ToUnixNanoUnsigned(s_now.AddDays(-30));

        try
        {
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 2);
            await store.EnqueueAsync(new SpanBatch(
            [
                CreateSpan("expired", "expired-root", cutoff - 20),
                CreateSpan("expired", "expired-child", cutoff - 10, "expired-root"),
                CreateSpan("fresh", "fresh-root", cutoff),
                CreateSpan("mixed", "mixed-old-root", cutoff - 20),
                CreateSpan("mixed", "mixed-fresh-child", cutoff + 10, "mixed-old-root")
            ]), testContext.CancellationToken);
            await store.InsertLogsAsync(
            [
                CreateLog("expired-log", cutoff - 1, "expired", "expired-child"),
                CreateLog("fresh-log", cutoff, "fresh", "fresh-root")
            ], testContext.CancellationToken);

            using var service = CreateService(store, days: 30);
            var result = await service.RunCycleAsync(testContext.CancellationToken);

            Assert.AreEqual(1, result.DeletedLogRows);
            Assert.AreEqual(2, result.DeletedSpanRows);
            Assert.IsEmpty(await store.GetTraceAsync(
                "expired", "default", testContext.CancellationToken));

            var spans = await store.GetSpansAsync(
                "default", 100, testContext.CancellationToken);
            Assert.AreSequenceEqual(
                ["fresh-root", "mixed-fresh-child", "mixed-old-root"],
                spans.Select(static span => span.SpanId).Order(StringComparer.Ordinal));

            var spanKeys = spans
                .Select(static span => (span.TraceId, span.SpanId))
                .ToHashSet();
            Assert.DoesNotContain(
                span => span.ParentSpanId is not null && !spanKeys.Contains((span.TraceId, span.ParentSpanId)),                spans);

            var logs = await store.GetLogsAsync(
                "default", limit: 100, ct: testContext.CancellationToken);
            var freshLog = Assert.ContainsSingle(logs);
            Assert.AreEqual("fresh-log", freshLog.LogId);
            Assert.Contains(span => span.TraceId == freshLog.TraceId,spans);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [TestMethod]
    public async Task Disabled_retention_is_idle_and_preserves_expired_rows()
    {
        var databasePath = DatabasePath("disabled");
        var writeCalls = 0;

        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeWrite: _ =>
                {
                    Interlocked.Increment(ref writeCalls);
                    return ValueTask.CompletedTask;
                });
            await store.EnqueueAsync(new SpanBatch(
            [
                CreateSpan("expired", "expired-root", 1)
            ]), testContext.CancellationToken);
            var writesAfterSeed = Volatile.Read(ref writeCalls);

            using var service = CreateService(store, days: 0);
            var result = await service.RunCycleAsync(testContext.CancellationToken);

            Assert.AreEqual(RetentionCycleOutcome.Empty, result);
            Assert.AreEqual(writesAfterSeed, Volatile.Read(ref writeCalls));
            Assert.ContainsSingle(await store.GetTraceAsync(
                "expired", "default", testContext.CancellationToken));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [TestMethod]
    public async Task Repeated_retention_cycles_bound_the_database_file_at_a_steady_state()
    {
        // The guarantee retention provides is a bounded file across repeated delete/ingest
        // cycles, not a single-cycle cap against a point-in-time size: checkpoint vacuum is
        // allowed to transiently rewrite live data and indexes into fresh blocks (the
        // experimental vacuum_rebuild_indexes path), so the honest invariant is that the
        // steady state stops trending upward once the free list has warmed up.
        var databasePath = DatabasePath("plateau");
        const int RowCount = 100_000;
        const int Cycles = 4;
        var oldTimestamp = TimeConversions.ToUnixNanoUnsigned(s_now.AddDays(-2));
        var currentTimestamp = TimeConversions.ToUnixNanoUnsigned(s_now);

        try
        {
            var random = new Random(314159);
            var bodies = new string[RowCount];
            var rows = new List<LogStorageRow>(RowCount + 100);
            for (var i = 0; i < 100; i++)
                rows.Add(CreateLog($"fresh-{i}", currentTimestamp));
            for (var i = 0; i < RowCount; i++)
            {
                var payload = new byte[256];
                random.NextBytes(payload);
                bodies[i] = Convert.ToBase64String(payload);
                rows.Add(CreateLog($"old-{i}", oldTimestamp, body: bodies[i]));
            }

            await using (var seedStore = new DuckDbStore(databasePath, maxConcurrentReads: 1))
                await seedStore.InsertLogsAsync(rows, testContext.CancellationToken);

            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            using var service = CreateService(store, days: 1);
            var seedCycle = await service.RunCycleAsync(testContext.CancellationToken);
            Assert.AreEqual(RowCount, seedCycle.DeletedLogRows);
            Assert.AreEqual(0, FileLength($"{databasePath}.wal"));

            var sizes = new long[Cycles];
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                rows.Clear();
                for (var i = 0; i < RowCount; i++)
                    rows.Add(CreateLog($"c{cycle}-{i}", oldTimestamp, body: bodies[i]));
                await store.InsertLogsAsync(rows, testContext.CancellationToken);

                var result = await service.RunCycleAsync(testContext.CancellationToken);
                Assert.AreEqual(RowCount, result.DeletedLogRows);
                sizes[cycle] = result.FileSizeAfterBytes;
            }

            // Cycles 1-2 establish the steady state; later cycles may oscillate around it but
            // must not trend upward. A real reuse failure appends roughly a full ingest of new
            // blocks every cycle (~50% growth), so the 10% allowance is a crisp discriminator.
            var establishedSteadyState = Math.Max(sizes[0], sizes[1]);
            var finalSize = sizes[Cycles - 1];
            Assert.IsLessThanOrEqualTo(
                establishedSteadyState * 110 / 100,
                finalSize,
                $"Expected repeated cycles to hold the file near {establishedSteadyState} bytes, but it "
                + $"trended upward to {finalSize} bytes (sizes: {string.Join(", ", sizes)}).");
            Assert.AreEqual(0, FileLength($"{databasePath}.wal"));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [TestMethod]
    public async Task Batched_retention_interleaves_with_active_ingest_without_deadlock()
    {
        var databasePath = DatabasePath("concurrency");
        var observeWrites = 0;
        var observedWriteCount = 0;
        var firstRetentionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRetention = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRetentionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecondRetention = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                jobQueueCapacity: 10,
                maxConcurrentReads: 1,
                beforeWrite: async token =>
                {
                    if (Volatile.Read(ref observeWrites) is 0)
                        return;

                    var call = Interlocked.Increment(ref observedWriteCount);
                    if (call is 1)
                    {
                        firstRetentionEntered.TrySetResult();
                        await releaseFirstRetention.Task.WaitAsync(token);
                    }
                    else if (call is 3)
                    {
                        secondRetentionEntered.TrySetResult();
                        await releaseSecondRetention.Task.WaitAsync(token);
                    }
                });

            var oldTimestamp = TimeConversions.ToUnixNanoUnsigned(s_now.AddDays(-2));
            var oldLogs = Enumerable.Range(0, 2501)
                .Select(index => CreateLog($"old-{index}", oldTimestamp))
                .ToArray();
            await store.InsertLogsAsync(oldLogs, testContext.CancellationToken);

            Volatile.Write(ref observeWrites, 1);
            using var service = CreateService(store, days: 1);
            var cycle = service.RunCycleAsync(testContext.CancellationToken);
            await firstRetentionEntered.Task.WaitAsync(testContext.CancellationToken);

            var ingest = store.EnqueueAsync(new SpanBatch(
            [
                CreateSpan(
                    "active-ingest",
                    "active-span",
                    TimeConversions.ToUnixNanoUnsigned(s_now))
            ]), testContext.CancellationToken).AsTask();
            releaseFirstRetention.TrySetResult();

            await ingest.WaitAsync(testContext.CancellationToken);
            await secondRetentionEntered.Task.WaitAsync(testContext.CancellationToken);
            Assert.IsFalse(cycle.IsCompleted);
            releaseSecondRetention.TrySetResult();

            var result = await cycle;
            Assert.AreEqual(2501, result.DeletedLogRows);
            Assert.IsGreaterThan(1, result.WriteBatches);
            Assert.ContainsSingle(await store.GetTraceAsync(
                "active-ingest", "default", testContext.CancellationToken));
        }
        finally
        {
            releaseFirstRetention.TrySetResult();
            releaseSecondRetention.TrySetResult();
            DeleteDatabase(databasePath);
        }
    }

    [TestMethod]
    public async Task Disk_pressure_health_is_degraded_and_carries_file_and_volume_numbers()
    {
        var databasePath = DatabasePath("health");

        try
        {
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            var check = new DuckDbHealthCheck(
                store,
                Options.Create(new RetentionOptions { StorageMinimumFreeMegabytes = long.MaxValue / (1024L * 1024L) }));

            var result = await check.CheckHealthAsync(
                new HealthCheckContext(),
                testContext.CancellationToken);

            Assert.AreEqual(HealthStatus.Degraded, result.Status);
            Assert.IsGreaterThan(0L, Assert.IsExactInstanceOfType<long>(result.Data["database_file_size_bytes"]));
            Assert.IsGreaterThanOrEqualTo(0L, Assert.IsExactInstanceOfType<long>(result.Data["storage_free_bytes"]));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [TestMethod]
    public void Retention_configuration_binds_the_three_operator_variables()
    {
        var defaults = BindRetention(new ConfigurationBuilder().Build());
        Assert.AreEqual(30, defaults.Days);
        Assert.AreEqual(TimeSpan.FromMinutes(60), defaults.Interval);
        Assert.AreEqual(2048L * 1024L * 1024L, defaults.StorageMinimumFreeBytes);

        var configured = BindRetention(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["QYL_RETENTION_DAYS"] = "0",
                    ["QYL_RETENTION_INTERVAL_MINUTES"] = "15",
                    ["QYL_STORAGE_MIN_FREE_MB"] = "4096"
                })
                .Build());
        Assert.IsFalse(configured.IsEnabled);
        Assert.AreEqual(TimeSpan.FromMinutes(15), configured.Interval);
        Assert.AreEqual(4096L * 1024L * 1024L, configured.StorageMinimumFreeBytes);
    }

    [TestMethod]
    public async Task A_cycle_expires_annotations_with_the_window_and_idempotency_keys_after_a_day()
    {
        var databasePath = DatabasePath("resources");
        var cutoff = TimeConversions.ToUnixNanoUnsigned(s_now.AddDays(-30));
        var dayAgo = TimeConversions.ToUnixNanoUnsigned(s_now.AddHours(-24));
        var ct = testContext.CancellationToken;

        try
        {
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 2);
            var annotation = new AnnotationRow
            {
                ProjectId = "alpha", AnnotationId = "stale", TargetKind = "trace",
                TraceId = CollectorApiTestHost.TraceId(1), Text = "old", CreatedAtUnixNano = cutoff - 10,
                UpdatedAtUnixNano = cutoff - 1, Etag = "\"1\""
            };
            await store.CreateAnnotationAsync(annotation, null, 0, ct: ct);
            // Edited inside the window: the last change, not the creation, counts.
            await store.CreateAnnotationAsync(
                annotation with { AnnotationId = "edited", UpdatedAtUnixNano = cutoff }, null, 0, ct: ct);
            var savedQuery = new SavedQueryRow
            {
                ProjectId = "alpha", SavedQueryId = "kept", Name = "kept", Signal = "traces",
                FilterJson = """{"signal":"traces"}""", CreatedAtUnixNano = 1, UpdatedAtUnixNano = 1, Etag = "\"1\""
            };
            await store.CreateSavedQueryAsync(savedQuery, new IdempotencyKeyRow
            {
                ProjectId = "alpha", Operation = "saved-queries", IdempotencyKey = "old", RequestHash = "h",
                ResourceId = "kept", CreatedAtUnixNano = dayAgo - 1
            }, 0, ct);
            await store.CreateSavedQueryAsync(savedQuery with { SavedQueryId = "kept-2", Name = "kept-2" },
                new IdempotencyKeyRow
                {
                    ProjectId = "alpha", Operation = "saved-queries", IdempotencyKey = "new", RequestHash = "h",
                    ResourceId = "kept-2", CreatedAtUnixNano = dayAgo
                }, 0, ct);

            using var service = CreateService(store, days: 30);
            var result = await service.RunCycleAsync(ct);

            Assert.AreEqual(1, result.DeletedAnnotations);
            Assert.AreEqual(1, result.DeletedIdempotencyKeys);
            Assert.IsNull(await store.GetAnnotationAsync("alpha", "stale", ct));
            Assert.IsNotNull(await store.GetAnnotationAsync("alpha", "edited", ct));
            Assert.HasCount(2, await store.ListSavedQueriesAsync("alpha", null, null, 10, ct));
            Assert.IsNull(await store.GetIdempotencyKeyAsync("alpha", "saved-queries", "old", 0, ct));
            Assert.IsNotNull(await store.GetIdempotencyKeyAsync("alpha", "saved-queries", "new", 0, ct));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static RetentionOptions BindRetention(IConfiguration configuration)
    {
        using var provider = new ServiceCollection().AddQylCollectorStorage(configuration).BuildServiceProvider();
        return provider.GetRequiredService<IOptions<RetentionOptions>>().Value;
    }

    private static RetentionService CreateService(DuckDbStore store, int days) =>
        new(
            store,
            Options.Create(new RetentionOptions { Days = days }),
            new FakeTimeProvider(s_now),
            NullLogger<RetentionService>.Instance);

    private static SpanStorageRow CreateSpan(
        string traceId,
        string spanId,
        ulong endTimeUnixNano,
        string? parentSpanId = null) =>
        new()
        {
            ProjectId = "default",
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            Name = spanId,
            Kind = 1,
            StartTimeUnixNano = endTimeUnixNano > 0 ? endTimeUnixNano - 1 : 0,
            EndTimeUnixNano = endTimeUnixNano,
            DurationNs = 1,
            StatusCode = 1
        };

    private static LogStorageRow CreateLog(
        string logId,
        ulong timeUnixNano,
        string? traceId = null,
        string? spanId = null,
        string? body = null) =>
        new()
        {
            ProjectId = "default",
            LogId = logId,
            TraceId = traceId,
            SpanId = spanId,
            TimeUnixNano = timeUnixNano,
            SeverityNumber = 9,
            Body = body
        };

    private static string DatabasePath(string testName) =>
        Path.Combine(Path.GetTempPath(), $"qyl-retention-{testName}-{Guid.NewGuid():N}.duckdb");

    private static void DeleteDatabase(string databasePath)
    {
        File.Delete(databasePath);
        File.Delete($"{databasePath}.wal");
    }

    private static long FileLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;
}
