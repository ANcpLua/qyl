using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qyl.Collector.Analysis;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

public sealed partial class AnalysisApiTests
{
    [TestMethod]
    public async Task Targeted_trace_reads_report_foreign_and_absent_traces_as_not_found()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(1), SpanId(1), Timestamp, Timestamp + 1000)]), testContext.CancellationToken);
        foreach (var (key, traceId) in new[] { ("beta-all", TraceId(1)), ("reader", TraceId(99)) })
        {
            var input = new JsonObject { ["trace_id"] = traceId };
            using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/get_trace", key, input.ToJsonString());
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.AreEqual("not_found", (await host.JsonAsync(response)).GetProperty("code").GetString());
        }
    }

    [TestMethod]
    public async Task Trace_comparison_loads_both_traces_and_returns_a_complete_nested_contract()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000, status: 2) with { Name = "request" },
            Span(TraceId(2), SpanId(2), time, time + 1000, status: 1) with { Name = "request" }
        ]), testContext.CancellationToken);
        var comparison = await Read(host, "compare_traces", $$$"""{"a":{"trace_id":"{{{TraceId(1)}}}"},"b":{"trace_id":"{{{TraceId(2)}}}"}}""");
        Assert.IsTrue(comparison.GetProperty("comparable").GetBoolean());
        Assert.IsFalse(comparison.GetProperty("has_more").GetBoolean());
        var pair = Assert.ContainsSingle(comparison.GetProperty("aligned").EnumerateArray());
        Assert.AreEqual((SpanId(1), SpanId(2)), (pair.GetProperty("a_span_id").GetString(), pair.GetProperty("b_span_id").GetString()));
        Assert.AreEqual("status", Assert.ContainsSingle(pair.GetProperty("differences").EnumerateArray()).GetProperty("kind").GetString());
        using var missing = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/compare_traces", "reader", $$$"""{"a":{"trace_id":"{{{TraceId(1)}}}"},"b":{"trace_id":"{{{TraceId(3)}}}"}}""");
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
    }

    /// <summary>
    /// The failure triple as ingest keeps it: the type from the exception event, the message from the status
    /// description (<c>RecordFailure</c>'s carrier; <c>exception.message</c> never survives ingest).
    /// </summary>
    [TestMethod]
    public async Task Failure_identity_ignores_UUIDs_and_reads_exception_events()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        var rows = Enumerable.Range(1, 100).Select(i => Span(TraceId(i), SpanId(i), time, time + 1000, status: 2) with
        {
            Name = "call",
            StatusMessage = $"request {Guid.NewGuid()} timed out",
            EventsJson = """[{"name":"exception","attributes":{"exception.type":"TimeoutException"}}]"""
        }).ToArray();
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);
        var first = await Read(host, "list_failures", "{}");
        var failure = Assert.ContainsSingle(first.GetProperty("failures").EnumerateArray());
        Assert.AreEqual(100, failure.GetProperty("count").GetInt32());
        Assert.AreEqual("TimeoutException", failure.GetProperty("exception_type").GetString());
        var identity = AnalysisEngine.FailureIdentity("alpha", rows[0]);
        rows.ToList().ForEach(row => Assert.AreEqual(identity, AnalysisEngine.FailureIdentity("alpha", row)));
        Assert.AreNotEqual(identity, AnalysisEngine.FailureIdentity("beta", rows[0]));
    }

    [TestMethod]
    public async Task Log_pages_use_log_identity_preserve_plain_text_and_report_the_total()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var rows = Enumerable.Range(1, 5).Select(i => new LogStorageRow
        {
            ProjectId = "alpha", LogId = "log-" + i, TraceId = TraceId(1), SpanId = SpanId(1),
            TimeUnixNano = Timestamp, SeverityNumber = 17, ServiceName = "logs", Body = "{\"message\":\"literal " + i + "\"}"
        }).ToArray();
        await host.Store.InsertLogsAsync(rows, testContext.CancellationToken);
        var seen = new HashSet<string>(); string? cursor = null;
        for (var page = 0; page < 3; page++)
        {
            var input = new JsonObject { ["limit"] = 2, ["cursor"] = cursor };
            var result = await Read(host, "search_logs", input.ToJsonString());
            Assert.AreEqual(5, result.GetProperty("total_matched").GetInt32());
            foreach (var log in result.GetProperty("logs").EnumerateArray())
            {
                var body = log.GetProperty("body").GetString()!;
                Assert.StartsWith("{\"message\":", body, StringComparison.Ordinal);
                Assert.IsTrue(seen.Add(body), "log repeated across pages");
            }
            cursor = result.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
        }
        Assert.AreEqual(5, seen.Count);
        Assert.IsNull(cursor);
    }

    [TestMethod]
    public async Task Failure_status_rejects_poisoned_values_and_enforces_etags()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(1), SpanId(1), Timestamp, Timestamp + 10, status: 2)]), testContext.CancellationToken);
        var failure = (await Read(host, "list_failures", "{}")).GetProperty("failures")[0];
        var route = "/api/v1/failures/" + failure.GetProperty("failure_id").GetString();
        var etag = failure.GetProperty("etag").GetString()!;
        var headers = new Dictionary<string, string> { ["If-Match"] = etag };
        foreach (var body in new[] { "{\"status\":\"open\",\"ignore_until\":\"2026-01-01T00:00:00Z\"}", "{\"status\":\"ignored\",\"ignore_until\":\"tomorrow\"}", "{\"status\":\"open\",\"assignee\":42}" })
        {
            using var rejected = await host.SendAsync(HttpMethod.Patch, route, "writer", body, headers);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        using var missing = await host.SendAsync(HttpMethod.Patch, route, "writer", "{\"status\":\"resolved\"}");
        Assert.AreEqual((HttpStatusCode)428, missing.StatusCode);
        using var changed = await host.SendAsync(HttpMethod.Patch, route, "writer", "{\"status\":\"resolved\"}", headers);
        Assert.AreEqual(HttpStatusCode.OK, changed.StatusCode);
        Assert.IsTrue((await host.JsonAsync(changed)).GetProperty("failure").TryGetProperty("updated_at", out _));
        using var stale = await host.SendAsync(HttpMethod.Patch, route, "writer", "{\"status\":\"ignored\"}", headers);
        Assert.AreEqual(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.AreEqual("resolved", (await Read(host, "list_failures", "{\"status\":\"all\"}")).GetProperty("failures")[0].GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task Cumulative_histograms_subtract_overlapping_exports_and_keep_exemplars()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        var series = new MetricSeriesRow { ProjectId = "alpha", SeriesId = "hist", MetricName = "latency", MetricKind = 3, Temporality = 2, IsMonotonic = 0, FirstSeenUnixNano = time, LastSeenUnixNano = time + 1_000_000_000 };
        var first = new MetricPointRow { ProjectId = "alpha", SeriesId = "hist", TimeUnixNano = time, StartTimeUnixNano = time - 1_000_000_000, HistogramCount = 10, HistogramSum = 100, BucketBoundsJson = "[20]", BucketCountsJson = "[10,0]" };
        var second = first with { TimeUnixNano = time + 1_000_000_000, HistogramCount = 15, HistogramSum = 160, BucketCountsJson = "[15,0]" };
        await host.Store.InsertMetricsAsync([series],
            [MetricFixtures.Observation(series, first), MetricFixtures.Observation(series, second,
                [new Api.Contracts.OTel.Metrics.MetricExemplar
                {
                    TimeUnixNano = second.TimeUnixNano, TraceId = TraceId(1), FilteredAttributes = [],
                    Value = new Api.Contracts.Common.AttributeDoubleValue { Value = 12 }
                }])], testContext.CancellationToken);
        var result = await Read(host, "query_metric", """{"metric_name":"latency","aggregation":"count","step_ms":3600000}""");
        Assert.AreEqual(15, result.GetProperty("series")[0].GetProperty("buckets")[0].GetProperty("value").GetInt32());
        Assert.AreEqual(TraceId(1), Assert.ContainsSingle(result.GetProperty("exemplar_trace_ids").EnumerateArray()).GetString());
        var reset = second with { HistogramCount = 2, StartTimeUnixNano = second.TimeUnixNano, HistogramSum = 4, BucketCountsJson = "[2,0]" };
        Assert.AreEqual((ulong)2, MetricHistogram.HistogramDelta(reset, second, time)!.HistogramCount);
        Assert.IsNull(MetricHistogram.HistogramDelta(first, null, time));
    }

    [TestMethod]
    public async Task Percentile_on_a_gauge_is_rejected_not_answered_with_the_average()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        var series = new MetricSeriesRow { ProjectId = "alpha", SeriesId = "gauge", MetricName = "queue.depth", MetricKind = 1, Temporality = 0, IsMonotonic = 0, FirstSeenUnixNano = time, LastSeenUnixNano = time };
        var point = new MetricPointRow { ProjectId = "alpha", SeriesId = "gauge", TimeUnixNano = time, StartTimeUnixNano = time, Value = 7 };
        await host.Store.InsertMetricsAsync([series], [MetricFixtures.Observation(series, point)], testContext.CancellationToken);
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/query_metric", "reader", """{"metric_name":"queue.depth","aggregation":"p95","step_ms":3600000}""");
        var json = await host.JsonAsync(response);
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var error = Assert.ContainsSingle(json.GetProperty("errors").EnumerateArray());
        Assert.AreEqual("aggregation", error.GetProperty("field").GetString());
        Assert.AreEqual("aggregation.requires_histogram", error.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task Snapshot_window_and_incomplete_coverage_are_honest()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(1), SpanId(1), time, time + 10), Span(TraceId(2), SpanId(2), time - 10_000, time - 9_000)]), testContext.CancellationToken);
        var snapshot = await host.Store.ReadAnalysisSnapshotAsync("alpha", new AnalysisReadQuery { Start = time, End = time + 1000 }, testContext.CancellationToken);
        Assert.ContainsSingle(snapshot.Spans);
        var engine = new AnalysisEngine(snapshot with { ScanComplete = false }, "alpha", new AnalysisHandles(new byte[32]), FixtureUtcNow);
        var result = engine.Run("list_traces", new JsonObject());
        Assert.IsFalse(result["coverage"]!["scan_complete"]!.GetValue<bool>());
        Assert.IsNull(result["coverage"]!["matched"]);
        Assert.IsTrue(result["coverage"]!["truncated"]!.GetValue<bool>());
    }

    [TestMethod]
    public async Task Triage_caps_new_failures_and_ci_pages_only_real_phases()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = Timestamp;
        var rows = Enumerable.Range(1, 7).Select(i => Span(TraceId(i), SpanId(i), time + (ulong)i, time + (ulong)i + 1000, service: "failure-" + i, status: 2)).ToList();
        rows.AddRange(Enumerable.Range(10, 4).Select(i => Span(TraceId(10), SpanId(i), time + (ulong)i, time + (ulong)i + 1000, service: "qyl-ci-tests", sessionId: "ci") with { AttributesJson = i < 12 ? "{\"cicd.pipeline.task.name\":\"build\"}" : "{}" }));
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);
        var triage = await Read(host, "triage", "{\"limit\":2}");
        Assert.AreEqual(2, triage.GetProperty("failures").GetArrayLength());
        Assert.IsTrue(triage.GetProperty("has_more").GetBoolean());
        var ci = await Read(host, "ci_log", "{\"run_id\":\"ci\",\"limit\":1}");
        Assert.ContainsSingle(ci.GetProperty("phases").EnumerateArray());
        var next = new JsonObject { ["run_id"] = "ci", ["limit"] = 1, ["phase_cursor"] = ci.GetProperty("next_phase_cursor").GetString() };
        var second = await Read(host, "ci_log", next.ToJsonString());
        Assert.ContainsSingle(second.GetProperty("phases").EnumerateArray());
        Assert.AreNotEqual(ci.GetProperty("phases")[0].GetProperty("span_id").GetString(), second.GetProperty("phases")[0].GetProperty("span_id").GetString());
        Assert.IsFalse(second.TryGetProperty("next_phase_cursor", out var cursor) && cursor.ValueKind == JsonValueKind.String);
    }

    /// <summary>
    /// get_mcp_stats carries the window's buckets, the server, transport and method breakdowns and the
    /// per-tool statistics computed from the same MCP spans as its totals.
    /// </summary>
    [TestMethod]
    public async Task Mcp_stats_report_buckets_breakdowns_and_tools_from_the_same_spans()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        const ulong Minute = 60_000_000_000, Millisecond = 1_000_000;
        const ulong Start = FixtureUnixNano - 60 * Minute;
        static string Mcp(string method, string? tool = null, string? server = null, string? transport = null)
        {
            var attributes = new JsonObject { ["mcp.method.name"] = method };
            if (tool is not null) attributes["gen_ai.tool.name"] = tool;
            if (server is not null) attributes["mcp.server.name"] = server;
            if (transport is not null) attributes["mcp.transport"] = transport;
            return attributes.ToJsonString();
        }
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), Start, Start + 10 * Millisecond) with { AttributesJson = Mcp("tools/call", "search_logs", "qyl.mcp", "stdio") },
            Span(TraceId(2), SpanId(2), Start + 30_000 * Millisecond, Start + 30_030 * Millisecond, status: 2) with { AttributesJson = Mcp("tools/call", "search_logs", "qyl.mcp", "stdio") },
            Span(TraceId(3), SpanId(3), Start + 61_000 * Millisecond, Start + 61_020 * Millisecond) with { AttributesJson = Mcp("tools/call", "get_trace", "qyl.mcp", "streamable_http") },
            Span(TraceId(4), SpanId(4), FixtureUnixNano - Millisecond, FixtureUnixNano + 4 * Millisecond, service: "edge-mcp") with { AttributesJson = Mcp("initialize") },
            Span(TraceId(5), SpanId(5), Start + Minute, Start + Minute + Millisecond) with { AttributesJson = "{\"gen_ai.tool.name\":\"not-mcp\"}" },
            Span(TraceId(6), SpanId(6), Start - Millisecond, Start) with { AttributesJson = Mcp("tools/call", "search_logs", "qyl.mcp", "stdio") }
        ]), testContext.CancellationToken);

        var window = $$"""{"start_time":"{{FixtureUtcNow.AddMinutes(-60):O}}","end_time":"{{FixtureUtcNow:O}}"}""";
        Assert.IsFalse((await Read(host, "get_mcp_stats", window)).TryGetProperty("buckets", out _), "buckets come only on request");
        var stats = await Read(host, "get_mcp_stats", window[..^1] + ",\"buckets\":true}");

        Assert.AreEqual(4, stats.GetProperty("span_count_analyzed").GetInt32());
        Assert.AreEqual(4, stats.GetProperty("totals").GetProperty("requests").GetInt32());
        Assert.AreEqual(1, stats.GetProperty("totals").GetProperty("errors").GetInt32());
        Assert.IsFalse(stats.GetProperty("truncated").GetBoolean());
        Assert.AreEqual("live", stats.GetProperty("mode").GetString());

        Assert.AreEqual(60_000, stats.GetProperty("bucket_ms").GetInt64());
        // Only the buckets that saw requests.
        var buckets = stats.GetProperty("buckets").EnumerateArray().ToArray();
        Assert.AreSequenceEqual([FixtureUtcNow.AddMinutes(-60), FixtureUtcNow.AddMinutes(-59), FixtureUtcNow.AddMinutes(-1)], buckets.Select(static b => b.GetProperty("start").GetDateTimeOffset()));
        Assert.AreEqual((2, 1), (buckets[0].GetProperty("requests").GetInt32(), buckets[0].GetProperty("errors").GetInt32()));
        Assert.AreEqual(1, buckets[1].GetProperty("requests").GetInt32());
        Assert.AreEqual(1, buckets[2].GetProperty("requests").GetInt32());
        Assert.AreEqual(4, buckets.Sum(static bucket => bucket.GetProperty("requests").GetInt32()));

        static string[] Counts(JsonElement list) => [.. list.EnumerateArray().Select(static item => $"{item.GetProperty("name").GetString()}={item.GetProperty("requests").GetInt32()}")];
        Assert.AreSequenceEqual(["qyl.mcp=3", "edge-mcp=1"], Counts(stats.GetProperty("by_server")));
        Assert.AreSequenceEqual(["stdio=2", "=1", "streamable_http=1"], Counts(stats.GetProperty("by_transport")));
        Assert.AreSequenceEqual(["tools/call=3", "initialize=1"], Counts(stats.GetProperty("by_method")));

        var tools = stats.GetProperty("tools").EnumerateArray().ToArray();
        Assert.AreSequenceEqual(["search_logs", "get_trace"], tools.Select(static tool => tool.GetProperty("name").GetString()));
        Assert.AreEqual((2, 1, 0.5, 20d, 29d), (tools[0].GetProperty("requests").GetInt32(), tools[0].GetProperty("errors").GetInt32(),
            tools[0].GetProperty("error_rate").GetDouble(), tools[0].GetProperty("avg_ms").GetDouble(), tools[0].GetProperty("p95_ms").GetDouble()));
        Assert.AreEqual(20d, tools[1].GetProperty("p95_ms").GetDouble());
        Assert.AreEqual(2, stats.GetProperty("page_totals").GetProperty("tools").GetInt32());

        var comparison = (await Read(host, "get_mcp_stats", window[..^1] + ",\"compare_to\":\"previous_window\"}")).GetProperty("comparison");
        Assert.AreEqual(1, comparison.GetProperty("totals").GetProperty("requests").GetInt32());
        Assert.IsFalse(comparison.TryGetProperty("buckets", out _), "a comparison carries only its traffic");
    }

    [TestMethod]
    public async Task Equal_groups_and_plain_version_comparisons_are_supported()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(1), SpanId(1), Timestamp, Timestamp + 1000) with { AttributesJson = "{\"mcp.method.name\":\"tools/call\"}", ResourceJson = "{\"service.version\":\"v1\"}" }]), testContext.CancellationToken);
        var compare = await Read(host, "compare_groups", "{\"signal\":\"spans\",\"group_a\":{\"matchers\":[]},\"group_b\":{\"matchers\":[]}}");
        Assert.IsFalse(compare.GetProperty("composition").GetProperty("changed").GetBoolean());
        var stats = await Read(host, "get_mcp_stats", "{\"compare_to\":\"v1\"}");
        Assert.IsTrue(stats.TryGetProperty("comparison", out _));
        using var invalid = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/triage", "reader", "{\"limit\":51}");
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [TestMethod]
    public async Task Metric_ratios_match_group_identity_instead_of_series_position()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var rows = new[] { ("num-a", "requests", "a", 10d), ("num-b", "requests", "b", 40d),
            ("num-c", "requests", "c", 2d), ("den-a", "total", "b", 10d), ("den-z", "total", "a", 20d) };
        var current = Timestamp;
        var baseline = current - 3_600_000_000_000;
        var metricSeries = rows.Select(r => new MetricSeriesRow { ProjectId = "alpha", SeriesId = r.Item1, MetricName = r.Item2,
            MetricKind = 1, Temporality = 0, IsMonotonic = 0, FirstSeenUnixNano = baseline, LastSeenUnixNano = current,
            AttributesJson = "{\"http.route\":\"" + r.Item3 + "\"}" }).ToArray();
        var observations = rows.SelectMany(r => new[] { baseline, current }.Select(time =>
            MetricFixtures.Observation(metricSeries.Single(series => series.SeriesId == r.Item1),
                new MetricPointRow { ProjectId = "alpha", SeriesId = r.Item1, TimeUnixNano = time, Value = r.Item4 }))).ToArray();
        await host.Store.InsertMetricsAsync(metricSeries, observations, testContext.CancellationToken);
        var result = await Read(host, "query_metric", """{"metric_name":"requests","group_by":["http.route"],"step_ms":3600000,"compare_to":"previous_window","ratio_of":{"metric_name":"total"}}""");
        foreach (var series in new[] { result.GetProperty("series"), result.GetProperty("comparison").GetProperty("series") })
        {
            var values = series.EnumerateArray().ToDictionary(
                s => s.GetProperty("attributes")[0].GetProperty("value").GetString()!,
                s => s.GetProperty("buckets")[0].GetProperty("value"));
            Assert.AreEqual(.5, values["a"].GetDouble());
            Assert.AreEqual(4, values["b"].GetDouble());
            Assert.AreEqual(JsonValueKind.Null, values["c"].ValueKind);
        }
    }

    [TestMethod]
    public async Task Ci_links_observed_reruns_without_fabricating_attempts_for_unknown_providers()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var rows = Enumerable.Range(1, 3).Select(i => Span(TraceId(i), SpanId(i), Timestamp + (ulong)i, Timestamp + (ulong)i + 1000,
            service: "qyl-ci-tests", sessionId: "run-" + i) with
        { AttributesJson = i < 3 ? "{\"cicd.pipeline.run.id\":\"same-provider-run\",\"cicd.pipeline.task.name\":\"build\"}" : "{}" }).ToArray();
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);
        var result = await Read(host, "ci_log", """{"run_id":"run-2"}""");
        var runs = result.GetProperty("runs").EnumerateArray().ToDictionary(r => r.GetProperty("run_id").GetString()!);
        Assert.AreEqual(2, runs["run-2"].GetProperty("attempt").GetInt32());
        var prior = Assert.ContainsSingle(runs["run-2"].GetProperty("attempts").EnumerateArray());
        Assert.AreEqual("run-1", prior.GetProperty("run_id").GetString());
        Assert.AreEqual(1, prior.GetProperty("attempt").GetInt32());
        Assert.IsFalse(runs["run-3"].TryGetProperty("attempt", out _));
    }
}
