using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// verify_fix compares the change with what came before it: the failure's whole known history, or the
/// request's lookback when it names matchers instead of a failure.
/// </summary>
public sealed partial class AnalysisApiTests
{
    [TestMethod]
    public async Task Verify_fix_measures_before_from_the_first_occurrence_and_waits_whole_minutes()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        SpanStorageRow Checkout(int id, ulong minutesAgo, bool failed) =>
            Span(TraceId(id), SpanId(id), FixtureUnixNano - minutesAgo * Minute, FixtureUnixNano - minutesAgo * Minute + 1000, status: failed ? (byte)2 : (byte)1)
                with { Name = "checkout", StatusMessage = failed ? "boom" : null };
        await host.Store.EnqueueAsync(new SpanBatch([Checkout(1, 180, true), Checkout(2, 150, false), Checkout(3, 120, true), Checkout(4, 90, false), Checkout(5, 5, false)]),
            testContext.CancellationToken);
        var failureId = Assert.ContainsSingle((await Read(host, "list_failures", """{"lookback_minutes":240}""")).GetProperty("failures").EnumerateArray())
            .GetProperty("failure_id").GetString();
        var since = FixtureUtcNow.AddMinutes(-10.5).ToString("O");

        var failure = await Read(host, "verify_fix", $$"""{"failure_id":"{{failureId}}","since":"{{since}}"}""");
        var before = failure.GetProperty("before");
        Assert.AreEqual(FixtureUtcNow.AddMinutes(-180), before.GetProperty("window").GetProperty("start_time").GetDateTimeOffset());
        Assert.AreEqual((2, 4), (before.GetProperty("occurrences").GetInt32(), before.GetProperty("requests").GetInt32()));
        Assert.AreEqual("pending", failure.GetProperty("verdict").GetString());
        Assert.AreEqual(JsonValueKind.Number, failure.GetProperty("needs").GetProperty("minutes").ValueKind);
        Assert.AreEqual("20", failure.GetProperty("needs").GetProperty("minutes").GetRawText());

        var matched = await Read(host, "verify_fix", $$"""{"matchers":[{"key":"service.name","op":"eq","value":"checkout"}],"since":"{{since}}","lookback_minutes":120}""");
        before = matched.GetProperty("before");
        Assert.AreEqual(FixtureUtcNow.AddMinutes(-130.5), before.GetProperty("window").GetProperty("start_time").GetDateTimeOffset());
        Assert.AreEqual((1, 2), (before.GetProperty("occurrences").GetInt32(), before.GetProperty("requests").GetInt32()));
    }
}
