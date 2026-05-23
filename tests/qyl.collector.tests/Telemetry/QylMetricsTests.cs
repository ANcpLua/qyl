using Qyl.Collector.Telemetry;
using Qyl.Collector.Storage;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.Inventory;
using QylAttr;

namespace Qyl.Collector.Tests.Telemetry;

public sealed class QylMetricsTests
{
    [Fact]
    public void StorageSize_ObservableGauge_Reports_Registered_Callback_Value()
    {
        using var collector = new InProcessMetricCollector(static instrument =>
            instrument.Meter.Name == QylTelemetry.ServiceName &&
            instrument.Name == "qyl.storage.size");

        try
        {
            QylMetrics.RegisterStorageSizeCallback(static () => 12_345);

            collector.RecordObservableInstruments();

            var captured = Assert.Single(collector.Measurements);
            Assert.Equal(QylTelemetry.ServiceName, captured.MeterName);
            Assert.Equal("qyl.storage.size", captured.Name);
            Assert.Equal("By", captured.Unit);
            Assert.Equal("Approximate storage size in bytes", captured.Description);
            Assert.Equal(12_345d, captured.Value);
        }
        finally
        {
            QylMetrics.RegisterStorageSizeCallback(static () => 0);
        }
    }

    [Fact]
    public void StorageSize_ObservableGauge_Returns_Zero_When_Callback_Fails()
    {
        using var collector = new InProcessMetricCollector(static instrument =>
            instrument.Meter.Name == QylTelemetry.ServiceName &&
            instrument.Name == "qyl.storage.size");

        try
        {
            QylMetrics.RegisterStorageSizeCallback(static () => throw new InvalidOperationException("storage probe failed"));

            collector.RecordObservableInstruments();

            var captured = Assert.Single(collector.Measurements);
            Assert.Equal(QylTelemetry.ServiceName, captured.MeterName);
            Assert.Equal("qyl.storage.size", captured.Name);
            Assert.Equal(0d, captured.Value);
        }
        finally
        {
            QylMetrics.RegisterStorageSizeCallback(static () => 0);
        }
    }

    [Fact]
    public void StorageSize_ObservableGauge_Does_Not_Report_Negative_Bytes()
    {
        using var collector = new InProcessMetricCollector(static instrument =>
            instrument.Meter.Name == QylTelemetry.ServiceName &&
            instrument.Name == "qyl.storage.size");

        try
        {
            QylMetrics.RegisterStorageSizeCallback(static () => -1);

            collector.RecordObservableInstruments();

            var captured = Assert.Single(collector.Measurements);
            Assert.Equal(QylTelemetry.ServiceName, captured.MeterName);
            Assert.Equal("qyl.storage.size", captured.Name);
            Assert.Equal(0d, captured.Value);
        }
        finally
        {
            QylMetrics.RegisterStorageSizeCallback(static () => 0);
        }
    }

    [Fact]
    public void AgentInventory_ObservableGauge_Reports_Registered_Agent_Count()
    {
        using var collector = new InProcessMetricCollector(static instrument =>
            instrument.Meter.Name == ActivitySources.Agent &&
            instrument.Name == QylAgentInventory.InventorySizeMetricName);

        var inventory = new QylAgentInventory(TimeProvider.System);
        inventory.Register(new AgentRegistration(
            Key: "smoke-agent",
            Name: "SmokeAgent",
            Description: null,
            InstructionsHash: null,
            ProviderName: null,
            RegisteredAtUtc: default));

        collector.RecordObservableInstruments();

        Assert.True(
            collector.Measurements.Any(static captured =>
                captured.MeterName == ActivitySources.Agent &&
                captured.Name == QylAgentInventory.InventorySizeMetricName &&
                captured.Unit == "{agent}" &&
                captured.Description == "Count of agents registered in the qyl inventory" &&
                captured.Value == 1d),
            "Expected qyl.agent inventory gauge to report the registered agent count.");
    }

    [Fact]
    public async Task DuckDbStore_Records_Dropped_Write_Queue_Metrics_When_Full()
    {
        using var collector = new InProcessMetricCollector(static instrument =>
            instrument.Meter.Name == QylTelemetry.StorageMeterName &&
            instrument.Name is Duckdb.DroppedJobsTotal or Duckdb.DroppedSpansTotal);

        var ct = TestContext.Current.CancellationToken;
        var blockerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBlocker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var store = new DuckDbStore(":memory:", jobQueueCapacity: 1);
        var blockingWrite = store.ExecuteWriteAsync(async (_, token) =>
        {
            blockerEntered.TrySetResult();
            await releaseBlocker.Task.WaitAsync(token).ConfigureAwait(false);
        }, ct);

        try
        {
            await blockerEntered.Task.WaitAsync(ct);

            await store.EnqueueAsync(CreateSpanBatch("queued", 1), ct);
            await store.EnqueueAsync(CreateSpanBatch("dropped", 3), ct);

            var droppedJobs = collector.Measurements.Should().ContainSingle(static measurement =>
                measurement.Name == Duckdb.DroppedJobsTotal).Subject;
            droppedJobs.Value.Should().Be(1d);

            var droppedSpans = collector.Measurements.Should().ContainSingle(static measurement =>
                measurement.Name == Duckdb.DroppedSpansTotal).Subject;
            droppedSpans.Value.Should().Be(3d);
        }
        finally
        {
            releaseBlocker.TrySetResult();
            await blockingWrite.WaitAsync(ct);
        }
    }

    private static SpanBatch CreateSpanBatch(string prefix, int spanCount)
    {
        var spans = new List<SpanStorageRow>(spanCount);
        for (var i = 0; i < spanCount; i++)
            spans.Add(CreateSpan($"{prefix}-{i}"));

        return new SpanBatch(spans);
    }

    private static SpanStorageRow CreateSpan(string spanId) =>
        new()
        {
            SpanId = spanId,
            TraceId = $"trace-{spanId}",
            Name = "test span",
            Kind = 1,
            StartTimeUnixNano = 1,
            EndTimeUnixNano = 2,
            DurationNs = 1,
            StatusCode = 1,
            ServiceName = "metric-test"
        };
}
