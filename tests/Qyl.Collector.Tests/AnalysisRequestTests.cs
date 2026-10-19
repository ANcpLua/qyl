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

    /// <summary>A saved query's coverage counts what its saved filter matched, not every record in the window.</summary>
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

        var run = await Read(host, "run_saved_query", $$"""{"id":"{{id}}"}""");

        Assert.AreEqual(TraceId(1), Assert.ContainsSingle(run.GetProperty("result").GetProperty("items").EnumerateArray()).GetProperty("trace_id").GetString());
        var coverage = run.GetProperty("coverage");
        Assert.AreEqual((2, 1, 1), (coverage.GetProperty("examined").GetInt32(), coverage.GetProperty("matched").GetInt32(), coverage.GetProperty("returned").GetInt32()));
    }
}
