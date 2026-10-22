using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

public sealed partial class AnalysisApiTests
{
    [TestMethod]
    public async Task GetTrace_EndedHalfAMillisecondShortOfFiveSeconds_IsNotSettled()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var end = FixtureUnixNano - 4_999_500_000;
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(1), SpanId(1), end - 1_000_000, end)]), testContext.CancellationToken);

        var trace = await Read(host, "get_trace", $$"""{"trace_id":"{{TraceId(1)}}"}""");

        Assert.IsFalse(trace.GetProperty("settled").GetBoolean());
    }

    [TestMethod]
    public async Task GetAgentRun_SubMillisecondStart_ReportsItToTheTick()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var start = FixtureUnixNano - 30 * Minute + 123_456;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), start, start + 1_000_000, sessionId: "run") with { Name = "chat", GenAiOperationName = "chat" }
        ]), testContext.CancellationToken);

        var run = await Read(host, "get_agent_run", """{"session_id":"run"}""");

        Assert.AreEqual("2026-10-17T11:30:00.0001234+00:00", run.GetProperty("run").GetProperty("started").GetString());
    }
}
