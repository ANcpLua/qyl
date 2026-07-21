using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Collector.Health;
using Qyl.Collector.Primitives;
using Qyl.Collector.Retention;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class RetentionTests
{
    private static readonly DateTimeOffset s_now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
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
            ]), TestContext.Current.CancellationToken);
            await store.InsertLogsAsync(
            [
                CreateLog("expired-log", cutoff - 1, "expired", "expired-child"),
                CreateLog("fresh-log", cutoff, "fresh", "fresh-root")
            ], TestContext.Current.CancellationToken);

            using var service = CreateService(store, days: 30);
            var result = await service.RunCycleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, result.DeletedLogRows);
            Assert.Equal(2, result.DeletedSpanRows);
            Assert.Empty(await store.GetTraceAsync(
                "expired", "default", TestContext.Current.CancellationToken));

            var spans = await store.GetSpansAsync(
                "default", 100, TestContext.Current.CancellationToken);
            Assert.Equal(
                ["fresh-root", "mixed-fresh-child", "mixed-old-root"],
                spans.Select(static span => span.SpanId).Order(StringComparer.Ordinal));

            var spanKeys = spans
                .Select(static span => (span.TraceId, span.SpanId))
                .ToHashSet();
            Assert.DoesNotContain(
                spans,
                span => span.ParentSpanId is not null && !spanKeys.Contains((span.TraceId, span.ParentSpanId)));

            var logs = await store.GetLogsAsync(
                "default", limit: 100, ct: TestContext.Current.CancellationToken);
            var freshLog = Assert.Single(logs);
            Assert.Equal("fresh-log", freshLog.LogId);
            Assert.Contains(spans, span => span.TraceId == freshLog.TraceId);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
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
            ]), TestContext.Current.CancellationToken);
            var writesAfterSeed = Volatile.Read(ref writeCalls);

            using var service = CreateService(store, days: 0);
            var result = await service.RunCycleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(RetentionCycleOutcome.Empty, result);
            Assert.Equal(writesAfterSeed, Volatile.Read(ref writeCalls));
            Assert.Single(await store.GetTraceAsync(
                "expired", "default", TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Checkpointed_retention_reuses_space_without_exceeding_the_pre_deletion_peak()
    {
        var databasePath = DatabasePath("plateau");
        const int RowCount = 100_000;
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
                await seedStore.InsertLogsAsync(rows, TestContext.Current.CancellationToken);

            var preDeletionPeak = new FileInfo(databasePath).Length;
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            using var service = CreateService(store, days: 1);
            var cycleResult = await service.RunCycleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(RowCount, cycleResult.DeletedLogRows);
            Assert.True(cycleResult.FileSizeBeforeBytes >= preDeletionPeak);
            Assert.Equal(0, FileLength($"{databasePath}.wal"));

            rows.Clear();
            for (var i = 0; i < RowCount; i++)
                rows.Add(CreateLog($"new-{i}", currentTimestamp, body: bodies[i]));

            await store.InsertLogsAsync(rows, TestContext.Current.CancellationToken);
            await store.CheckpointAsync(TestContext.Current.CancellationToken);

            var steadyStateSize = new FileInfo(databasePath).Length;
            Assert.True(
                steadyStateSize <= cycleResult.FileSizeBeforeBytes,
                $"Expected reclaimed blocks to cap the file at {cycleResult.FileSizeBeforeBytes} bytes, " +
                $"but comparable replacement ingest grew it to {steadyStateSize} bytes.");
            Assert.Equal(0, FileLength($"{databasePath}.wal"));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
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
            await store.InsertLogsAsync(oldLogs, TestContext.Current.CancellationToken);

            Volatile.Write(ref observeWrites, 1);
            using var service = CreateService(store, days: 1);
            var cycle = service.RunCycleAsync(TestContext.Current.CancellationToken);
            await firstRetentionEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

            var ingest = store.EnqueueAsync(new SpanBatch(
            [
                CreateSpan(
                    "active-ingest",
                    "active-span",
                    TimeConversions.ToUnixNanoUnsigned(s_now))
            ]), TestContext.Current.CancellationToken).AsTask();
            releaseFirstRetention.TrySetResult();

            await ingest.WaitAsync(TestContext.Current.CancellationToken);
            await secondRetentionEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.False(cycle.IsCompleted);
            releaseSecondRetention.TrySetResult();

            var result = await cycle;
            Assert.Equal(2501, result.DeletedLogRows);
            Assert.True(result.WriteBatches > 1);
            Assert.Single(await store.GetTraceAsync(
                "active-ingest", "default", TestContext.Current.CancellationToken));
        }
        finally
        {
            releaseFirstRetention.TrySetResult();
            releaseSecondRetention.TrySetResult();
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Disk_pressure_health_is_degraded_and_carries_file_and_volume_numbers()
    {
        var databasePath = DatabasePath("health");

        try
        {
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            var check = new DuckDbHealthCheck(
                store,
                new RetentionOptions { StorageMinimumFreeBytes = long.MaxValue });

            var result = await check.CheckHealthAsync(
                new HealthCheckContext(),
                TestContext.Current.CancellationToken);

            Assert.Equal(HealthStatus.Degraded, result.Status);
            Assert.True(Assert.IsType<long>(result.Data["database_file_size_bytes"]) > 0);
            Assert.True(Assert.IsType<long>(result.Data["storage_free_bytes"]) >= 0);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public void Retention_configuration_binds_the_three_operator_variables()
    {
        var defaults = RetentionOptions.FromConfiguration(new ConfigurationBuilder().Build());
        Assert.Equal(30, defaults.Days);
        Assert.Equal(TimeSpan.FromMinutes(60), defaults.Interval);
        Assert.Equal(2048L * 1024L * 1024L, defaults.StorageMinimumFreeBytes);

        var configured = RetentionOptions.FromConfiguration(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["QYL_RETENTION_DAYS"] = "0",
                    ["QYL_RETENTION_INTERVAL_MINUTES"] = "15",
                    ["QYL_STORAGE_MIN_FREE_MB"] = "4096"
                })
                .Build());
        Assert.False(configured.IsEnabled);
        Assert.Equal(TimeSpan.FromMinutes(15), configured.Interval);
        Assert.Equal(4096L * 1024L * 1024L, configured.StorageMinimumFreeBytes);
    }

    private static RetentionService CreateService(DuckDbStore store, int days) =>
        new(
            store,
            new RetentionOptions { Days = days },
            new FixedTimeProvider(s_now),
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
