using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// A read answers the question it was asked: traces come in the requested order, a release row
/// describes its own environment, and a link names a retained target as available.
/// </summary>
public sealed partial class AnalysisApiTests
{
    private const ulong Second = 1_000_000_000;

    /// <summary>
    /// Newest first, longest first or most error spans first; equal keys keep the newest trace first.
    /// </summary>
    [TestMethod]
    public async Task List_traces_orders_by_the_requested_field_and_newest_first_within_ties()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(4), SpanId(4), time + 4 * Second, time + 4 * Second + 3_000_000, status: 2),
            Span(TraceId(1), SpanId(1), time + 3 * Second, time + 3 * Second + 1_000_000, status: 2),
            Span(TraceId(2), SpanId(2), time + 2 * Second, time + 7 * Second),
            Span(TraceId(3), SpanId(3), time, time + 2 * Second, status: 2),
            Span(TraceId(3), SpanId(5), time + 1, time + Second, status: 2, parentSpanId: SpanId(3))
        ]), testContext.CancellationToken);

        async Task<string?[]> Order(string orderBy) =>
            [.. (await Read(host, "list_traces", $$"""{"order_by":"{{orderBy}}"}""")).GetProperty("traces").EnumerateArray().Select(static t => t.GetProperty("trace_id").GetString())];

        Assert.AreSequenceEqual([TraceId(4), TraceId(1), TraceId(2), TraceId(3)], await Order("start_time"));
        Assert.AreSequenceEqual([TraceId(2), TraceId(3), TraceId(4), TraceId(1)], await Order("duration"));
        Assert.AreSequenceEqual([TraceId(3), TraceId(4), TraceId(1), TraceId(2)], await Order("errors"));
    }

    /// <summary>
    /// A release row's before and after cohorts, traffic share and current flag come from its own
    /// environment: a production failure on the same version is not a staging regression.
    /// </summary>
    [TestMethod]
    public async Task A_release_row_measures_only_its_own_environment()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 40 * Minute;
        static string Release(string environment) => $$"""{"service.version":"v1","deployment.environment.name":"{{environment}}"}""";
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + Second, status: 1) with { ResourceJson = Release("staging") },
            Span(TraceId(2), SpanId(2), time + 30 * Minute, time + 30 * Minute + Second, status: 2) with { ResourceJson = Release("production") }
        ]), testContext.CancellationToken);

        var releases = (await Read(host, "list_releases", "{}")).GetProperty("releases").EnumerateArray()
            .ToDictionary(static r => r.GetProperty("environment").GetString()!);

        var staging = releases["staging"];
        Assert.AreEqual(0d, staging.GetProperty("after").GetProperty("error_rate").GetDouble());
        Assert.IsEmpty(staging.GetProperty("new_failure_ids").EnumerateArray());
        Assert.AreEqual(1d, staging.GetProperty("traffic_share").GetDouble());
        Assert.IsTrue(staging.GetProperty("current").GetBoolean());
        var production = releases["production"];
        Assert.AreEqual(1d, production.GetProperty("after").GetProperty("error_rate").GetDouble());
        Assert.ContainsSingle(production.GetProperty("new_failure_ids").EnumerateArray());
    }

    /// <summary>
    /// get_trace reads one trace, but a link's target lives in another one: availability is looked
    /// up in the project's retained spans, never in another project.
    /// </summary>
    [TestMethod]
    public async Task A_link_to_a_retained_span_of_another_trace_is_available()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        static string Link(int trace, int span) => $$"""{"traceId":"{{TraceId(trace)}}","spanId":"{{SpanId(span)}}","droppedAttributesCount":0,"flags":0}""";
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + Second) with { LinksJson = "[" + Link(2, 2) + "," + Link(3, 3) + "," + Link(4, 4) + "]" },
            Span(TraceId(2), SpanId(2), time - 90 * Minute, time - 90 * Minute + Second),
            Span(TraceId(4), SpanId(4), time, time + Second, project: "beta")
        ]), testContext.CancellationToken);

        var links = (await Read(host, "get_trace", $$"""{"trace_id":"{{TraceId(1)}}"}""")).GetProperty("links").EnumerateArray()
            .ToDictionary(static l => l.GetProperty("trace_id").GetString()!, static l => l.GetProperty("available").GetBoolean());

        Assert.IsTrue(links[TraceId(2)], "the target is retained in this project");
        Assert.IsFalse(links[TraceId(3)], "no span has this identity");
        Assert.IsFalse(links[TraceId(4)], "the only such span belongs to another project");
    }
}
