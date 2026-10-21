using System.Net;
using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// An analysis read holds at most <see cref="AnalysisReadQuery.MaximumRows"/> rows of a table, the newest ones.
/// A verdict may rest only on windows the read covered completely. Each case stores more spans than the
/// cap, so the class runs alone instead of competing with the parallel suite.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class AnalysisScanBudgetTests(TestContext testContext)
{
    private const ulong Minute = 60_000_000_000;
    private const string Matchers = """[{"key":"service.name","op":"eq","value":"checkout"}]""";

    [TestMethod]
    public async Task A_recurrence_the_row_cap_cut_off_keeps_verify_fix_from_passing()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var since = FixtureUnixNano - 40 * Minute;
        var rows = new List<SpanStorageRow> { Request(0, since + Minute, failed: true) };
        rows.AddRange(Enumerable.Range(1, AnalysisReadQuery.MaximumRows + 1).Select(i => Request(i, since + 2 * Minute + (ulong)i * 1_000)));
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);

        var verification = await VerifyAsync(host, $$"""{"since":"{{FixtureUtcNow.AddMinutes(-40):O}}","matchers":{{Matchers}}}""");

        Assert.IsFalse(verification.GetProperty("coverage").GetProperty("scan_complete").GetBoolean());
        Assert.AreEqual("inconclusive", verification.GetProperty("verdict").GetString());
    }

    [TestMethod]
    public async Task A_cap_that_cut_only_older_spans_leaves_complete_evidence_windows()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var old = FixtureUnixNano - 119 * Minute;
        var rows = Enumerable.Range(1, AnalysisReadQuery.MaximumRows + 1).Select(i => Request(i, old + (ulong)i * 1_000)).ToList();
        rows.Add(Request(0, FixtureUnixNano - 50 * Minute, failed: true));
        rows.AddRange(Enumerable.Range(1, 100).Select(i => Request(100_000 + i, FixtureUnixNano - 20 * Minute + (ulong)i * 1_000)));
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);

        var verification = await VerifyAsync(host, $$"""{"since":"{{FixtureUtcNow.AddMinutes(-30):O}}","matchers":{{Matchers}}}""");

        var coverage = verification.GetProperty("coverage");
        Assert.IsFalse(coverage.GetProperty("scan_complete").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, coverage.GetProperty("matched").ValueKind, "the cap leaves the total unknown");
        Assert.IsFalse(coverage.GetProperty("truncated").GetBoolean(), "the answer left nothing out");
        Assert.AreEqual(1, verification.GetProperty("before").GetProperty("occurrences").GetInt64());
        Assert.AreEqual(100, verification.GetProperty("after").GetProperty("requests").GetInt64());
        Assert.AreEqual("passed", verification.GetProperty("verdict").GetString());
    }

    /// <summary>
    /// A capped log read knows neither how many records matched nor every distinct value: the oldest
    /// record holds a third region the cap cut off, so the total is omitted and the cardinality is not exact.
    /// </summary>
    [TestMethod]
    public async Task A_capped_log_read_claims_no_total_and_no_exact_cardinality()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var start = FixtureUnixNano - 50 * Minute;
        await host.Store.InsertLogsAsync([.. Enumerable.Range(0, AnalysisReadQuery.MaximumRows + 1).Select(i => new LogStorageRow
        {
            ProjectId = AlphaProjectId, LogId = "log-" + i, TimeUnixNano = start + (ulong)i * 1_000_000, SeverityNumber = 9, ServiceName = "checkout",
            Body = "entry", AttributesJson = "{\"region\":\"" + (i is 0 ? "c" : i % 2 is 0 ? "a" : "b") + "\"}"
        })], testContext.CancellationToken);

        var logs = await ReadAsync(host, "search_logs", """{"limit":1}""");
        var region = Assert.ContainsSingle((await ReadAsync(host, "discover_attributes", """{"signal":"logs","key":"region"}""")).GetProperty("attributes").EnumerateArray());

        Assert.IsFalse(logs.GetProperty("coverage").GetProperty("scan_complete").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, logs.GetProperty("coverage").GetProperty("matched").ValueKind, "the cap cut matching records off");
        Assert.AreEqual(2, region.GetProperty("cardinality").GetInt32(), "the values of the records the read held");
    }

    /// <summary>
    /// More log records than one read holds, all older than the window, make neither the failure's context nor its
    /// verification partial: the context reads the failure's own trace and the window, a verification only spans.
    /// </summary>
    [TestMethod]
    public async Task Records_an_analysis_does_not_read_leave_its_coverage_complete()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var old = FixtureUnixNano - 180 * Minute;
        await host.Store.InsertLogsAsync([.. Enumerable.Range(0, AnalysisReadQuery.MaximumRows + 1).Select(i => new LogStorageRow
        {
            ProjectId = AlphaProjectId, LogId = "old-" + i, TimeUnixNano = old + (ulong)i * 1_000, SeverityNumber = 9, ServiceName = "batch", Body = "entry"
        })], testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch([Request(1, FixtureUnixNano - 30 * Minute, failed: true), Request(2, FixtureUnixNano - 10 * Minute)]),
            testContext.CancellationToken);
        var failure = Assert.ContainsSingle((await ReadAsync(host, "triage", "{}")).GetProperty("failures").EnumerateArray()).GetProperty("failure_id").GetString();

        foreach (var (operation, input) in new[]
                 {
                     ("get_failure_context", $$"""{"failure_id":"{{failure}}"}"""),
                     ("verify_fix", $$"""{"failure_id":"{{failure}}","since":"{{FixtureUtcNow.AddMinutes(-20):O}}"}""")
                 })
        {
            var coverage = (await ReadAsync(host, operation, input)).GetProperty("coverage");
            Assert.IsTrue(coverage.GetProperty("scan_complete").GetBoolean(), operation);
            Assert.IsFalse(coverage.GetProperty("truncated").GetBoolean(), operation);
            Assert.AreEqual(JsonValueKind.Number, coverage.GetProperty("matched").ValueKind, operation);
        }
    }

    /// <summary>
    /// A release anchors the change at its first span, which the store finds over every retained span. A cap that
    /// cut only spans older than the change leaves the anchor known and the after window complete, so the verdict
    /// is decided.
    /// </summary>
    [TestMethod]
    public async Task A_release_anchor_stays_known_when_the_cap_cut_spans_older_than_the_change()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        const string Introduced = """{"service.version":"1.4.1"}""", Fixed = """{"service.version":"1.4.2"}""";
        SpanStorageRow Checkout(int id, ulong start, string resource, bool failed = false) =>
            Request(id, start, failed) with { Name = "checkout", ResourceJson = resource, StatusMessage = failed ? "timeout" : null };
        var old = FixtureUnixNano - 240 * Minute; var shipped = FixtureUnixNano - 60 * Minute;
        var rows = Enumerable.Range(1, AnalysisReadQuery.MaximumRows + 1).Select(i => Checkout(i, old + (ulong)i * 1_000, Introduced)).ToList();
        rows.Add(Checkout(0, FixtureUnixNano - 120 * Minute, Introduced, failed: true));
        rows.AddRange(Enumerable.Range(0, 120).Select(i => Checkout(100_000 + i, shipped + (ulong)i * Minute / 4, Fixed)));
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);
        var failure = Assert.ContainsSingle((await ReadAsync(host, "triage", """{"lookback_minutes":240}""")).GetProperty("failures").EnumerateArray()).GetProperty("failure_id").GetString();

        var verification = await VerifyAsync(host, $$"""{"failure_id":"{{failure}}","release":"1.4.2"}""");

        Assert.IsFalse(verification.GetProperty("coverage").GetProperty("scan_complete").GetBoolean(), "the cap cut the oldest 1.4.1 spans");
        var change = verification.GetProperty("change");
        Assert.AreEqual(("release", "checkout", "1.4.2"), (change.GetProperty("kind").GetString(), change.GetProperty("service").GetString(), change.GetProperty("version").GetString()));
        Assert.AreEqual(FixtureUtcNow.AddMinutes(-60), change.GetProperty("time").GetDateTimeOffset());
        Assert.AreEqual((0, 120), (verification.GetProperty("after").GetProperty("occurrences").GetInt32(), verification.GetProperty("after").GetProperty("requests").GetInt32()));
        Assert.AreEqual("passed", verification.GetProperty("verdict").GetString());
    }

    /// <summary>
    /// A saved query run over HTTP reads its saved signal over its own window, as the analysis route does: a window
    /// older than more spans than one read holds still lists its traces.
    /// </summary>
    [TestMethod]
    public async Task A_saved_query_run_over_http_reads_its_window_not_the_projects_newest_rows()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var recent = FixtureUnixNano - 30 * Minute;
        var rows = Enumerable.Range(1, AnalysisReadQuery.MaximumRows + 1).Select(i => Request(i, recent + (ulong)i * 1_000)).ToList();
        rows.AddRange([Request(100_001, FixtureUnixNano - 180 * Minute), Request(100_002, FixtureUnixNano - 170 * Minute)]);
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer", """{"name":"checkout","filter":{"signal":"traces","service_name":"checkout"}}""");
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString();

        using var response = await host.SendAsync(HttpMethod.Post, $"/api/v1/saved-queries/{id}:run", ReaderApiKey,
            $$"""{"start_time":"{{FixtureUtcNow.AddMinutes(-200):O}}","end_time":"{{FixtureUtcNow.AddMinutes(-160):O}}"}""");
        var run = await host.JsonAsync(response);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, run.GetRawText());
        Assert.AreSequenceEqual([TraceId(100_002), TraceId(100_001)], run.GetProperty("items").EnumerateArray().Select(static t => t.GetProperty("trace_id").GetString()));
        Assert.IsFalse(run.GetProperty("has_more").GetBoolean());
    }

    /// <summary>
    /// list_traces with a failure_id keeps the window's traces in which the failure occurred, so it reads the window:
    /// more spans than one read holds, all older than the window, leave its coverage complete.
    /// </summary>
    [TestMethod]
    public async Task List_traces_with_a_failure_id_reads_its_window_and_reports_it_complete()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var old = FixtureUnixNano - 180 * Minute;
        var rows = Enumerable.Range(1, AnalysisReadQuery.MaximumRows + 1).Select(i => Request(i, old + (ulong)i * 1_000)).ToList();
        rows.Add(Request(0, FixtureUnixNano - 30 * Minute, failed: true));
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);
        var failure = Assert.ContainsSingle((await ReadAsync(host, "triage", "{}")).GetProperty("failures").EnumerateArray()).GetProperty("failure_id").GetString();

        var traces = await ReadAsync(host, "list_traces", $$"""{"failure_id":"{{failure}}"}""");

        Assert.AreEqual(TraceId(0), Assert.ContainsSingle(traces.GetProperty("traces").EnumerateArray()).GetProperty("trace_id").GetString());
        var coverage = traces.GetProperty("coverage");
        Assert.IsTrue(coverage.GetProperty("scan_complete").GetBoolean(), "the window holds every span the answer could come from");
        Assert.AreEqual(1, coverage.GetProperty("matched").GetInt32());
    }

    /// <summary>
    /// A metric answer reads its own instrument's points, and check_telemetry_health only the conflicting ones: more
    /// points of another instrument than one read holds, all newer, leave query_metric, a saved metric query's run and
    /// the health report complete, and the health report still names an older conflict.
    /// </summary>
    [TestMethod]
    public async Task Another_instruments_points_leave_metric_answers_and_health_complete()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var measured = FixtureUnixNano - 50 * Minute; var noisy = FixtureUnixNano - 40 * Minute;
        var depth = Gauge("queue.depth", "s_depth", measured); var pool = Gauge("pool.size", "s_pool", FixtureUnixNano - 55 * Minute);
        var noise = Gauge("mcp.server.operation.duration", "s_noise", noisy);
        await host.Store.InsertMetricsAsync([depth], [Point(depth, measured, 7)], testContext.CancellationToken);
        await host.Store.InsertMetricsAsync([pool], [Point(pool, pool.LastSeenUnixNano, 3), Point(pool, pool.LastSeenUnixNano, 4)], testContext.CancellationToken);
        await host.Store.InsertMetricsAsync([noise], [.. Enumerable.Range(0, AnalysisReadQuery.MaximumRows + 1).Select(i => Point(noise, noisy + (ulong)i * 1_000, i))],
            testContext.CancellationToken);
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            """{"name":"depth","filter":{"signal":"metrics","metric_name":"queue.depth","aggregation":"last","step_ms":3600000}}""");
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString();

        var metric = await ReadAsync(host, "query_metric", """{"metric_name":"queue.depth","aggregation":"last","step_ms":3600000}""");
        var run = await ReadAsync(host, "run_saved_query", $$"""{"id":"{{id}}"}""");
        var health = await ReadAsync(host, "check_telemetry_health", "{}");

        Assert.AreEqual(7d, Assert.ContainsSingle(metric.GetProperty("series").EnumerateArray()).GetProperty("value").GetDouble());
        Assert.AreEqual(7d, Assert.ContainsSingle(run.GetProperty("metric").GetProperty("series").EnumerateArray()).GetProperty("value").GetDouble());
        foreach (var (operation, answer) in new[] { ("query_metric", metric), ("run_saved_query", run), ("check_telemetry_health", health) })
            Assert.IsTrue(answer.GetProperty("coverage").GetProperty("scan_complete").GetBoolean(), operation);
        var conflict = Assert.ContainsSingle(health.GetProperty("metric_conflicts").GetProperty("series").EnumerateArray());
        Assert.AreEqual(("pool.size", 1), (conflict.GetProperty("metric_name").GetString(), conflict.GetProperty("points").GetInt32()));
    }

    private static SpanStorageRow Request(int id, ulong start, bool failed = false) =>
        Span(TraceId(id), SpanId(id), start, start + 500, status: failed ? (byte)2 : (byte)1);

    private static MetricSeriesRow Gauge(string name, string seriesId, ulong lastSeen) => new()
    {
        ProjectId = AlphaProjectId, SeriesId = seriesId, MetricName = name, MetricKind = 1, Temporality = 0, IsMonotonic = 0,
        FirstSeenUnixNano = lastSeen, LastSeenUnixNano = lastSeen, ServiceName = "checkout"
    };

    private static MetricObservationRow Point(MetricSeriesRow series, ulong time, double value) =>
        MetricFixtures.Observation(series, new MetricPointRow { ProjectId = AlphaProjectId, SeriesId = series.SeriesId, TimeUnixNano = time, Value = value });

    private static Task<JsonElement> VerifyAsync(CollectorApiTestHost host, string input) => ReadAsync(host, "verify_fix", input);

    private static async Task<JsonElement> ReadAsync(CollectorApiTestHost host, string operation, string input)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + operation, ReaderApiKey, input);
        var json = await host.JsonAsync(response);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, json.GetRawText());
        return json;
    }
}
