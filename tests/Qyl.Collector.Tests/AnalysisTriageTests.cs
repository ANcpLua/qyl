using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// A triage row names the release its failing spans run on, its own state and the failure it
/// propagated from; a silent service is one that stopped reporting for longer than it usually pauses.
/// </summary>
public sealed partial class AnalysisApiTests
{
    /// <summary>Every failing span runs 2.3; the healthy 2.2 seen later in the window is not the failure's release.</summary>
    [TestMethod]
    public async Task Triage_takes_the_coinciding_release_from_the_failing_spans_version()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            .. Enumerable.Range(1, 5).Select(i => Span(TraceId(i), SpanId(i), time + (ulong)i, time + (ulong)i + 1000, service: "regional", status: 2)
                with { Name = "request", ResourceJson = """{"service.version":"2.3"}""" }),
            .. Enumerable.Range(6, 5).Select(i => Span(TraceId(i), SpanId(i), time + Minute + (ulong)i, time + Minute + (ulong)i + 1000, service: "regional", status: 1)
                with { Name = "request", ResourceJson = """{"service.version":"2.2"}""" })
        ]), testContext.CancellationToken);

        var failure = Assert.ContainsSingle((await Read(host, "triage", "{}")).GetProperty("failures").EnumerateArray());

        Assert.AreEqual("2.3", failure.GetProperty("coinciding_release").GetProperty("version").GetString());
    }

    /// <summary>
    /// Silent means the last span is older than the window's end minus 15 minutes or five usual pauses,
    /// whichever is longer, over the baseline and the window: a minute-by-minute service that stopped 20
    /// minutes ago is silent, a service reporting every 10 minutes that last spoke 30 minutes ago is not.
    /// </summary>
    [TestMethod]
    public async Task A_silent_service_stopped_for_longer_than_it_usually_pauses()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var stopped = Enumerable.Range(0, 80).Select(i => FixtureUnixNano - 100 * Minute + (ulong)i * Minute)
            .Select((start, i) => Span(TraceId(100 + i), SpanId(100 + i), start, start + 1000, service: "every-minute"));
        var sparse = Enumerable.Range(0, 9).Select(i => FixtureUnixNano - 110 * Minute + (ulong)i * 10 * Minute)
            .Select((start, i) => Span(TraceId(300 + i), SpanId(300 + i), start, start + 1000, service: "every-ten-minutes"));
        await host.Store.EnqueueAsync(new SpanBatch([.. stopped, .. sparse,
            Span(TraceId(400), SpanId(400), FixtureUnixNano - 90 * Minute, FixtureUnixNano - 90 * Minute + 1000, service: "baseline-only")]), testContext.CancellationToken);

        foreach (var operation in new[] { "triage", "check_telemetry_health" })
        {
            var silent = (await Read(host, operation, "{}")).GetProperty("silent_services").EnumerateArray()
                .Select(static s => s.GetProperty("service").GetString()).ToArray();
            Assert.AreSequenceEqual(["baseline-only", "every-minute"], silent, operation);
        }
    }

    /// <summary>
    /// The checkout error propagated from the database timeout below it, so its row names that failure as
    /// its root cause; the timeout is its own. Each row carries the status and etag update_failure_status needs.
    /// </summary>
    [TestMethod]
    public async Task Triage_rows_carry_status_etag_and_the_failure_they_propagated_from()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 2000, status: 2) with { Name = "checkout", StatusMessage = "dependency failed" },
            Span(TraceId(1), SpanId(2), time + 10, time + 1000, parentSpanId: SpanId(1), service: "orders-db", status: 2) with { Name = "db", StatusMessage = "database timeout" }
        ]), testContext.CancellationToken);

        var rows = (await Read(host, "triage", "{}")).GetProperty("failures").EnumerateArray().ToArray();
        string Id(string service) => rows.Single(r => r.GetProperty("services")[0].GetString() == service).GetProperty("failure_id").GetString()!;

        Assert.HasCount(2, rows);
        foreach (var row in rows)
        {
            Assert.AreEqual("open", row.GetProperty("status").GetString());
            Assert.IsFalse(string.IsNullOrEmpty(row.GetProperty("etag").GetString()));
            Assert.AreEqual(Id("orders-db"), row.GetProperty("root_cause_failure_id").GetString());
        }
    }
}
