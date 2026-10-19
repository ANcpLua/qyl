using System.Net;
using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>An analysis applies the request it was given: its exact boundaries and its saved filter.</summary>
public sealed partial class AnalysisApiTests
{
    /// <summary>
    /// Window boundaries apply to the 100 ns an instant holds. A finer boundary is rejected rather than
    /// moved, while digits that are zero below 100 ns change nothing.
    /// </summary>
    [TestMethod]
    [DataRow("2026-10-17T11:00:00.000000049Z", null)]
    [DataRow("2026-10-17T11:00:00.0000001Z", 1)]
    [DataRow("2026-10-17T11:00:00.000000100Z", 1)]
    [DataRow("2026-10-17T11:00:00Z", 2)]
    public async Task Boundaries_finer_than_100_nanoseconds_are_rejected_not_rounded(string start, int? count)
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var hour = FixtureUnixNano - 60 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(1), SpanId(1), hour, hour + 1), Span(TraceId(2), SpanId(2), hour + 100, hour + 101)]), testContext.CancellationToken);

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/aggregate_telemetry", ReaderApiKey,
            $$"""{"signal":"spans","measures":["count"],"start_time":"{{start}}","end_time":"2026-10-17T11:00:01Z"}""");
        var result = await host.JsonAsync(response);

        if (count is null)
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, result.GetRawText());
            var error = Assert.ContainsSingle(result.GetProperty("errors").EnumerateArray());
            Assert.AreEqual(("start_time", "time.precision_unsupported"), (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()));
            return;
        }
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, result.GetRawText());
        Assert.AreEqual(count, Assert.ContainsSingle(result.GetProperty("groups").EnumerateArray()).GetProperty("measures").GetProperty("count").GetInt32());
    }

    /// <summary>
    /// Without an id run_saved_query lists the saved queries; a run answers like list_traces, and its coverage counts
    /// what its saved filter matched, not every record in the window.
    /// </summary>
    [TestMethod]
    public async Task A_saved_query_run_reports_the_coverage_of_its_filter()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1_000_000, service: "wanted"),
            Span(TraceId(2), SpanId(2), time, time + 1_000_000, service: "other")
        ]), testContext.CancellationToken);
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/saved-queries", "writer",
            """{"name":"wanted traces","filter":{"signal":"traces","service_name":"wanted"}}""");
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var id = (await host.JsonAsync(created)).GetProperty("id").GetString();

        var listed = await Read(host, "run_saved_query", """{"signal":"traces"}""");
        Assert.AreEqual(id, Assert.ContainsSingle(listed.GetProperty("saved_queries").EnumerateArray()).GetProperty("id").GetString());
        Assert.IsEmpty((await Read(host, "run_saved_query", """{"signal":"logs"}""")).GetProperty("saved_queries").EnumerateArray());

        var run = await Read(host, "run_saved_query", $$"""{"id":"{{id}}","signal":"logs"}""");

        Assert.AreEqual("wanted traces", run.GetProperty("saved_query").GetProperty("name").GetString());
        Assert.AreEqual(TraceId(1), Assert.ContainsSingle(run.GetProperty("traces").EnumerateArray()).GetProperty("trace_id").GetString());
        var coverage = run.GetProperty("coverage");
        Assert.AreEqual((2, 1, 1), (coverage.GetProperty("examined").GetInt32(), coverage.GetProperty("matched").GetInt32(), coverage.GetProperty("returned").GetInt32()));
    }

    /// <summary>
    /// Every comparison names its baseline the way triage does, and every error filter is errors_only: get_mcp_stats
    /// shifts its window back by offset_minutes or takes an explicit one, ci_log compares a run with another by its
    /// ci_run_id, and errors_only keeps the failed runs and sessions.
    /// </summary>
    [TestMethod]
    public async Task Comparisons_take_a_baseline_in_triages_shape_and_errors_only_filters_failures()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        SpanStorageRow Call(int id, ulong minutesAgo) => Span(TraceId(id), SpanId(id), FixtureUnixNano - minutesAgo * Minute, FixtureUnixNano - minutesAgo * Minute + 1000)
            with { AttributesJson = """{"mcp.method.name":"tools/call"}""" };
        SpanStorageRow Build(int id, ulong minutesAgo, string run, bool failed) => Span(TraceId(id), SpanId(id), FixtureUnixNano - minutesAgo * Minute,
            FixtureUnixNano - minutesAgo * Minute + 2000, service: "qyl-ci-tests", status: failed ? (byte)2 : (byte)1, sessionId: run)
            with { Name = "build", AttributesJson = """{"cicd.pipeline.task.name":"build"}""" };
        await host.Store.EnqueueAsync(new SpanBatch([Call(1, 30), Call(2, 90), Call(3, 91), Build(4, 50, "ci-old", false), Build(5, 20, "ci-new", true)]),
            testContext.CancellationToken);

        var shifted = await Read(host, "get_mcp_stats", """{"baseline":{"offset_minutes":60}}""");
        Assert.AreEqual((1, 2), (shifted.GetProperty("totals").GetProperty("requests").GetInt32(),
            shifted.GetProperty("comparison").GetProperty("totals").GetProperty("requests").GetInt32()));
        var start = FixtureUtcNow.AddMinutes(-95); var end = FixtureUtcNow.AddMinutes(-85);
        var explicitWindow = (await Read(host, "get_mcp_stats", $$$"""{"baseline":{"start_time":"{{{start:O}}}","end_time":"{{{end:O}}}"}}""")).GetProperty("comparison");
        Assert.AreEqual(2, explicitWindow.GetProperty("totals").GetProperty("requests").GetInt32());
        Assert.AreEqual(start, explicitWindow.GetProperty("window").GetProperty("start_time").GetDateTimeOffset());

        var compared = await Read(host, "ci_log", """{"ci_run_id":"ci-new","baseline":{"ci_run_id":"ci-old"}}""");
        Assert.AreEqual("ci-new", compared.GetProperty("ci_run_id").GetString());
        Assert.AreEqual("ci-old", compared.GetProperty("comparison").GetProperty("baseline_ci_run_id").GetString());
        Assert.AreEqual("build", Assert.ContainsSingle(compared.GetProperty("comparison").GetProperty("new_failures").EnumerateArray()).GetProperty("phase").GetString());

        var failedRuns = await Read(host, "ci_log", """{"errors_only":true}""");
        Assert.AreEqual("ci-new", Assert.ContainsSingle(failedRuns.GetProperty("runs").EnumerateArray()).GetProperty("ci_run_id").GetString());
        var failedSessions = await Read(host, "list_sessions", """{"errors_only":true}""");
        Assert.AreEqual("ci-new", Assert.ContainsSingle(failedSessions.GetProperty("sessions").EnumerateArray()).GetProperty("session_id").GetString());
    }
}
