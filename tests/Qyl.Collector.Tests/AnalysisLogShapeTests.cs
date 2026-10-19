using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// search_logs answers with compact records, and a grouped search answers with its patterns alone,
/// each naming the traces it was seen in.
/// </summary>
public sealed partial class AnalysisApiTests
{
    [TestMethod]
    public async Task Search_logs_returns_compact_records_and_patterns_alone_when_grouped()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.InsertLogsAsync([.. Enumerable.Range(1, 3).Select(i => new LogStorageRow
        {
            ProjectId = AlphaProjectId, LogId = "log-" + i, TraceId = TraceId(i), SpanId = SpanId(i), TimeUnixNano = time + (ulong)i * Second,
            SeverityNumber = 17, SeverityText = "ERROR", ServiceName = "worker", Body = $"tool timeout after {i} seconds", AttributesJson = """{"error.type":"timeout"}"""
        })], testContext.CancellationToken);

        var records = await Read(host, "search_logs", "{}");
        var newest = records.GetProperty("logs")[0];
        Assert.AreSequenceEqual(["time", "severity", "severity_number", "service", "body", "trace_id", "span_id", "attributes"], newest.EnumerateObject().Select(static p => p.Name));
        Assert.AreEqual((FixtureUtcNow.AddMinutes(-30).AddSeconds(3), "ERROR", 17, "worker", "tool timeout after 3 seconds"),
            (newest.GetProperty("time").GetDateTimeOffset(), newest.GetProperty("severity").GetString(), newest.GetProperty("severity_number").GetInt32(),
                newest.GetProperty("service").GetString(), newest.GetProperty("body").GetString()));

        var grouped = await Read(host, "search_logs", """{"group_by":"pattern"}""");
        Assert.IsFalse(grouped.TryGetProperty("logs", out _), "a grouped search answers with its patterns");
        var pattern = Assert.ContainsSingle(grouped.GetProperty("patterns").EnumerateArray());
        Assert.AreEqual(("tool timeout after # seconds", 3), (pattern.GetProperty("template").GetString(), pattern.GetProperty("count").GetInt32()));
        Assert.AreSequenceEqual([TraceId(1), TraceId(2), TraceId(3)], pattern.GetProperty("example_trace_ids").EnumerateArray().Select(static t => t.GetString()).Order(StringComparer.Ordinal));
    }
}
