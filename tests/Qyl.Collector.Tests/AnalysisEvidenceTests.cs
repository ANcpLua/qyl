using System.Text.Json;
using System.Text.Json.Nodes;
using Qyl.Collector.Analysis;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// Findings an agent acts on must rest on evidence the data justifies: scoped the way the request
/// asked, complete for the windows the answer covers, and keyed by the identities telemetry defines.
/// </summary>
public sealed partial class AnalysisApiTests
{
    private const ulong Minute = 60_000_000_000;

    /// <summary>
    /// verify_fix counts the requests its matchers select exactly as it selects failures: staging
    /// traffic cannot verify a production fix.
    /// </summary>
    [TestMethod]
    public async Task Verify_fix_counts_requests_and_failures_through_the_same_matchers()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        const string Production = """{"deployment.environment.name":"production"}""";
        const string Staging = """{"deployment.environment.name":"staging"}""";
        var failed = FixtureUnixNano - 90 * Minute;
        var rows = new List<SpanStorageRow>
        {
            Span(TraceId(1), SpanId(1), failed, failed + 1_000_000, service: "test", status: 2) with { Name = "operation", ResourceJson = Production }
        };
        rows.AddRange(Enumerable.Range(2, 100).Select(i =>
        {
            var start = FixtureUnixNano - 30 * Minute + (ulong)i * 1_000_000;
            return Span(TraceId(i), SpanId(i), start, start + 1_000_000, service: "test", status: 1) with { Name = "operation", ResourceJson = Staging };
        }));
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);

        var verification = await Read(host, "verify_fix", $$"""
            {"since":"{{FixtureUtcNow.AddHours(-1):O}}","matchers":[{"key":"service.name","op":"eq","value":"test"},
            {"key":"deployment.environment.name","op":"eq","value":"production","scope":"resource"}]}
            """);

        Assert.AreEqual(0, verification.GetProperty("after").GetProperty("requests").GetInt64(), "Staging requests are outside the matchers.");
        Assert.AreEqual(1, verification.GetProperty("before").GetProperty("occurrences").GetInt64());
        Assert.AreEqual(1, verification.GetProperty("before").GetProperty("requests").GetInt64());
        Assert.AreEqual("pending", verification.GetProperty("verdict").GetString());
        Assert.AreEqual(100, verification.GetProperty("needs").GetProperty("requests").GetInt64());
    }

    /// <summary>
    /// A snapshot that is incomplete for the after window may lack a recurrence, so 100 observed
    /// successes cannot pass the fix.
    /// </summary>
    [TestMethod]
    public void Verify_fix_does_not_pass_on_an_incomplete_scan()
    {
        var incident = Span(TraceId(101), SpanId(101), FixtureUnixNano - 90 * Minute, FixtureUnixNano - 90 * Minute + 1_000_000, status: 2) with { Name = "GET /orders" };
        var successes = Enumerable.Range(1, 100).Select(i =>
        {
            var start = FixtureUnixNano - 50 * Minute + (ulong)i;
            return Span(TraceId(i), SpanId(i), start, start + 1_000_000, status: 1) with { Name = "GET /orders" };
        });
        var snapshot = new AnalysisSnapshot([incident, .. successes], [], [], [], [], [], [], ScanComplete: false);
        var input = JsonNode.Parse($$"""{"since":"{{FixtureUtcNow.AddHours(-1):O}}","matchers":[{"key":"service.name","op":"eq","value":"checkout"}]}""")!.AsObject();

        var verification = new AnalysisEngine(snapshot, "alpha", new AnalysisHandles(new byte[32]), FixtureUtcNow).Run("verify_fix", input);

        Assert.AreEqual(100, verification["after"]!["requests"]!.GetValue<int>());
        Assert.AreEqual("inconclusive", verification["verdict"]!.GetValue<string>());
        Assert.IsFalse(verification["coverage"]!["scan_complete"]!.GetValue<bool>());
    }

    /// <summary>A request after the evaluation instant (clock skew) is outside the after window it is reported in.</summary>
    [TestMethod]
    public async Task Verify_fix_counts_only_requests_inside_the_after_window()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var rows = Enumerable.Range(1, 100).Select(i =>
        {
            var start = FixtureUnixNano - 20 * Minute + (ulong)i;
            return Span(TraceId(i), SpanId(i), start, start + 1_000_000, status: 1);
        }).Append(Span(TraceId(200), SpanId(200), FixtureUnixNano + Minute, FixtureUnixNano + Minute + 1, status: 1));
        await host.Store.EnqueueAsync(new SpanBatch([.. rows]), testContext.CancellationToken);

        var verification = await Read(host, "verify_fix", $$"""
            {"since":"{{FixtureUtcNow.AddMinutes(-40):O}}","end_time":"{{FixtureUtcNow.AddMinutes(5):O}}","matchers":[{"key":"service.name","op":"eq","value":"checkout"}]}
            """);

        Assert.AreEqual(100, verification.GetProperty("after").GetProperty("requests").GetInt64());
        Assert.AreEqual(FixtureUtcNow, verification.GetProperty("after").GetProperty("window").GetProperty("end_time").GetDateTimeOffset());
    }

    /// <summary>
    /// A span id is unique only within its trace. A session whose traces reuse one keeps every span,
    /// relates each span only to its own trace's parent, and names the earliest unexplained error.
    /// </summary>
    [TestMethod]
    public async Task Failure_context_keys_spans_by_trace_and_span_id_across_a_session()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var (first, second) = (TraceId(1), TraceId(2));
        var start = FixtureUnixNano - 20 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(first, SpanId(9), start, start + 10_000_000, sessionId: "workflow", status: 1) with { Name = "checkout" },
            Span(first, SpanId(1), start + 1_000_000, start + 5_000_000, parentSpanId: SpanId(9), sessionId: "workflow", status: 2) with { Name = "charge" },
            Span(second, SpanId(9), start + 10 * Minute, start + 10 * Minute + 5_000_000, sessionId: "workflow", status: 2) with { Name = "checkout" }
        ]), testContext.CancellationToken);

        var observed = (await Read(host, "get_failure_context", """{"session_id":"workflow"}""")).GetProperty("observed");

        var root = observed.GetProperty("root_cause_span");
        Assert.AreEqual((first, SpanId(1)), (root.GetProperty("trace_id").GetString(), root.GetProperty("span_id").GetString()));
        Assert.AreEquivalent(new[] { $"{first}/{SpanId(9)}/ok", $"{first}/{SpanId(1)}/error", $"{second}/{SpanId(9)}/error" },
            observed.GetProperty("spans").EnumerateArray().Select(static span =>
                $"{span.GetProperty("trace_id").GetString()}/{span.GetProperty("span_id").GetString()}/{span.GetProperty("status").GetString()}").ToArray());
        Assert.AreEquivalent(new[] { $"{first}/{SpanId(1)}", $"{second}/{SpanId(9)}" },
            observed.GetProperty("error_spans").EnumerateArray().Select(static span => $"{span.GetProperty("trace_id").GetString()}/{span.GetProperty("span_id").GetString()}").ToArray());
        var propagation = Assert.ContainsSingle(observed.GetProperty("propagation").EnumerateArray());
        Assert.AreEqual((first, SpanId(9)), (propagation.GetProperty("from").GetProperty("trace_id").GetString(), propagation.GetProperty("from").GetProperty("span_id").GetString()));
        Assert.AreEqual((first, SpanId(1)), (propagation.GetProperty("to").GetProperty("trace_id").GetString(), propagation.GetProperty("to").GetProperty("span_id").GetString()));
    }

    /// <summary>
    /// The changes in a failure's context are the regressions of its root cause's service. An execution without an
    /// error span has no root cause, so its context names no changes, whatever other services did meanwhile.
    /// </summary>
    [TestMethod]
    public async Task Failure_context_without_a_root_cause_names_no_changes()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        // billing served 40 charges in the baseline hour and 20 in the window: a throughput regression of billing.
        SpanStorageRow Charge(int id, ulong start) => Span(TraceId(id), SpanId(id), start, start + 1_000_000, service: "billing", status: 1) with { Name = "charge" };
        var rows = Enumerable.Range(1, 40).Select(i => Charge(i, FixtureUnixNano - 90 * Minute + (ulong)i * Second))
            .Concat(Enumerable.Range(41, 20).Select(i => Charge(i, FixtureUnixNano - 30 * Minute + (ulong)i * Second)))
            .Append(Span(TraceId(100), SpanId(100), FixtureUnixNano - 20 * Minute, FixtureUnixNano - 20 * Minute + 1_000_000, status: 1) with { Name = "checkout" });
        await host.Store.EnqueueAsync(new SpanBatch([.. rows]), testContext.CancellationToken);
        Assert.AreEqual("billing", Assert.ContainsSingle((await Read(host, "triage", "{}")).GetProperty("regressions").EnumerateArray()).GetProperty("service").GetString());

        var context = await Read(host, "get_failure_context", $$"""{"trace_id":"{{TraceId(100)}}"}""");

        Assert.IsFalse(context.GetProperty("observed").TryGetProperty("root_cause_span", out var root) && root.ValueKind != JsonValueKind.Null, "a successful execution has no root cause");
        var changes = context.GetProperty("context").TryGetProperty("changes", out var listed) ? listed.EnumerateArray().ToArray() : [];
        Assert.IsEmpty(changes, "without a root cause no service's regressions apply");
    }

    /// <summary>
    /// A failure-id cohort holds the failure's occurrences inside its window: the request's window, its
    /// own window, or for group_b previous_window the equally long window that precedes group_a's.
    /// </summary>
    [TestMethod]
    public async Task Compare_groups_limits_failure_cohorts_to_their_windows()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var minutes = new[] { 90, 80, 20, 210, 190, 170 };
        var rows = minutes.Select((ago, i) =>
        {
            var start = FixtureUnixNano - (ulong)ago * Minute;
            return Span(TraceId(i + 1), SpanId(i + 1), start, start + 1_000_000, status: 2) with { Name = "work", StatusMessage = "boom" };
        }).ToArray();
        await host.Store.EnqueueAsync(new SpanBatch(rows), testContext.CancellationToken);
        var failure = AnalysisEngine.FailureIdentity("alpha", rows[0])!;

        var current = await Read(host, "compare_groups", $$"""{"signal":"spans","group_a":{"failure_id":"{{failure}}"},"group_b":"previous_window"}""");
        Assert.AreEqual((1, 2), (current.GetProperty("size_a").GetInt32(), current.GetProperty("size_b").GetInt32()));

        var window = $$"""{"start_time":"{{FixtureUtcNow.AddMinutes(-200):O}}","end_time":"{{FixtureUtcNow.AddMinutes(-180):O}}"}""";
        var earlier = await Read(host, "compare_groups", $$"""{"signal":"spans","group_a":{"failure_id":"{{failure}}","window":{{window}}},"group_b":"previous_window"}""");
        Assert.AreEqual((1, 1), (earlier.GetProperty("size_a").GetInt32(), earlier.GetProperty("size_b").GetInt32()));
    }
}
