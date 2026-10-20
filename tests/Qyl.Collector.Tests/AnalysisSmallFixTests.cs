using System.Net;
using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// The tool review's smaller findings: answers say nothing circular or empty, name what is missing,
/// and carry numbers an agent can read.
/// </summary>
public sealed partial class AnalysisApiTests
{
    /// <summary>
    /// A failure cohort is defined by its error type, so that type cannot discriminate it, and an attribute
    /// both groups share equally (lift 1) explains nothing. An empty group is an error, not a change.
    /// </summary>
    [TestMethod]
    public async Task Compare_groups_leaves_out_defining_and_non_lifting_keys_and_rejects_an_empty_group()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            .. Enumerable.Range(1, 10).Select(i => Span(TraceId(i), SpanId(i), time + (ulong)i, time + (ulong)i + 1000, status: i <= 5 ? (byte)2 : (byte)1)
                with { Name = "request", AttributesJson = i <= 5 ? """{"error.type":"timeout","region":"eu"}""" : """{"region":"us"}""" })
        ]), testContext.CancellationToken);
        var failure = Assert.ContainsSingle((await Read(host, "triage", "{}")).GetProperty("failures").EnumerateArray()).GetProperty("failure_id").GetString();

        var compared = await Read(host, "compare_groups", $$"""{"signal":"spans","group_a":{"failure_id":"{{failure}}"},"group_b":"rest","attribute_scope":"both"}""");

        var keys = compared.GetProperty("discriminators").EnumerateArray().Select(static d => d.GetProperty("key").GetString()).ToArray();
        Assert.AreSequenceEqual(["region"], keys.Distinct());
        Assert.IsTrue(compared.GetProperty("discriminators").EnumerateArray().All(static d => d.GetProperty("lift").ValueKind == JsonValueKind.Null || d.GetProperty("lift").GetDouble() > 1));

        using var empty = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/compare_groups", ReaderApiKey,
            """{"signal":"spans","group_a":{"matchers":[{"key":"region","op":"eq","value":"mars"}]},"group_b":"rest"}""");
        var error = Assert.ContainsSingle((await host.JsonAsync(empty)).GetProperty("errors").EnumerateArray());
        Assert.AreEqual(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.AreEqual(("group_a", "group.empty"), (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()));
    }

    /// <summary>
    /// A group_by key no record carries is a typo, not one group of everything; order_by names one of the
    /// requested measures; and records without the key form a group whose key is null, not "".
    /// </summary>
    [TestMethod]
    public async Task Aggregate_rejects_an_unrecorded_key_and_an_unknown_order_and_labels_a_missing_value_null()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000) with { AttributesJson = """{"http.route":"/checkout"}""" },
            Span(TraceId(2), SpanId(2), time, time + 1000)
        ]), testContext.CancellationToken);

        var typo = await RejectedAsync(host, "aggregate_telemetry", """{"signal":"spans","measures":["count"],"group_by":["http_route"]}""");
        Assert.AreEqual(("group_by", "group_by.unrecorded"), (typo.GetProperty("field").GetString(), typo.GetProperty("code").GetString()));
        StringAssert.Contains(typo.GetProperty("hint").GetString(), "discover_attributes");
        var order = await RejectedAsync(host, "aggregate_telemetry", """{"signal":"spans","measures":["count"],"order_by":"p95"}""");
        Assert.AreEqual("order_by", order.GetProperty("field").GetString());
        Assert.Contains("count", order.GetProperty("allowed").EnumerateArray().Select(static v => v.GetString()).ToArray());

        var groups = (await Read(host, "aggregate_telemetry", """{"signal":"spans","measures":["count"],"group_by":["http.route"]}""")).GetProperty("groups").EnumerateArray().ToArray();
        Assert.ContainsSingle(groups.Where(static g => g.GetProperty("key").GetProperty("http.route").ValueKind == JsonValueKind.Null));
        Assert.ContainsSingle(groups.Where(static g => g.GetProperty("key").GetProperty("http.route").ValueKind == JsonValueKind.String));
    }

    /// <summary>slowest_steps names steps by index, unknown token counts are absent, and an unknown session is not found.</summary>
    [TestMethod]
    public async Task Agent_run_names_slowest_steps_by_index_omits_unknown_tokens_and_finds_no_unknown_session()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 5_000_000, sessionId: "run") with { Name = "execute_tool search", AttributesJson = """{"gen_ai.tool.name":"search"}""" },
            Span(TraceId(1), SpanId(2), time + 10, time + 1_000_010, sessionId: "run") with { Name = "chat", GenAiOperationName = "chat", GenAiInputTokens = 10 }
        ]), testContext.CancellationToken);

        var run = await Read(host, "get_agent_run", """{"session_id":"run"}""");

        Assert.AreSequenceEqual([0, 1], run.GetProperty("findings").GetProperty("slowest_steps").EnumerateArray().Select(static s => s.GetInt32()));
        var tool = run.GetProperty("steps")[0].GetProperty("tokens");
        Assert.IsEmpty(tool.EnumerateObject());
        Assert.AreSequenceEqual(["input"], run.GetProperty("steps")[1].GetProperty("tokens").EnumerateObject().Select(static p => p.Name));
        using var missing = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/get_agent_run", ReaderApiKey, """{"session_id":"absent"}""");
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
        StringAssert.Contains((await host.JsonAsync(missing)).GetProperty("detail").GetString(), "list_sessions");
    }

    /// <summary>An unknown CI run is not found, and the answer names the runs that exist.</summary>
    [TestMethod]
    public async Task Ci_log_of_an_unknown_run_names_the_recent_runs()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 1000, service: "qyl-ci-tests", sessionId: "run-1") with { AttributesJson = """{"cicd.pipeline.task.name":"build"}""" }
        ]), testContext.CancellationToken);

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/ci_log", ReaderApiKey, """{"ci_run_id":"run-9"}""");
        var json = await host.JsonAsync(response);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, json.GetRawText());
        StringAssert.Contains(json.GetProperty("detail").GetString(), "run-1");
    }

    /// <summary>Durations come rounded to the microsecond, and an environment that was never recorded is absent.</summary>
    [TestMethod]
    public async Task Service_map_and_releases_round_durations_and_omit_an_unrecorded_environment()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            .. new ulong[] { 270, 100, 100 }.SelectMany((ms, i) => new[]
            {
                Span(TraceId(i + 1), SpanId(i + 1), time, time + 400 * 1_000_000UL),
                Span(TraceId(i + 1), SpanId(i + 10), time + 1, time + 1 + ms * 1_000_000, parentSpanId: SpanId(i + 1), service: "payments") with { ResourceJson = """{"service.version":"v1"}""" }
            })
        ]), testContext.CancellationToken);

        var edge = Assert.ContainsSingle((await Read(host, "get_service_map", "{}")).GetProperty("edges").EnumerateArray());
        Assert.AreEqual("253", edge.GetProperty("p95_ms").GetRawText());
        var release = Assert.ContainsSingle((await Read(host, "list_releases", "{}")).GetProperty("releases").EnumerateArray());
        Assert.IsFalse(release.TryGetProperty("environment", out _));
        Assert.AreEqual("253", release.GetProperty("after").GetProperty("p95_ms").GetRawText());
    }

    private static readonly string[] s_verificationFields = ["coverage", "page_totals", "has_more", "verdict", "change", "before", "after", "needs", "recurrences", "guardrails", "verify_id"];
    private static readonly string[] s_ciRunFields = ["ci_run_id", "state", "start_time", "duration_ms", "error_count", "services", "attempts"];
    private static readonly string[] s_plainSessionFields =
        ["session_id", "state", "start_time", "end_time", "duration_ms", "trace_count", "span_count", "error_count", "services", "failure_ids"];
    private static readonly string[] s_pageFields = ["window", "coverage", "has_more"];

    /// <summary>
    /// Answers leave out what is empty or said twice: an evidence span says its status once and carries an exception
    /// type only when one was recorded, a release that was never recorded is no empty string, a verification's windows
    /// are its own, a failed tool call is an index into the steps, a CI run is named once, a session without GenAI
    /// requests has no GenAI block and no answer says it is live.
    /// </summary>
    [TestMethod]
    public async Task Answers_leave_out_empty_and_repeated_fields()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var time = FixtureUnixNano - 30 * Minute;
        await host.Store.EnqueueAsync(new SpanBatch([
            Span(TraceId(1), SpanId(1), time, time + 50_000_000, status: 1, sessionId: "plain") with { Name = "checkout" },
            Span(TraceId(1), SpanId(2), time + 10, time + 1_000_000, parentSpanId: SpanId(1), service: "payments", status: 2, sessionId: "plain")
                with { Name = "charge", StatusMessage = "declined", AttributesJson = """{"error.type":"card"}""" },
            Span(TraceId(1), SpanId(3), time + 2_000_000, time + 3_000_000, parentSpanId: SpanId(1), status: 2, sessionId: "plain") with { Name = "render" },
            Span(TraceId(2), SpanId(4), time, time + 5_000_000, status: 2, sessionId: "agent")
                with { Name = "execute_tool search", StatusMessage = "tool failed", AttributesJson = """{"gen_ai.tool.name":"search"}""" },
            Span(TraceId(5), SpanId(5), time, time + 1000, service: "qyl-ci-tests", sessionId: "ci-run") with { Name = "build", AttributesJson = """{"cicd.pipeline.task.name":"build"}""" }
        ]), testContext.CancellationToken);

        var context = await Read(host, "get_failure_context", $$"""{"trace_id":"{{TraceId(1)}}"}""");
        var spans = context.GetProperty("observed").GetProperty("spans").EnumerateArray().ToDictionary(static s => s.GetProperty("span_id").GetString()!);
        var charge = spans[SpanId(2)];
        Assert.AreEqual(("error", "declined", "card"), (charge.GetProperty("status").GetString(), charge.GetProperty("status_message").GetString(), charge.GetProperty("exception_type").GetString()));
        static string[] Fields(JsonElement element) => [.. element.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal)];
        static string[] Sorted(IEnumerable<string> names) => [.. names.Order(StringComparer.Ordinal)];
        string[] span = ["span_id", "trace_id", "session_id", "service", "name", "start_time", "duration_ms", "status", "key_attributes"];
        Assert.AreSequenceEqual(Sorted(span.Concat(["status_message", "parent_span_id", "exception_type"])), Fields(charge));
        Assert.AreEqual("ok", spans[SpanId(1)].GetProperty("status").GetString());
        Assert.AreSequenceEqual(Sorted(span), Fields(spans[SpanId(1)]), "a root without a message or an error type says nothing empty");
        var similar = Assert.ContainsSingle(context.GetProperty("context").GetProperty("similar_failures").EnumerateArray());
        Assert.IsEmpty(similar.GetProperty("releases").EnumerateArray(), "no service.version was recorded, so no release is named");

        var verification = await Read(host, "verify_fix", $$"""{"matchers":[{"key":"service.name","op":"eq","value":"payments"}],"since":"{{FixtureUtcNow.AddMinutes(-10):O}}"}""");
        Assert.AreSequenceEqual(Sorted(s_verificationFields), Fields(verification), "the before and after windows bound a verification");

        var run = await Read(host, "get_agent_run", """{"session_id":"agent"}""");
        Assert.AreSequenceEqual([0], run.GetProperty("findings").GetProperty("failed_tool_calls").EnumerateArray().Select(static index => index.GetInt32()));
        Assert.AreEqual("error", run.GetProperty("steps")[0].GetProperty("status").GetString());

        var ci = Assert.ContainsSingle((await Read(host, "ci_log", "{}")).GetProperty("runs").EnumerateArray());
        Assert.AreEqual("ci-run", ci.GetProperty("ci_run_id").GetString());
        Assert.AreSequenceEqual(Sorted(s_ciRunFields), Fields(ci), "the run's id is its session id");

        var sessions = await Read(host, "list_sessions", "{}");
        var session = sessions.GetProperty("sessions").EnumerateArray().Single(static s => s.GetProperty("session_id").GetString() == "plain");
        Assert.AreSequenceEqual(Sorted(s_plainSessionFields), Fields(session), "a session without GenAI requests or notes carries no empty block");
        foreach (var (operation, rows) in new[] { ("list_sessions", "sessions"), ("list_traces", "traces"), ("search_logs", "logs") })
            Assert.AreSequenceEqual(Sorted(s_pageFields.Append(rows)), Fields(await Read(host, operation, "{}")), operation);
    }
}
