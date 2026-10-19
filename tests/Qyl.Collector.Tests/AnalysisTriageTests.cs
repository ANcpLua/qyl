using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
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
        string Id(string service) => rows.Single(r => r.GetProperty("service").GetString() == service).GetProperty("failure_id").GetString()!;

        Assert.HasCount(2, rows);
        foreach (var row in rows)
        {
            Assert.AreEqual("open", row.GetProperty("status").GetString());
            Assert.IsFalse(string.IsNullOrEmpty(row.GetProperty("etag").GetString()));
            Assert.AreEqual(Id("orders-db"), row.GetProperty("root_cause_failure_id").GetString());
        }
    }

    /// <summary>
    /// triage lists the failures that need attention unless a status is asked for: open ones, a resolved one that
    /// recurred after it was resolved and an ignored one whose ignore_until passed. Any other status lists the
    /// failures recorded with it, and all lists every failure of the window.
    /// </summary>
    [TestMethod]
    public async Task Triage_lists_the_open_failures_by_default_and_the_others_by_their_recorded_status()
    {
        var clock = new FakeTimeProvider(FixtureUtcNow);
        await using var host = await StartAsync(testContext.CancellationToken, timeProvider: clock);
        SpanStorageRow Failing(int id, string operation, ulong start) =>
            Span(TraceId(id), SpanId(id), start, start + 1000, status: 2) with { Name = operation, StatusMessage = operation + " failed" };
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([Failing(1, "resolved", time), Failing(2, "ignored", time), Failing(3, "expired", time), Failing(4, "open", time)]),
            testContext.CancellationToken);
        var rows = (await Read(host, "triage", "{}")).GetProperty("failures").EnumerateArray().ToDictionary(static r => r.GetProperty("operation").GetString()!);
        async Task RecordAsync(string operation, string body)
        {
            using var response = await host.SendAsync(HttpMethod.Patch, "/api/v1/failures/" + rows[operation].GetProperty("failure_id").GetString(), "writer", body,
                new Dictionary<string, string> { ["If-Match"] = rows[operation].GetProperty("etag").GetString()! });
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
        await RecordAsync("resolved", """{"status":"resolved"}""");
        await RecordAsync("ignored", """{"status":"ignored"}""");
        await RecordAsync("expired", $$"""{"status":"ignored","ignore_until":"{{FixtureUtcNow.AddMinutes(5):O}}"}""");
        async Task<string[]> ListedAsync(string input) => [.. (await Read(host, "triage", input)).GetProperty("failures").EnumerateArray()
            .Select(static r => r.GetProperty("operation").GetString()!).Order(StringComparer.Ordinal)];

        Assert.AreSequenceEqual(["open"], await ListedAsync("{}"));
        Assert.AreSequenceEqual(["resolved"], await ListedAsync("""{"status":"resolved"}"""));
        Assert.AreSequenceEqual(["expired", "ignored"], await ListedAsync("""{"status":"ignored"}"""));
        Assert.AreSequenceEqual(["expired", "ignored", "open", "resolved"], await ListedAsync("""{"status":"all"}"""));

        // Ten minutes later the ignore has expired and the resolved failure has recurred.
        clock.Advance(TimeSpan.FromMinutes(10));
        await host.Store.EnqueueAsync(new SpanBatch([Failing(5, "resolved", FixtureUnixNano + Minute)]), testContext.CancellationToken);
        Assert.AreSequenceEqual(["expired", "open", "resolved"], await ListedAsync("{}"));
        var recurred = (await Read(host, "triage", "{}")).GetProperty("failures").EnumerateArray().Single(static r => r.GetProperty("operation").GetString() == "resolved");
        Assert.AreEqual(("resolved", "regressed"), (recurred.GetProperty("status").GetString(), recurred.GetProperty("change").GetString()));
    }

    /// <summary>A note on a failure is counted on its triage row and returned by get_failure_context with its failure_id.</summary>
    [TestMethod]
    public async Task A_note_on_a_failure_is_counted_by_triage_and_returned_by_its_context()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(1), SpanId(1), time, time + 1000, status: 2) with { Name = "charge", StatusMessage = "declined" }]),
            testContext.CancellationToken);
        var failure = Assert.ContainsSingle((await Read(host, "triage", "{}")).GetProperty("failures").EnumerateArray()).GetProperty("failure_id").GetString();
        using var created = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer",
            $$"""{"target":{"kind":"failure","failure_id":"{{failure}}"},"text":"card network outage","classification":"finding"}""");
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);

        Assert.AreEqual(1, Assert.ContainsSingle((await Read(host, "triage", "{}")).GetProperty("failures").EnumerateArray()).GetProperty("annotation_count").GetInt32());
        var notes = (await Read(host, "get_failure_context", $$"""{"failure_id":"{{failure}}"}""")).GetProperty("context").GetProperty("annotations").EnumerateArray();
        Assert.AreEqual("card network outage", Assert.ContainsSingle(notes).GetProperty("text").GetString());
    }
}
