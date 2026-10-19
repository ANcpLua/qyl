using System.Net;
using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed partial class AnalysisApiTests(TestContext testContext)
{
    private static readonly double[] s_counterValues = [100d, 50d, 190d];
    private const ulong Timestamp = FixtureUnixNano - 120_000_000_000;

    [TestMethod]
    public async Task Filters_run_before_paging_without_an_arbitrary_scan_limit()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        var rows = Enumerable.Range(1, 1200).Select(i => Span(TraceId(i), SpanId(i), time + (ulong)i, time + (ulong)i + 1000,
            service: i < 1198 ? "healthy" : "matching", status: i < 1198 ? (byte)1 : (byte)2)).ToArray();
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);
        var input = """{"service_name":"matching","errors_only":true,"limit":2} """;
        var first = await Read(host, "list_traces", input);
        Assert.AreEqual(2, first.GetProperty("traces").GetArrayLength());
        Assert.IsTrue(first.GetProperty("has_more").GetBoolean());
        var cursor = first.GetProperty("next_cursor").GetString();
        var next = await Read(host, "list_traces", $$"""{"service_name":"matching","errors_only":true,"limit":2,"cursor":"{{cursor}}"}""");
        Assert.ContainsSingle(next.GetProperty("traces").EnumerateArray());
        Assert.IsFalse(next.GetProperty("has_more").GetBoolean());
        var denied = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/list_traces", "beta-all", $$"""{"service_name":"matching","errors_only":true,"limit":2,"cursor":"{{cursor}}"}""");
        Assert.AreEqual(HttpStatusCode.BadRequest, denied.StatusCode);
    }

    [TestMethod]
    public async Task All_matcher_operators_and_resource_scope_use_the_same_engine()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000) with { AttributesJson = """{"count":{"type":"int","value":"4"},"phase":"build-fast","enabled":true}""", ResourceJson = """{"cloud.region":"eu"}""" },
            Span(TraceId(2), SpanId(2), time, time + 1000) with { AttributesJson = """{"count":{"type":"int","value":"1"},"phase":"test","enabled":false}""", ResourceJson = """{"cloud.region":"us"}""" }
        ]), testContext.CancellationToken);
        foreach (var matcher in new[] {
            """{"key":"count","op":"gt","value":2}""", """{"key":"count","op":"gte","value":4}""",
            """{"key":"count","op":"lt","value":2}""", """{"key":"count","op":"lte","value":1}""",
            """{"key":"phase","op":"prefix","value":"build"}""", """{"key":"phase","op":"contains","value":"fast"}""",
            """{"key":"enabled","op":"eq","value":true}""", """{"key":"enabled","op":"neq","value":false}""",
            """{"key":"cloud.region","scope":"resource","op":"in","values":["eu"]}""" })
        {
            var result = await Read(host, "list_traces", "{\"matchers\":[" + matcher + "]}");
            Assert.ContainsSingle(result.GetProperty("traces").EnumerateArray());
        }
    }

    [TestMethod]
    public async Task Trace_error_projection_is_applied_before_limit_and_strips_nested_attributes()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000, status: 1),
            Span(TraceId(1), SpanId(2), time + 10, time + 100, status: 2, parentSpanId: SpanId(1)) with { AttributesJson = """{"safe":"value"}""", ResourceJson = """{"service.name":"checkout"}""" }
        ]), testContext.CancellationToken);
        var result = await Read(host, "get_trace", $$"""{"trace_id":"{{TraceId(1)}}","selection":"errors","max_spans":1,"include_attributes":false}""");
        var trace = result.GetProperty("trace");
        Assert.AreEqual(2, trace.GetProperty("span_count").GetInt32());
        var span = Assert.ContainsSingle(trace.GetProperty("spans").EnumerateArray());
        Assert.AreEqual(SpanId(2), span.GetProperty("span_id").GetString());
        Assert.DoesNotContain("\"attributes\"", span.GetRawText(), StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow((byte)1, 340d)]
    [DataRow((byte)2, 240d)]
    public async Task Counter_increase_respects_temporality_and_start_time_resets(byte temporality, double expected)
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        var series = new MetricSeriesRow { ProjectId = "alpha", SeriesId = "counter", MetricName = "test.requests", MetricKind = 2, Temporality = temporality, IsMonotonic = 1, Unit = "{request}", FirstSeenUnixNano = time, LastSeenUnixNano = time + 2_000_000_000 };
        // The first cumulative point started before the window, so only the later points' changes are attributable to it.
        var points = s_counterValues.Select((value, index) => new MetricPointRow { ProjectId = "alpha", SeriesId = "counter", TimeUnixNano = time + (ulong)index * 1_000_000_000, StartTimeUnixNano = index == 2 ? time + 1_500_000_000 : time - 7_200_000_000_000, Value = value }).ToArray();
        await host.Store.InsertMetricsAsync([series], points.Select(point => MetricFixtures.Observation(series, point)).ToArray(), ct: testContext.CancellationToken);
        var data = await Read(host, "query_metric", """{"metric_name":"test.requests","function":"increase","step_ms":3600000}""");
        var increase = data.GetProperty("series").EnumerateArray().SelectMany(stream => stream.GetProperty("buckets").EnumerateArray()).Where(bucket => bucket.GetProperty("value").ValueKind != JsonValueKind.Null).Sum(bucket => bucket.GetProperty("value").GetDouble());
        Assert.AreEqual(expected, increase);
    }

    [TestMethod]
    public async Task Analysis_reads_include_notes_and_apply_updated_labels_before_paging()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000, sessionId: "annotated-session"),
            Span(TraceId(1), SpanId(2), time + 10, time + 100, parentSpanId: SpanId(1), sessionId: "annotated-session"),
            Span(TraceId(2), SpanId(3), time + 10000, time + 11000, sessionId: "other-session")
        ]), testContext.CancellationToken);
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            $$"""{"target":{"kind":"trace","trace_id":"{{TraceId(1)}}"},"text":"investigate","labels":["investigating"]}""");
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var note = await host.JsonAsync(created);
        using var child = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            $$"""{"target":{"kind":"span","trace_id":"{{TraceId(1)}}","span_id":"{{SpanId(2)}}"},"text":"child note","labels":["child"]}""");
        Assert.AreEqual(HttpStatusCode.Created, child.StatusCode);
        using var session = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            """{"target":{"kind":"session","session_id":"annotated-session"},"text":"session note"}""");
        Assert.AreEqual(HttpStatusCode.Created, session.StatusCode);
        var trace = await Read(host, "get_trace", $$"""{"trace_id":"{{TraceId(1)}}"}""");
        Assert.AreEqual(2, trace.GetProperty("annotations").GetArrayLength());
        var filtered = await Read(host, "list_traces", """{"annotation_label":"child","limit":1}""");
        var item = Assert.ContainsSingle(filtered.GetProperty("traces").EnumerateArray());
        Assert.AreEqual(TraceId(1), item.GetProperty("trace_id").GetString());
        var sessions = await Read(host, "list_sessions", """{"annotation_label":"absent"}""");
        Assert.IsEmpty(sessions.GetProperty("sessions").EnumerateArray());
        sessions = await Read(host, "list_sessions", """{}""");
        var annotated = sessions.GetProperty("sessions").EnumerateArray().Single(static s => s.GetProperty("session_id").GetString() == "annotated-session");
        Assert.ContainsSingle(annotated.GetProperty("annotations").EnumerateArray());
        Assert.IsFalse(sessions.TryGetProperty("annotations", out _), "each session carries its own notes");
        Assert.IsFalse(sessions.GetProperty("sessions").EnumerateArray().Single(static s => s.GetProperty("session_id").GetString() == "other-session")
            .TryGetProperty("annotations", out _), "a session without notes carries no empty list");
        using var updated = await host.SendAsync(HttpMethod.Patch, "/api/v1/annotations/" + note.GetProperty("id").GetString(), "writer",
            """{"text":"resolved note","labels":["resolved"]}""", new Dictionary<string, string> { ["If-Match"] = note.GetProperty("etag").GetString()! });
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        trace = await Read(host, "get_trace", $$"""{"trace_id":"{{TraceId(1)}}"}""");
        Assert.Contains(static annotation => annotation.GetProperty("text").GetString() == "resolved note",trace.GetProperty("annotations").EnumerateArray());
        Assert.IsEmpty((await Read(host, "list_traces", """{"annotation_label":"investigating"}""")).GetProperty("traces").EnumerateArray());
        Assert.ContainsSingle((await Read(host, "list_traces", """{"annotation_label":"resolved"}""")).GetProperty("traces").EnumerateArray());
    }

    /// <summary>
    /// An outbound call is an edge to the service or store its client span names: <c>service.peer.name</c> (ingest
    /// renames the deprecated <c>peer.service</c> to it), then <c>server.address</c>, then the database or messaging system.
    /// A call whose callee reports its own span counts once, through that span, and a named callee that reports spans
    /// stays a service node.
    /// </summary>
    [TestMethod]
    public async Task The_service_map_names_a_target_by_peer_service_then_server_address_and_counts_each_call_once()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000) with { Kind = 3, AttributesJson = """{"service.peer.name":"payments","server.address":"10.0.4.17"}""" },
            Span(TraceId(2), SpanId(2), time, time + 1000) with { Kind = 3, AttributesJson = """{"server.address":"ledger.internal"}""" },
            Span(TraceId(3), SpanId(3), time, time + 1000) with { Kind = 3, AttributesJson = """{"db.system.name":"postgresql"}""" },
            Span(TraceId(4), SpanId(4), time, time + 1000) with { Kind = 4, AttributesJson = """{"messaging.system":"kafka"}""" },
            Span(TraceId(5), SpanId(5), time, time + 1000) with { Kind = 3, AttributesJson = """{"service.peer.name":"inventory"}""" },
            Span(TraceId(5), SpanId(6), time + 10, time + 900, parentSpanId: SpanId(5), service: "inventory") with { Kind = 2 },
            Span(TraceId(7), SpanId(7), time + 2000, time + 3000) with { Kind = 3, AttributesJson = """{"service.peer.name":"inventory"}""" }
        ]), testContext.CancellationToken);

        var map = await Read(host, "get_service_map", "{}");

        var edges = map.GetProperty("edges").EnumerateArray()
            .Select(static edge => (edge.GetProperty("from").GetString(), edge.GetProperty("to").GetString()))
            .ToArray();
        (string?, string?)[] expected =
        [
            ("checkout", "inventory"), ("checkout", "kafka"), ("checkout", "ledger.internal"), ("checkout", "payments"),
            ("checkout", "postgresql")
        ];
        Assert.AreSequenceEqual(expected, edges.Order());
        var inventory = map.GetProperty("edges").EnumerateArray().Single(static edge => edge.GetProperty("to").GetString() == "inventory");
        Assert.AreEqual(2, inventory.GetProperty("calls").GetInt32());
        Assert.AreEqual("service", map.GetProperty("nodes").EnumerateArray()
            .Single(static node => node.GetProperty("name").GetString() == "inventory").GetProperty("kind").GetString());
    }

    private static async Task<JsonElement> Read(CollectorApiTestHost host, string operation, string input)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + operation, "reader", input);
        var json = await host.JsonAsync(response);
        Assert.IsTrue(response.IsSuccessStatusCode, json.GetRawText());
        return json;
    }
}
