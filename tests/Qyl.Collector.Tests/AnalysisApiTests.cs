using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

public sealed partial class AnalysisApiTests
{
    private static readonly double[] CounterValues = [100d, 50d, 190d];
    private static ulong Timestamp => (ulong)DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds() * 1_000_000;

    [Fact]
    public async Task Filters_run_before_paging_without_an_arbitrary_scan_limit()
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        var time = Timestamp;
        var rows = Enumerable.Range(1, 1200).Select(i => Span(TraceId(i), SpanId(i), time + (ulong)i, time + (ulong)i + 1000,
            service: i < 1198 ? "healthy" : "matching", status: i < 1198 ? (byte)1 : (byte)2)).ToArray();
        await host.Store.EnqueueAsync(new SpanBatch(rows), TestContext.Current.CancellationToken);
        var input = """{"service_name":"matching","errors_only":true,"limit":2} """;
        var first = await Read(host, "list_traces", input);
        Assert.Equal(2, first.GetProperty("traces").GetArrayLength());
        Assert.True(first.GetProperty("has_more").GetBoolean());
        var cursor = first.GetProperty("next_cursor").GetString();
        var next = await Read(host, "list_traces", $$"""{"service_name":"matching","errors_only":true,"limit":2,"cursor":"{{cursor}}"}""");
        Assert.Single(next.GetProperty("traces").EnumerateArray());
        Assert.False(next.GetProperty("has_more").GetBoolean());
        var denied = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/list_traces", "beta-all", $$"""{"service_name":"matching","errors_only":true,"limit":2,"cursor":"{{cursor}}"}""");
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
    }

    [Fact]
    public async Task All_matcher_operators_and_resource_scope_use_the_same_engine()
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        var time = Timestamp;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000) with { AttributesJson = """{"count":{"type":"int","value":"4"},"phase":"build-fast","enabled":true}""", ResourceJson = """{"cloud.region":"eu"}""" },
            Span(TraceId(2), SpanId(2), time, time + 1000) with { AttributesJson = """{"count":{"type":"int","value":"1"},"phase":"test","enabled":false}""", ResourceJson = """{"cloud.region":"us"}""" }
        ]), TestContext.Current.CancellationToken);
        foreach (var matcher in new[] {
            """{"key":"count","op":"gt","value":2}""", """{"key":"count","op":"gte","value":4}""",
            """{"key":"count","op":"lt","value":2}""", """{"key":"count","op":"lte","value":1}""",
            """{"key":"phase","op":"prefix","value":"build"}""", """{"key":"phase","op":"contains","value":"fast"}""",
            """{"key":"enabled","op":"eq","value":true}""", """{"key":"enabled","op":"neq","value":false}""",
            """{"key":"cloud.region","scope":"resource","op":"in","values":["eu"]}""" })
        {
            var result = await Read(host, "list_traces", "{\"matchers\":[" + matcher + "]}");
            Assert.Single(result.GetProperty("traces").EnumerateArray());
        }
    }

    [Fact]
    public async Task Trace_error_projection_is_applied_before_limit_and_strips_nested_attributes()
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        var time = Timestamp;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000, status: 1),
            Span(TraceId(1), SpanId(2), time + 10, time + 100, status: 2, parentSpanId: SpanId(1)) with { AttributesJson = """{"safe":"value"}""", ResourceJson = """{"service.name":"checkout"}""" }
        ]), TestContext.Current.CancellationToken);
        var result = await Read(host, "get_trace", $$"""{"trace_id":"{{TraceId(1)}}","errors_only":true,"max_spans":1,"include_attributes":false}""");
        var trace = result.GetProperty("trace");
        Assert.Equal(2, trace.GetProperty("span_count").GetInt32());
        var span = Assert.Single(trace.GetProperty("spans").EnumerateArray());
        Assert.Equal(SpanId(2), span.GetProperty("span_id").GetString());
        Assert.DoesNotContain("\"attributes\"", span.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((byte)1, 340d)]
    [InlineData((byte)2, 240d)]
    public async Task Counter_increase_respects_temporality_and_start_time_resets(byte temporality, double expected)
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        var time = Timestamp;
        var series = new MetricSeriesRow { ProjectId = "alpha", SeriesId = "counter", MetricName = "test.requests", MetricKind = 2, Temporality = temporality, IsMonotonic = 1, Unit = "{request}", FirstSeenUnixNano = time, LastSeenUnixNano = time + 2_000_000_000 };
        var points = CounterValues.Select((value, index) => new MetricPointRow { ProjectId = "alpha", SeriesId = "counter", TimeUnixNano = time + (ulong)index * 1_000_000_000, StartTimeUnixNano = index == 2 ? time + 1_500_000_000 : time - 1_000_000_000, Value = value }).ToArray();
        await host.Store.InsertMetricsAsync([series], points, ct: TestContext.Current.CancellationToken);
        var data = await Read(host, "query_metric", """{"metric_name":"test.requests","function":"increase","step_ms":3600000}""");
        var increase = data.GetProperty("series").EnumerateArray().SelectMany(stream => stream.GetProperty("buckets").EnumerateArray()).Where(bucket => bucket.GetProperty("value").ValueKind != JsonValueKind.Null).Sum(bucket => bucket.GetProperty("value").GetDouble());
        Assert.Equal(expected, increase);
    }

    [Fact]
    public async Task Analysis_reads_include_notes_and_apply_updated_labels_before_paging()
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);
        var time = Timestamp;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000, sessionId: "annotated-session"),
            Span(TraceId(1), SpanId(2), time + 10, time + 100, parentSpanId: SpanId(1), sessionId: "annotated-session"),
            Span(TraceId(2), SpanId(3), time + 10000, time + 11000, sessionId: "other-session")
        ]), TestContext.Current.CancellationToken);
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            $$"""{"target":{"kind":"trace","trace_id":"{{TraceId(1)}}"},"text":"investigate","labels":["investigating"]}""");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var note = await JsonAsync(created);
        using var child = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            $$"""{"target":{"kind":"span","trace_id":"{{TraceId(1)}}","span_id":"{{SpanId(2)}}"},"text":"child note","labels":["child"]}""");
        Assert.Equal(HttpStatusCode.Created, child.StatusCode);
        using var session = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            """{"target":{"kind":"session","session_id":"annotated-session"},"text":"session note"}""");
        Assert.Equal(HttpStatusCode.Created, session.StatusCode);
        var trace = await Read(host, "get_trace", $$"""{"trace_id":"{{TraceId(1)}}"}""");
        Assert.Equal(2, trace.GetProperty("annotations").GetArrayLength());
        var filtered = await Read(host, "list_traces", """{"annotation_label":"child","limit":1}""");
        var item = Assert.Single(filtered.GetProperty("traces").EnumerateArray());
        Assert.Equal(TraceId(1), item.GetProperty("trace_id").GetString());
        Assert.Contains(item.GetProperty("annotation_labels").EnumerateArray(), static label => label.GetString() == "child");
        var sessions = await Read(host, "list_sessions", """{"annotation_label":"absent"}""");
        Assert.Empty(sessions.GetProperty("annotations").EnumerateArray());
        sessions = await Read(host, "list_sessions", """{}""");
        Assert.Single(sessions.GetProperty("annotations").EnumerateArray());
        using var updated = await host.SendAsync(HttpMethod.Patch, "/api/v1/annotations/" + note.GetProperty("id").GetString(), "writer",
            """{"text":"resolved note","labels":["resolved"]}""", new Dictionary<string, string> { ["If-Match"] = note.GetProperty("etag").GetString()! });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        trace = await Read(host, "get_trace", $$"""{"trace_id":"{{TraceId(1)}}"}""");
        Assert.Contains(trace.GetProperty("annotations").EnumerateArray(), static annotation => annotation.GetProperty("text").GetString() == "resolved note");
        Assert.Empty((await Read(host, "list_traces", """{"annotation_label":"investigating"}""")).GetProperty("traces").EnumerateArray());
        Assert.Single((await Read(host, "list_traces", """{"annotation_label":"resolved"}""")).GetProperty("traces").EnumerateArray());
    }

    private static async Task<JsonElement> Read(CollectorApiTestHost host, string operation, string input)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + operation, "reader", input);
        var json = await JsonAsync(response);
        Assert.True(response.IsSuccessStatusCode, json.GetRawText());
        return json;
    }
}
