using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// list_traces and get_trace answer with the fields an investigation reads: a trace row names its root,
/// duration, size, errors and failures; a span names its parent, service, timing and status, and each
/// service's resource appears once.
/// </summary>
public sealed partial class AnalysisApiTests
{
    [TestMethod]
    public async Task List_traces_returns_compact_rows_ten_by_default()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            .. Enumerable.Range(1, 12).SelectMany(i => new[]
            {
                Span(TraceId(i), SpanId(i), time + (ulong)i * Second, time + (ulong)i * Second + 2_000_000, sessionId: "s-" + i) with { Name = "request" },
                Span(TraceId(i), SpanId(100 + i), time + (ulong)i * Second + 10, time + (ulong)i * Second + 1_000_000, parentSpanId: SpanId(i), service: "orders-db", status: i == 12 ? (byte)2 : (byte)1)
                    with { Name = "db", StatusMessage = i == 12 ? "timeout" : null }
            })
        ]), testContext.CancellationToken);

        var result = await Read(host, "list_traces", "{}");

        var rows = result.GetProperty("traces").EnumerateArray().ToArray();
        Assert.HasCount(10, rows);
        Assert.IsTrue(result.GetProperty("has_more").GetBoolean());
        var newest = rows[0];
        Assert.AreSequenceEqual(["trace_id", "root", "start_time", "duration_ms", "span_count", "error_count", "services", "failure_ids", "session_id"],
            newest.EnumerateObject().Select(static p => p.Name));
        Assert.AreEqual(TraceId(12), newest.GetProperty("trace_id").GetString());
        Assert.AreEqual(("checkout", "request"), (newest.GetProperty("root").GetProperty("service").GetString(), newest.GetProperty("root").GetProperty("name").GetString()));
        Assert.AreEqual(2d, newest.GetProperty("duration_ms").GetDouble());
        Assert.AreEqual((2, 1), (newest.GetProperty("span_count").GetInt32(), newest.GetProperty("error_count").GetInt32()));
        Assert.AreSequenceEqual(["checkout", "orders-db"], newest.GetProperty("services").EnumerateArray().Select(static s => s.GetString()));
        Assert.ContainsSingle(newest.GetProperty("failure_ids").EnumerateArray());
        Assert.AreEqual("s-12", newest.GetProperty("session_id").GetString());
    }

    [TestMethod]
    public async Task Get_trace_returns_compact_spans_each_resource_once_and_its_own_range()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 2_000_000) with { Name = "request", ResourceJson = """{"service.name":"checkout","host.name":"a"}""" },
            Span(TraceId(1), SpanId(2), time + 10, time + 1_000_010, parentSpanId: SpanId(1), service: "orders-db", status: 2)
                with { Name = "db", StatusMessage = "timeout", AttributesJson = """{"db.system.name":"postgresql"}""", ResourceJson = """{"service.name":"orders-db"}""" },
            Span(TraceId(1), SpanId(3), time + 20, time + 30, parentSpanId: SpanId(1), service: "orders-db") with { Name = "db", ResourceJson = """{"service.name":"orders-db"}""" }
        ]), testContext.CancellationToken);

        var result = await Read(host, "get_trace", $$"""{"trace_id":"{{TraceId(1)}}"}""");

        Assert.IsFalse(result.TryGetProperty("span_roles", out _));
        Assert.AreEqual(J(time), result.GetProperty("window").GetProperty("start_time").GetDateTimeOffset());
        Assert.AreEqual(J(time + 2_000_000), result.GetProperty("window").GetProperty("end_time").GetDateTimeOffset());
        var trace = result.GetProperty("trace");
        var spans = trace.GetProperty("spans").EnumerateArray().ToDictionary(static s => s.GetProperty("span_id").GetString()!);
        var db = spans[SpanId(2)];
        Assert.AreSequenceEqual(["span_id", "parent_span_id", "service", "name", "start_time", "duration_ms", "status", "message", "attributes"], db.EnumerateObject().Select(static p => p.Name));
        Assert.AreEqual((SpanId(1), "orders-db", "error", "timeout"), (db.GetProperty("parent_span_id").GetString(), db.GetProperty("service").GetString(), db.GetProperty("status").GetString(), db.GetProperty("message").GetString()));
        Assert.AreEqual(1d, db.GetProperty("duration_ms").GetDouble());
        Assert.IsFalse(spans[SpanId(1)].TryGetProperty("parent_span_id", out _));
        var resources = trace.GetProperty("resources").EnumerateArray().Select(static r => r.GetProperty("service").GetString()).ToArray();
        Assert.AreSequenceEqual(["checkout", "orders-db"], resources);
        Assert.IsFalse(db.TryGetProperty("resource", out _));

        static DateTimeOffset J(ulong nanos) => DateTimeOffset.UnixEpoch.AddTicks((long)(nanos / 100));
    }
}
