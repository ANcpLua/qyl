using System.Net;
using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// An analysis read holds at most <see cref="AnalysisReadQuery.MaximumRows"/> spans, the newest ones.
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
        Assert.IsFalse(logs.TryGetProperty("total_matched", out _), "the cap cut matching records off");
        Assert.AreEqual(2, region.GetProperty("cardinality").GetInt32());
        Assert.IsFalse(region.GetProperty("cardinality_exact").GetBoolean());
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

    private static SpanStorageRow Request(int id, ulong start, bool failed = false) =>
        Span(TraceId(id), SpanId(id), start, start + 500, status: failed ? (byte)2 : (byte)1);

    private static Task<JsonElement> VerifyAsync(CollectorApiTestHost host, string input) => ReadAsync(host, "verify_fix", input);

    private static async Task<JsonElement> ReadAsync(CollectorApiTestHost host, string operation, string input)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + operation, ReaderApiKey, input);
        var json = await host.JsonAsync(response);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, json.GetRawText());
        return json;
    }
}
