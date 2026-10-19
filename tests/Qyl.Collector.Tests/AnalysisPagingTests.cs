using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// Pages split one answer: an omitted limit is the contract's default, has_more, truncated and other
/// describe the same remainder, and a continuation never repeats or silently skips an item.
/// </summary>
public sealed partial class AnalysisApiTests
{
    /// <summary>other counts the records of the groups after this page, whichever measures were requested.</summary>
    [TestMethod]
    public async Task Aggregate_other_counts_the_records_of_the_omitted_groups()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch([.. Enumerable.Range(1, 3).Select(i => Region(i, "g" + i, (ulong)i * 1_000_000))]), testContext.CancellationToken);

        var result = await Read(host, "aggregate_telemetry", """{"signal":"spans","group_by":["region"],"measures":["duration_avg"],"limit":1}""");

        Assert.ContainsSingle(result.GetProperty("groups").EnumerateArray());
        Assert.AreEqual((2, 2), (result.GetProperty("other").GetProperty("groups").GetInt32(), result.GetProperty("other").GetProperty("count").GetInt32()));
    }

    /// <summary>
    /// Without a limit aggregate_telemetry returns the contract's 20 groups; the remainder is one page
    /// further, and has_more, truncated and other agree on every page.
    /// </summary>
    [TestMethod]
    public async Task An_omitted_aggregate_limit_pages_by_the_contract_default()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch([.. Enumerable.Range(1, 21).Select(i => Region(i, "g" + i, 1_000_000))]), testContext.CancellationToken);
        const string Input = """{"signal":"spans","group_by":["region"],"measures":["count"]}""";

        var first = await Read(host, "aggregate_telemetry", Input);
        Assert.HasCount(20, first.GetProperty("groups").EnumerateArray());
        AssertRemainder(first, groups: 1, records: 1);

        var next = JsonNode.Parse(Input)!.AsObject();
        next["cursor"] = first.GetProperty("next_cursor").GetString();
        var second = await Read(host, "aggregate_telemetry", next.ToJsonString());
        Assert.ContainsSingle(second.GetProperty("groups").EnumerateArray());
        AssertRemainder(second, groups: 0, records: 0);

        static void AssertRemainder(JsonElement page, int groups, int records)
        {
            var more = groups > 0;
            Assert.AreEqual(more, page.GetProperty("has_more").GetBoolean());
            Assert.AreEqual(more, page.GetProperty("coverage").GetProperty("truncated").GetBoolean());
            Assert.AreEqual(more, page.TryGetProperty("next_cursor", out _));
            Assert.AreEqual((groups, records), (page.GetProperty("other").GetProperty("groups").GetInt32(), page.GetProperty("other").GetProperty("count").GetInt32()));
        }
    }

    /// <summary>
    /// get_failure_context pages error spans and logs by the contract's default of 100; evidence that fits
    /// that page is complete and never reported as truncated.
    /// </summary>
    [TestMethod]
    public async Task Failure_context_reports_truncation_only_beyond_its_contract_page()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var start = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([.. Enumerable.Range(1, 60).Select(i =>
            Span(TraceId(i), SpanId(i), start + (ulong)i, start + (ulong)i + 1_000, sessionId: "busy", status: 2))]), testContext.CancellationToken);
        await host.Store.InsertLogsAsync([.. Enumerable.Range(1, 30).Select(i => new LogStorageRow
        {
            ProjectId = "alpha", LogId = "log-" + i, TraceId = TraceId(i), TimeUnixNano = start + (ulong)i, SeverityNumber = 17, Body = "\"failed\""
        })], testContext.CancellationToken);

        var context = await Read(host, "get_failure_context", """{"session_id":"busy"}""");

        Assert.HasCount(60, context.GetProperty("observed").GetProperty("error_spans").EnumerateArray());
        Assert.HasCount(30, context.GetProperty("observed").GetProperty("logs").EnumerateArray());
        Assert.IsFalse(context.GetProperty("has_more").GetBoolean());
        Assert.IsFalse(context.GetProperty("coverage").GetProperty("truncated").GetBoolean());
        Assert.DoesNotContain("Evidence was truncated.", context.GetProperty("unknowns").EnumerateArray().Select(static unknown => unknown.GetString()));
    }

    /// <summary>
    /// A span that arrives after the first page and sorts before its position is neither skipped nor
    /// answered with a repeated span: the continuation is rejected so the caller starts again. A span
    /// that sorts after the position continues normally.
    /// </summary>
    [TestMethod]
    public async Task A_continuation_rejects_an_item_that_arrived_before_its_position()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var trace = TraceId(7);
        SpanStorageRow At(int id, ulong minutesAgo) => Span(trace, SpanId(id), FixtureUnixNano - minutesAgo * Minute, FixtureUnixNano - minutesAgo * Minute + 1);
        await host.Store.EnqueueAsync(new SpanBatch([At(700, 20), At(701, 10)]), testContext.CancellationToken);
        var input = new JsonObject { ["trace_id"] = trace, ["max_spans"] = 1 };

        var first = await Read(host, "get_trace", input.ToJsonString());
        Assert.AreEqual(SpanId(700), Assert.ContainsSingle(first.GetProperty("trace").GetProperty("spans").EnumerateArray()).GetProperty("span_id").GetString());
        input["cursor"] = first.GetProperty("next_cursor").GetString();

        await host.Store.EnqueueAsync(new SpanBatch([At(702, 5)]), testContext.CancellationToken);
        var second = await Read(host, "get_trace", input.ToJsonString());
        Assert.AreEqual(SpanId(701), Assert.ContainsSingle(second.GetProperty("trace").GetProperty("spans").EnumerateArray()).GetProperty("span_id").GetString());
        input["cursor"] = second.GetProperty("next_cursor").GetString();

        await host.Store.EnqueueAsync(new SpanBatch([At(699, 30)]), testContext.CancellationToken);
        using var stale = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/get_trace", ReaderApiKey, input.ToJsonString());
        var error = await host.JsonAsync(stale);
        Assert.AreEqual(HttpStatusCode.BadRequest, stale.StatusCode, error.GetRawText());
        Assert.AreEqual(("cursor", "cursor.stale"), (error.GetProperty("errors")[0].GetProperty("field").GetString(), error.GetProperty("errors")[0].GetProperty("code").GetString()));
    }

    /// <summary>A list continuation is rejected, not skipped past, when an item arrived ahead of its position.</summary>
    [TestMethod]
    public async Task A_list_continuation_rejects_a_trace_that_arrived_ahead_of_it()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var window = $$"""
            "start_time":"{{FixtureUtcNow.AddHours(-1):O}}","end_time":"{{FixtureUtcNow:O}}","limit":1
            """;
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(1), SpanId(1), FixtureUnixNano - 20 * Minute, FixtureUnixNano - 20 * Minute + 1),
            Span(TraceId(2), SpanId(2), FixtureUnixNano - 30 * Minute, FixtureUnixNano - 30 * Minute + 1)]), testContext.CancellationToken);
        var first = await Read(host, "list_traces", "{" + window + "}");
        Assert.AreEqual(TraceId(1), Assert.ContainsSingle(first.GetProperty("traces").EnumerateArray()).GetProperty("trace_id").GetString());

        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(3), SpanId(3), FixtureUnixNano - 10 * Minute, FixtureUnixNano - 10 * Minute + 1)]), testContext.CancellationToken);
        using var stale = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/list_traces", ReaderApiKey,
            "{" + window + ",\"cursor\":\"" + first.GetProperty("next_cursor").GetString() + "\"}");
        var error = await host.JsonAsync(stale);

        Assert.AreEqual(HttpStatusCode.BadRequest, stale.StatusCode, error.GetRawText());
        Assert.AreEqual("cursor.stale", error.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    private static SpanStorageRow Region(int id, string region, ulong duration)
    {
        var start = FixtureUnixNano - 30 * Minute;
        return Span(TraceId(id), SpanId(id), start, start + duration) with { AttributesJson = "{\"region\":\"" + region + "\"}" };
    }
}
