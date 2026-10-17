using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Qyl.Collector.Ingestion.Primitives;
using Qyl.Collector.Retention;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class RetentionSchedulingTests(TestContext testContext) : IAsyncDisposable
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private FakeTimeProvider _fake = null!;
    private TimerObservingTimeProvider _clock = null!;
    private DuckDbStore? _store;
    private RetentionService? _service;

    private DuckDbStore Store => _store ?? throw new InvalidOperationException("The test store has not started.");

    [TestInitialize]
    public void CreateFixture()
    {
        _fake = new FakeTimeProvider(s_now);
        _clock = new TimerObservingTimeProvider(_fake);
        _store = new DuckDbStore(":memory:");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_service is not null)
            {
                using var service = _service;
                await service.StopAsync(CancellationToken.None);
                if (service.ExecuteTask is { } execution)
                    await execution;
            }
        }
        finally
        {
            if (_store is not null)
                await _store.DisposeAsync();
        }
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Retention_waits_ten_seconds_then_repeats_hourly_using_the_advanced_cutoff()
    {
        await Store.InsertLogsAsync(
        [
            Log("expired", s_now.AddDays(-2)),
            Log("expires-next-cycle", s_now.AddHours(-23.5))
        ], testContext.CancellationToken);
        await StartServiceAsync(days: 1);
        Assert.AreEqual(TimeSpan.FromSeconds(10), await _clock.WaitForTimerAsync(testContext.CancellationToken));

        _fake.Advance(TimeSpan.FromSeconds(9));
        Assert.AreEqual(2, (await Store.GetLogsAsync(AlphaProjectId, ct: testContext.CancellationToken)).Count);

        _fake.Advance(TimeSpan.FromSeconds(1));
        // Registering the next wait also proves that deletion and checkpointing have completed.
        Assert.AreEqual(TimeSpan.FromHours(1), await _clock.WaitForTimerAsync(testContext.CancellationToken));
        var remaining = Assert.ContainsSingle(await Store.GetLogsAsync(AlphaProjectId, ct: testContext.CancellationToken));
        Assert.AreEqual("expires-next-cycle", remaining.LogId);

        _fake.Advance(TimeSpan.FromSeconds(3599));
        Assert.ContainsSingle(await Store.GetLogsAsync(AlphaProjectId, ct: testContext.CancellationToken));

        _fake.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(TimeSpan.FromHours(1), await _clock.WaitForTimerAsync(testContext.CancellationToken));
        Assert.IsEmpty(await Store.GetLogsAsync(AlphaProjectId, ct: testContext.CancellationToken));
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Stopping_retention_joins_the_worker_during_its_initial_or_recurring_wait(bool afterFirstCycle)
    {
        await Store.InsertLogsAsync([Log("expired", s_now.AddDays(-2))], testContext.CancellationToken);
        await StartServiceAsync(days: 1);
        await _clock.WaitForTimerAsync(testContext.CancellationToken);
        if (afterFirstCycle)
        {
            _fake.Advance(TimeSpan.FromSeconds(10));
            await _clock.WaitForTimerAsync(testContext.CancellationToken);
        }

        Assert.IsNotNull(_service);
        await _service.StopAsync(testContext.CancellationToken);
        var execution = _service.ExecuteTask;
        Assert.IsNotNull(execution);
        await execution.WaitAsync(testContext.CancellationToken);
        await Store.InsertLogsAsync([Log("after-stop", s_now.AddDays(-2))], testContext.CancellationToken);

        _fake.Advance(TimeSpan.FromDays(2));

        string[] expected = afterFirstCycle ? ["after-stop"] : ["after-stop", "expired"];
        Assert.AreSequenceEqual(expected, (await Store.GetLogsAsync(AlphaProjectId, ct: testContext.CancellationToken))
            .Select(static row => row.LogId).Order(StringComparer.Ordinal));
        Assert.IsFalse(_clock.TryReadTimer(out _));
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Disabled_retention_finishes_without_scheduling_a_timer_or_deleting_data()
    {
        await Store.InsertLogsAsync([Log("expired", s_now.AddDays(-2))], testContext.CancellationToken);
        await StartServiceAsync(days: 0);
        Assert.IsNotNull(_service);
        var execution = _service.ExecuteTask;
        Assert.IsNotNull(execution);
        await execution.WaitAsync(testContext.CancellationToken);

        _fake.Advance(TimeSpan.FromDays(2));

        Assert.IsFalse(_clock.TryReadTimer(out _));
        Assert.AreEqual("expired", Assert.ContainsSingle(
            await Store.GetLogsAsync(AlphaProjectId, ct: testContext.CancellationToken)).LogId);
    }

    private Task StartServiceAsync(int days)
    {
        _service = new RetentionService(Store, new RetentionOptions { Days = days }, _clock,
            NullLogger<RetentionService>.Instance);
        return _service.StartAsync(testContext.CancellationToken);
    }

    private static LogStorageRow Log(string id, DateTimeOffset timestamp) => new()
    {
        ProjectId = AlphaProjectId,
        LogId = id,
        TimeUnixNano = TimeConversions.ToUnixNanoUnsigned(timestamp),
        SeverityNumber = 9,
        Body = "\"retention scheduling\""
    };
}
