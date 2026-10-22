using System.Net;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Qyl.Collector.Tests;

/// <summary>
/// Contract 11.6.0 read side (SCH-9, QYL-A3, QYL-A4, QYL-A5): every paged list takes a cursor,
/// pages by keyset with one look-ahead row, and the trace list filters server-side.
/// </summary>
[TestClass]
public sealed class ReadPagingTests(TestContext testContext)
{
    [TestMethod]
    [DataRow("service_name=billing", new[] { 3 })]
    [DataRow("errors_only=true", new[] { 4, 2 })]
    [DataRow("errors_only=false", new[] { 4, 3, 2, 1 })]
    // Bounds are on the trace start: inclusive start, exclusive end.
    [DataRow("start_time=1970-01-01T00:00:02Z", new[] { 4, 3, 2 })]
    [DataRow("end_time=1970-01-01T00:00:03Z", new[] { 2, 1 })]
    [DataRow("start_time=1970-01-01T00:00:02Z&end_time=1970-01-01T00:00:04Z&errors_only=true", new[] { 2 })]
    public async Task Trace_list_filters_whole_traces_server_side(string query, int[] expected)
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await SeedTracesAsync(host);

        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/traces?{query}", "reader");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var items = (await host.JsonAsync(response)).GetProperty("items").EnumerateArray().ToList();
        Assert.AreSequenceEqual(expected.Select(TraceId), items.Select(static item => item.GetProperty("trace_id").GetString()));
        // A filter matching one span still returns the whole trace.
        items.ToList().ForEach(static item =>
            Assert.AreEqual(item.GetProperty("span_count").GetInt32(), item.GetProperty("spans").GetArrayLength()));
    }

    [TestMethod]
    public async Task Trace_list_rejects_an_inverted_range_and_a_foreign_cursor()
    {
        await using var host = await StartAsync(testContext.CancellationToken);

        using var inverted = await host.SendAsync(HttpMethod.Get,
            "/api/v1/traces?start_time=1970-01-01T00:00:05Z&end_time=1970-01-01T00:00:01Z", "reader");
        using var badBool = await host.SendAsync(HttpMethod.Get, "/api/v1/traces?errors_only=yes", "reader");
        using var foreign = await host.SendAsync(HttpMethod.Get,
            "/api/v1/traces?cursor=" + Uri.EscapeDataString(SessionsCursor()), "reader");

        Assert.AreEqual(HttpStatusCode.BadRequest, inverted.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, badBool.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    [TestMethod]
    public async Task Trace_spans_page_in_start_order_with_the_shared_page_limit()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await SeedTracesAsync(host);

        // Omitted, the limit is the contract's default page of 100: three spans fit in one page.
        using var all = await host.SendAsync(HttpMethod.Get, $"/api/v1/traces/{TraceId(1)}/spans", "reader");
        var allBody = await host.JsonAsync(all);
        Assert.AreEqual(3, allBody.GetProperty("items").GetArrayLength());
        Assert.IsFalse(allBody.GetProperty("has_more").GetBoolean());
        Assert.IsFalse(allBody.TryGetProperty("next_cursor", out _));

        var spans = await CollectAsync(host, $"/api/v1/traces/{TraceId(1)}/spans?limit=1", "span_id");
        Assert.AreSequenceEqual([SpanId(10), SpanId(11), SpanId(12)], spans);

        using var tooLarge = await host.SendAsync(HttpMethod.Get, $"/api/v1/traces/{TraceId(1)}/spans?limit=1001", "reader");
        using var missing = await host.SendAsync(HttpMethod.Get, $"/api/v1/traces/{TraceId(99)}/spans?limit=1", "reader");
        Assert.AreEqual(HttpStatusCode.BadRequest, tooLarge.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [TestMethod]
    public async Task Session_traces_are_bounded_and_paged()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), 1_000, 2_000, sessionId: "sess-1"),
            Span(TraceId(2), SpanId(2), 3_000, 4_000, sessionId: "sess-1"),
            // Only the child carries the session; the session still returns the whole trace.
            Span(TraceId(3), SpanId(3), 5_000, 9_000),
            Span(TraceId(3), SpanId(4), 6_000, 7_000, parentSpanId: SpanId(3), sessionId: "sess-1"),
            Span(TraceId(4), SpanId(5), 5_000, 6_000, sessionId: "sess-2")
        ]), testContext.CancellationToken);

        using var first = await host.SendAsync(HttpMethod.Get, "/api/v1/sessions/sess-1/traces?limit=2", "reader");
        var firstBody = await host.JsonAsync(first);
        Assert.AreSequenceEqual([TraceId(3), TraceId(2)],
            firstBody.GetProperty("items").EnumerateArray().Select(static item => item.GetProperty("trace_id").GetString()));
        Assert.AreEqual(2, firstBody.GetProperty("items")[0].GetProperty("span_count").GetInt32());
        Assert.IsTrue(firstBody.GetProperty("has_more").GetBoolean());

        var cursor = firstBody.GetProperty("next_cursor").GetString()!;
        using var second = await host.SendAsync(HttpMethod.Get,
            "/api/v1/sessions/sess-1/traces?limit=2&cursor=" + Uri.EscapeDataString(cursor), "reader");
        var secondBody = await host.JsonAsync(second);
        Assert.AreEqual(TraceId(1), Assert.ContainsSingle(secondBody.GetProperty("items").EnumerateArray())
            .GetProperty("trace_id").GetString());
        Assert.IsFalse(secondBody.GetProperty("has_more").GetBoolean());

        // A session-traces cursor is not a trace-list cursor.
        using var foreign = await host.SendAsync(HttpMethod.Get,
            "/api/v1/traces?cursor=" + Uri.EscapeDataString(cursor), "reader");
        using var missing = await host.SendAsync(HttpMethod.Get, "/api/v1/sessions/none/traces", "reader");
        using var tooLarge = await host.SendAsync(HttpMethod.Get, "/api/v1/sessions/sess-1/traces?limit=1001", "reader");
        Assert.AreEqual(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, tooLarge.StatusCode);
    }

    [TestMethod]
    public async Task Sessions_page_by_keyset_without_an_offset()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), 1_000, 2_000, sessionId: "a"),
            // Two sessions end at the same instant; the session id breaks the tie.
            Span(TraceId(2), SpanId(2), 1_000, 3_000, sessionId: "b"),
            Span(TraceId(3), SpanId(3), 1_000, 3_000, sessionId: "c")
        ]), testContext.CancellationToken);

        Assert.AreSequenceEqual(["c", "b", "a"], await CollectAsync(host, "/api/v1/sessions?limit=1", "session_id"));

        using var offset = await host.SendAsync(HttpMethod.Get, "/api/v1/sessions?cursor=1", "reader");
        Assert.AreEqual(HttpStatusCode.BadRequest, offset.StatusCode);
    }

    [TestMethod]
    public async Task Sessions_keep_is_active_and_the_time_range_while_paging()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        const ulong now = FixtureUnixNano;
        const ulong Second = 1_000_000_000UL;
        await host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), 1 * Second, 2 * Second, sessionId: "ended-early"),
            Span(TraceId(2), SpanId(2), 5 * Second, 6 * Second, sessionId: "ended-late"),
            Span(TraceId(3), SpanId(3), 6 * Second, 7 * Second, sessionId: "ended-later"),
            Span(TraceId(4), SpanId(4), now - Second, now, sessionId: "live")
        ]), testContext.CancellationToken);

        Assert.AreSequenceEqual(["live"], await CollectAsync(host, "/api/v1/sessions?is_active=true&limit=1", "session_id"));
        Assert.AreSequenceEqual(["ended-later", "ended-late", "ended-early"],
            await CollectAsync(host, "/api/v1/sessions?is_active=false&limit=1", "session_id"));
        Assert.AreSequenceEqual(["ended-later", "ended-late"], await CollectAsync(host,
            "/api/v1/sessions?is_active=false&start_time=1970-01-01T00:00:04Z&end_time=1970-01-01T00:00:10Z&limit=1",
            "session_id"));
    }

    [TestMethod]
    public async Task SessionList_TimeRangeCutsSession_KeepsWholeTotals()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        const ulong Second = 1_000_000_000UL;
        await host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), 1 * Second, 2 * Second, sessionId: "spanning"),
            Span(TraceId(2), SpanId(2), 5 * Second, 6 * Second, sessionId: "spanning")
        ]), testContext.CancellationToken);

        using var whole = await host.SendAsync(HttpMethod.Get, "/api/v1/sessions/spanning", "reader");
        var session = await host.JsonAsync(whole);
        using var listed = await host.SendAsync(HttpMethod.Get, "/api/v1/sessions?start_time=1970-01-01T00:00:04Z", "reader");
        var item = Assert.ContainsSingle((await host.JsonAsync(listed)).GetProperty("items").EnumerateArray());

        Assert.AreEqual(2, session.GetProperty("span_count").GetInt32());
        Assert.AreEqual(session.GetProperty("span_count").GetInt32(), item.GetProperty("span_count").GetInt32());
        Assert.AreEqual(session.GetProperty("trace_count").GetInt32(), item.GetProperty("trace_count").GetInt32());
        Assert.AreEqual(session.GetProperty("start_time").GetString(), item.GetProperty("start_time").GetString());
    }

    [TestMethod]
    public async Task Logs_page_by_time_and_id_with_a_next_cursor()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var log = new LogStorageRow
        {
            ProjectId = "alpha", LogId = "a", TimeUnixNano = 1_000, SeverityNumber = 9, Body = "\"x\""
        };
        await host.Store.InsertLogsAsync(
        [
            log with { Body = "\"a\"" },
            // Equal timestamps page by log id.
            log with { LogId = "b", TimeUnixNano = 2_000, Body = "\"b\"" },
            log with { LogId = "c", TimeUnixNano = 2_000, Body = "\"c\"" },
            log with { LogId = "d", TimeUnixNano = 3_000, SessionId = "sess-1", Body = "\"d\"" }
        ], testContext.CancellationToken);

        var ids = await CollectAsync(host, "/api/v1/logs?limit=1", "body");
        Assert.AreSequenceEqual(["d", "c", "b", "a"], ids);

        using var bySession = await host.SendAsync(HttpMethod.Get, "/api/v1/logs?session_id=sess-1", "reader");
        Assert.ContainsSingle((await host.JsonAsync(bySession)).GetProperty("items").EnumerateArray());
    }

    [TestMethod]
    public async Task Metric_catalog_and_series_page_with_cursors()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var gauge = new Gauge();
        foreach (var (route, time) in new[] { ("/a", 1UL), ("/b", 2UL), ("/c", 3UL) })
        {
            gauge.DataPoints.Add(new NumberDataPoint
            {
                TimeUnixNano = time * 1_000_000_000UL,
                AsDouble = 1,
                Attributes = { new KeyValue { Key = "http.route", Value = new AnyValue { StringValue = route } } }
            });
        }

        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    Resource = new Resource(),
                    ScopeMetrics =
                    {
                        new ScopeMetrics
                        {
                            Metrics =
                            {
                                new OtlpMetric { Name = "m.one", Gauge = gauge },
                                new OtlpMetric { Name = "m.three", Gauge = gauge.Clone() },
                                new OtlpMetric { Name = "m.two", Gauge = gauge.Clone() }
                            }
                        }
                    }
                }
            }
        };
        var write = MetricStorageMapper.ToStorageRows(OtlpConverter.ConvertMetrics(request), "alpha");
        await host.Store.InsertMetricsAsync(write.Series, write.Observations, testContext.CancellationToken);

        Assert.AreSequenceEqual(["m.one", "m.three", "m.two"], await CollectAsync(host, "/api/v1/metrics?limit=1", "name"));

        var series = await CollectAsync(host, "/api/v1/metrics/m.one/series?limit=1", "series_id");
        Assert.AreEqual(3, series.Distinct(StringComparer.Ordinal).Count());
    }

    // Follows next_cursor to the end, asserting has_more agrees with it on every page.
    private static async Task<List<string>> CollectAsync(CollectorApiTestHost host, string path, string property)
    {
        var values = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            using var response = await host.SendAsync(HttpMethod.Get,
                path + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), "reader");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var body = await host.JsonAsync(response);
            values.AddRange(body.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty(property).GetString()!));
            cursor = body.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
            Assert.AreEqual(cursor is not null, body.GetProperty("has_more").GetBoolean());
            Assert.IsFalse(body.TryGetProperty("prev_cursor", out _));
            Assert.IsTrue(++pages < 20, "Paging did not terminate.");
        } while (cursor is not null);

        return values;
    }

    private async Task SeedTracesAsync(CollectorApiTestHost host)
    {
        const ulong Second = 1_000_000_000UL;
        await host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(10), 1 * Second, 2 * Second),
            Span(TraceId(1), SpanId(11), 1 * Second + 1, 2 * Second, parentSpanId: SpanId(10)),
            Span(TraceId(1), SpanId(12), 1 * Second + 2, 2 * Second, parentSpanId: SpanId(10)),
            Span(TraceId(2), SpanId(20), 2 * Second, 3 * Second),
            Span(TraceId(2), SpanId(21), 2 * Second + 1, 3 * Second, parentSpanId: SpanId(20), status: 2),
            Span(TraceId(3), SpanId(30), 3 * Second, 4 * Second, service: "billing"),
            Span(TraceId(4), SpanId(40), 4 * Second, 5 * Second, status: 2),
            Span(TraceId(5), SpanId(50), 1 * Second, 9 * Second, project: "beta", status: 2)
        ]), testContext.CancellationToken);
    }

    private static string SessionsCursor() =>
        Hosting.PageCursorCodec.Encode(Hosting.PageCursorKind.Sessions, "0000000000000001", "a");
}
