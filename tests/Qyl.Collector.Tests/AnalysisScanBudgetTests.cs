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

        Assert.IsFalse(verification.GetProperty("coverage").GetProperty("scan_complete").GetBoolean());
        Assert.AreEqual(1, verification.GetProperty("before").GetProperty("occurrences").GetInt64());
        Assert.AreEqual(100, verification.GetProperty("after").GetProperty("requests").GetInt64());
        Assert.AreEqual("passed", verification.GetProperty("verdict").GetString());
    }

    private static SpanStorageRow Request(int id, ulong start, bool failed = false) =>
        Span(TraceId(id), SpanId(id), start, start + 500, status: failed ? (byte)2 : (byte)1);

    private static async Task<JsonElement> VerifyAsync(CollectorApiTestHost host, string input)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/verify_fix", ReaderApiKey, input);
        var json = await host.JsonAsync(response);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, json.GetRawText());
        return json;
    }
}
