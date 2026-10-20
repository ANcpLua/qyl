using System.Net;
using System.Text.Json;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// An error tells the caller what to fix: a rejected argument names its field, the values it accepts and
/// a hint, and a 404 names what is missing and the tool that lists what exists.
/// </summary>
public sealed partial class AnalysisApiTests
{
    [TestMethod]
    public async Task A_rejected_argument_names_its_field_the_values_it_accepts_and_a_hint()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await InsertAsync(host, Gauge("queue.depth", "q_errors", FixtureUnixNano - 10 * Minute), (FixtureUnixNano - 10 * Minute, 1d));

        var aggregation = await RejectedAsync(host, "query_metric", """{"metric_name":"queue.depth","aggregation":"p95"}""");
        Assert.AreEqual("aggregation", aggregation.GetProperty("field").GetString());
        Assert.Contains("avg", aggregation.GetProperty("allowed").EnumerateArray().Select(static v => v.GetString()).ToArray());
        Assert.IsFalse(string.IsNullOrEmpty(aggregation.GetProperty("hint").GetString()));

        var op = await RejectedAsync(host, "list_traces", """{"matchers":[{"key":"service.name","op":"like","value":"x"}]}""");
        Assert.AreEqual("matchers[0].op", op.GetProperty("field").GetString());
        Assert.AreSequenceEqual(["eq", "neq", "in", "prefix", "contains", "exists", "gt", "gte", "lt", "lte"], op.GetProperty("allowed").EnumerateArray().Select(static v => v.GetString()));
    }

    /// <summary>
    /// An argument the tool's contract does not declare, one it requires and leaves out, and a value of another JSON type
    /// or outside the closed set the argument takes are refused naming the field by its path, as the MCP's input schema
    /// names it, never read as the default: a number sent as a string, null, a nested member, an array's element, a
    /// time, an enumeration and a union C# reads as a bare string or object.
    /// </summary>
    [TestMethod]
    [DataRow("triage", """{"lookback":1440}""", "lookback", "lookback.unknown", "is not accepted here; accepted: lookback_minutes, start_time, end_time, matchers, cursor, service_name, environment, limit, baseline, status")]
    [DataRow("triage", """{"lookback_minutes":"1440"}""", "lookback_minutes", "lookback_minutes.type", "must be integer")]
    [DataRow("list_traces", """{"limit":1.5}""", "limit", "limit.type", "must be integer")]
    [DataRow("search_logs", """{"limit":null}""", "limit", "limit.type", "must be integer")]
    [DataRow("list_traces", """{"errors_only":"true"}""", "errors_only", "errors_only.type", "must be boolean")]
    [DataRow("list_traces", """{"service_name":5}""", "service_name", "service_name.type", "must be string")]
    [DataRow("list_traces", """{"matchers":{"key":"service.name"}}""", "matchers", "matchers.type", "must be array")]
    [DataRow("list_traces", """{"matchers":[{"key":"service.name","op":"exists","extra":1}]}""", "matchers[0].extra", "matchers[].extra.unknown", "is not accepted here; accepted: key, op, value, values, scope")]
    [DataRow("list_traces", """{"matchers":[{"key":"service.name","op":"like","value":"x"}]}""", "matchers[0].op", "matchers[].op.invalid", "must be one of: eq, neq, in, prefix, contains, exists, gt, gte, lt, lte")]
    [DataRow("list_traces", """{"matchers":[{"key":"service.name","op":"eq","value":{}}]}""", "matchers[0].value", "matchers[].value.invalid", "must be one of: string, number, boolean")]
    [DataRow("list_traces", """{"start_time":"yesterday"}""", "start_time", "start_time.format", "must match format \"date-time\"")]
    [DataRow("aggregate_telemetry", """{"measures":["count"]}""", "signal", "signal.required", "is required")]
    [DataRow("compare_groups", """{"signal":"spans","group_a":{"window":{"start_time":"2026-10-17T11:00:00Z"}},"group_b":"rest"}""", "group_a.window.end_time", "group_a.window.end_time.required", "is required")]
    [DataRow("compare_groups", """{"signal":"spans","group_a":{},"group_b":"others"}""", "group_b", "group_b.invalid", "must be one of: rest, previous_window, {matchers, window, failure_id}")]
    [DataRow("get_trace", """{"trace_id":"0af7651916cd43dd8448eb211c80319c","selection":"error"}""", "selection", "selection.invalid", "must be one of: all, errors, errors_with_ancestors, critical_path")]
    [DataRow("search_logs", """{"group_by":["patterns"]}""", "group_by[0]", "group_by[].invalid", "must be one of: pattern")]
    [DataRow("triage", """{"status":"opened"}""", "status", "status.invalid", "must be one of: open, resolved, ignored, all")]
    [DataRow("triage", """{"baseline":"last_week"}""", "baseline", "baseline.invalid", "must be one of: previous_window, {start_time, end_time}, {offset_minutes}")]
    [DataRow("query_metric", """{"metric_name":"queue.depth","baseline":{"offset_minutes":"60"}}""", "baseline.offset_minutes", "baseline.offset_minutes.type", "must be number")]
    public async Task An_argument_outside_its_contract_type_is_refused_naming_the_field(string operation, string input, string field, string code, string message)
    {
        await using var host = await StartAsync(testContext.CancellationToken);

        var error = await RejectedAsync(host, operation, input);

        Assert.AreEqual((field, code, message), (error.GetProperty("field").GetString(), error.GetProperty("code").GetString(), error.GetProperty("message").GetString()));
        Assert.IsFalse(string.IsNullOrEmpty(error.GetProperty("hint").GetString()));
    }

    /// <summary>
    /// A string or an array outside the length the contract bounds it to is refused naming the field by its path and
    /// the length it keeps: a matcher's key and values, an aggregate's group_by, search_logs' one pattern group,
    /// ci_log's service_prefix, a trace id's exact length, a metric name that may not be empty and a group's failure id.
    /// </summary>
    [TestMethod]
    [DataRow("list_traces", """{"matchers":[{"key":"","op":"exists"}]}""", "matchers[0].key", "matchers[].key.length", "Use matchers[0].key of 1 to 256 characters.")]
    [DataRow("list_traces", """{"matchers":[{"key":"service.name","op":"in","values":[]}]}""", "matchers[0].values", "matchers[].values.length", "Use matchers[0].values with 1 to 256 items.")]
    [DataRow("aggregate_telemetry", """{"signal":"spans","measures":["count"],"group_by":["a","b","c","d","e"]}""", "group_by", "group_by.length", "Use group_by with at most 4 items.")]
    [DataRow("search_logs", """{"group_by":["pattern","pattern"]}""", "group_by", "group_by.length", "Use group_by with at most 1 item.")]
    [DataRow("ci_log", """{"service_prefix":""}""", "service_prefix", "service_prefix.length", "Use service_prefix of 1 to 256 characters.")]
    [DataRow("get_trace", """{"trace_id":"0af7651916cd43dd8448eb211c80319"}""", "trace_id", "trace_id.length", "Use trace_id of exactly 32 characters.")]
    [DataRow("query_metric", """{"metric_name":""}""", "metric_name", "metric_name.length", "Use metric_name of at least 1 character.")]
    [DataRow("compare_groups", """{"signal":"spans","group_a":{"failure_id":""},"group_b":"rest"}""", "group_a.failure_id", "group_a.failure_id.length", "Use group_a.failure_id of 1 to 512 characters.")]
    public async Task A_string_or_an_array_outside_its_contract_length_is_refused_with_the_length(string operation, string input, string field, string code, string message)
    {
        await using var host = await StartAsync(testContext.CancellationToken);

        var error = await RejectedAsync(host, operation, input);

        Assert.AreEqual((field, code, message, $"Correct {field}."),
            (error.GetProperty("field").GetString(), error.GetProperty("code").GetString(), error.GetProperty("message").GetString(), error.GetProperty("hint").GetString()));
    }

    /// <summary>
    /// A list of matchers keeps 32 at every depth, and a key counts its characters as code points: 256 emoji are 512
    /// UTF-16 units and a key, 257 characters are not.
    /// </summary>
    [TestMethod]
    public async Task Matchers_keep_32_and_a_key_256_characters_counted_as_code_points()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        var matchers = string.Join(",", Enumerable.Repeat("""{"key":"service.name","op":"exists"}""", 33));

        var top = await RejectedAsync(host, "list_traces", $$"""{"matchers":[{{matchers}}]}""");
        var group = await RejectedAsync(host, "compare_groups", $$"""{"signal":"spans","group_a":{"matchers":[{{matchers}}]},"group_b":"rest"}""");
        var key = await RejectedAsync(host, "triage", $$"""{"matchers":[{"key":"{{new string('k', 257)}}","op":"exists"}]}""");

        Assert.AreEqual(("matchers", "Use matchers with at most 32 items."), (top.GetProperty("field").GetString(), top.GetProperty("message").GetString()));
        Assert.AreEqual("group_a.matchers", group.GetProperty("field").GetString());
        Assert.AreEqual("matchers[0].key", key.GetProperty("field").GetString());
        await Read(host, "triage", $$"""{"matchers":[{"key":"{{string.Concat(Enumerable.Repeat("\U0001F600", 256))}}","op":"exists"}]}""");
    }

    /// <summary>An unknown argument is answered with the names the tool accepts, and an enumeration with its values.</summary>
    [TestMethod]
    public async Task A_refusal_lists_the_arguments_or_values_the_field_accepts()
    {
        await using var host = await StartAsync(testContext.CancellationToken);

        var unknown = await RejectedAsync(host, "list_metrics", """{"metric_name":"requests"}""");
        var choice = await RejectedAsync(host, "get_mcp_stats", """{"baseline":7}""");

        Assert.AreSequenceEqual(["query", "service_name", "kind", "unit", "matchers", "lookback_minutes", "start_time", "end_time", "name_prefix", "limit", "cursor"],
            unknown.GetProperty("allowed").EnumerateArray().Select(static name => name.GetString()));
        Assert.AreEqual("Replace metric_name with one of the accepted arguments or drop it.", unknown.GetProperty("hint").GetString());
        Assert.AreSequenceEqual(["previous_window", "{start_time, end_time}", "{offset_minutes}", "{server_version}"],
            choice.GetProperty("allowed").EnumerateArray().Select(static value => value.GetString()));
    }

    /// <summary>
    /// A number outside the range the contract bounds its argument to is refused with the argument, its range and what
    /// to send instead, whichever kind of bound it breaks: the shared window's lookback, a tool's page size, a
    /// collection's own size, a small closed range, a 64-bit step, a raw read's page, a lower bound only the
    /// argument's scalar type declares, a lower bound of 0 with a default and a member of an object the argument holds.
    /// </summary>
    [TestMethod]
    [DataRow("triage", """{"lookback_minutes":0}""", "lookback_minutes", "Use lookback_minutes from 1 to 43200.", "Omit lookback_minutes for the tool's default.")]
    [DataRow("triage", """{"limit":51}""", "limit", "Use limit from 1 to 50.", "Omit limit for 10.")]
    [DataRow("get_trace", """{"trace_id":"0af7651916cd43dd8448eb211c80319c","max_spans":1001}""", "max_spans", "Use max_spans from 1 to 1000.", "Omit max_spans for 100.")]
    [DataRow("get_trace", """{"trace_id":"0af7651916cd43dd8448eb211c80319c","max_logs":-1}""", "max_logs", "Use max_logs from 0 to 50.", "Omit max_logs for the tool's default.")]
    [DataRow("get_service_map", """{"depth":4}""", "depth", "Use depth from 1 to 3.", "Omit depth for the tool's default.")]
    [DataRow("query_metric", """{"metric_name":"queue.depth","step_ms":86400001}""", "step_ms", "Use step_ms from 1 to 86400000.", "Omit step_ms for the tool's default.")]
    [DataRow("query_metric", """{"metric_name":"queue.depth","raw":true,"limit":501}""", "limit", "Use limit from 1 to 500.", "Omit limit for 50.")]
    [DataRow("list_traces", """{"min_duration_ms":-0.5}""", "min_duration_ms", "Use min_duration_ms of at least 0.", "Omit min_duration_ms for the tool's default.")]
    [DataRow("aggregate_telemetry", """{"signal":"spans","measures":["count"],"step_ms":0}""", "step_ms", "Use step_ms from 1 to 86400000.", "Omit step_ms for the tool's default.")]
    [DataRow("aggregate_telemetry", """{"signal":"spans","measures":["count"],"step_ms":9223372036854775807}""", "step_ms", "Use step_ms from 1 to 86400000.", "Omit step_ms for the tool's default.")]
    [DataRow("verify_fix", """{"min_requests":-1}""", "min_requests", "Use min_requests of at least 0.", "Omit min_requests for 100.")]
    [DataRow("verify_fix", """{"min_minutes":-1}""", "min_minutes", "Use min_minutes of at least 0.", "Omit min_minutes for 30.")]
    [DataRow("list_sessions", """{"min_tokens":-1}""", "min_tokens", "Use min_tokens of at least 0.", "Omit min_tokens for the tool's default.")]
    [DataRow("triage", """{"baseline":{"offset_minutes":0}}""", "baseline.offset_minutes", "Use baseline.offset_minutes from 1 to 43200.", "Correct baseline.offset_minutes.")]
    [DataRow("query_metric", """{"metric_name":"queue.depth","baseline":{"offset_minutes":1000000000}}""", "baseline.offset_minutes", "Use baseline.offset_minutes from 1 to 43200.", "Correct baseline.offset_minutes.")]
    [DataRow("get_mcp_stats", """{"baseline":{"offset_minutes":43201}}""", "baseline.offset_minutes", "Use baseline.offset_minutes from 1 to 43200.", "Correct baseline.offset_minutes.")]
    public async Task A_number_outside_its_contract_range_is_refused_with_the_range(string operation, string input, string field, string message, string hint)
    {
        await using var host = await StartAsync(testContext.CancellationToken);

        var error = await RejectedAsync(host, operation, input);

        Assert.AreEqual((field, field + ".out_of_range", message, hint),
            (error.GetProperty("field").GetString(), error.GetProperty("code").GetString(), error.GetProperty("message").GetString(), error.GetProperty("hint").GetString()));
        using var sent = JsonDocument.Parse(input);
        var value = field.Split('.').Aggregate(sent.RootElement, static (member, name) => member.GetProperty(name));
        Assert.AreEqual(value.GetRawText(), error.GetProperty("rejected_value").GetString());
    }

    /// <summary>A range includes both its ends.</summary>
    [TestMethod]
    public async Task A_number_at_either_end_of_its_range_is_accepted()
    {
        await using var host = await StartAsync(testContext.CancellationToken);

        await Read(host, "triage", """{"lookback_minutes":43200,"limit":50}""");
        await Read(host, "search_logs", """{"lookback_minutes":1,"limit":1,"context":10,"severity_min":24}""");
        await Read(host, "list_traces", """{"min_duration_ms":0,"max_duration_ms":0,"limit":100}""");
        await Read(host, "get_service_map", """{"service_name":"checkout","depth":3}""");
        await Read(host, "triage", """{"baseline":{"offset_minutes":1}}""");
        await Read(host, "get_mcp_stats", """{"baseline":{"offset_minutes":43200}}""");
        await Read(host, "aggregate_telemetry", """{"signal":"spans","measures":["count"],"step_ms":1}""");
        await Read(host, "aggregate_telemetry", """{"signal":"logs","measures":["count"],"step_ms":86400000}""");
        await Read(host, "list_sessions", """{"min_tokens":0}""");
        await Read(host, "verify_fix", """{"matchers":[{"key":"service.name","op":"eq","value":"checkout"}],"since":"2026-10-17T11:00:00Z","min_requests":0,"min_minutes":0}""");
    }

    /// <summary>
    /// No telemetry precedes the Unix epoch: a window that starts there compares with a baseline reaching before it,
    /// the window just before or one 30 days back, and that baseline holds nothing, so the window's failure is new and
    /// a group B over the window before is empty.
    /// </summary>
    [TestMethod]
    public async Task A_baseline_reaching_back_before_the_unix_epoch_holds_nothing()
    {
        await using var host = await StartAsync(testContext.CancellationToken);
        await host.Store.EnqueueAsync(new SpanBatch([Span(TraceId(1), SpanId(1), Minute, 2 * Minute, status: 2)]), testContext.CancellationToken);

        foreach (var input in new[] {
            """{"start_time":"1970-01-01T00:00:00Z","end_time":"1970-01-01T01:00:00Z"}""",
            """{"start_time":"1970-01-01T00:00:00Z","end_time":"1970-01-01T01:00:00Z","baseline":{"offset_minutes":43200}}""" })
        {
            var failure = Assert.ContainsSingle((await Read(host, "triage", input)).GetProperty("failures").EnumerateArray());
            Assert.AreEqual(("new", 0), (failure.GetProperty("change").GetString(), failure.GetProperty("baseline_count").GetInt32()));
        }
        await Read(host, "check_telemetry_health", """{"start_time":"1970-01-01T00:00:00Z","end_time":"1970-01-01T01:00:00Z"}""");
        await Read(host, "get_mcp_stats", """{"start_time":"1970-01-01T00:00:00Z","end_time":"1970-01-01T01:00:00Z","baseline":{"offset_minutes":120}}""");
        var empty = await RejectedAsync(host, "compare_groups", """{"start_time":"1970-01-01T00:00:00Z","end_time":"1970-01-01T01:00:00Z","signal":"spans","group_a":{"window":{"start_time":"1970-01-01T00:00:00Z","end_time":"1970-01-01T01:00:00Z"}},"group_b":"previous_window"}""");
        Assert.AreEqual(("group_b", "group.empty"), (empty.GetProperty("field").GetString(), empty.GetProperty("code").GetString()));
    }

    [TestMethod]
    [DataRow("query_metric", """{"metric_name":"absent.metric"}""", "metric", "absent.metric", "list_metrics")]
    [DataRow("get_trace", """{"trace_id":"0000000000000000000000000000abcd"}""", "trace", "0000000000000000000000000000abcd", "list_traces")]
    [DataRow("get_failure_context", """{"failure_id":"fail_absent"}""", "failure", "fail_absent", "triage")]
    [DataRow("run_saved_query", """{"id":"sq_absent"}""", "saved_query", "sq_absent", "run_saved_query without id")]
    public async Task A_not_found_names_what_is_missing_and_the_tool_that_lists_it(string operation, string input, string type, string id, string tool)
    {
        await using var host = await StartAsync(testContext.CancellationToken);

        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + operation, ReaderApiKey, input);
        var json = await host.JsonAsync(response);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, json.GetRawText());
        Assert.AreEqual((type, id), (json.GetProperty("resource_type").GetString(), json.GetProperty("resource_id").GetString()));
        StringAssert.Contains(json.GetProperty("detail").GetString(), tool);
        StringAssert.Contains(json.GetProperty("detail").GetString(), id);
    }

    private static async Task<JsonElement> RejectedAsync(CollectorApiTestHost host, string operation, string input)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + operation, ReaderApiKey, input);
        var json = await host.JsonAsync(response);
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, json.GetRawText());
        return Assert.ContainsSingle(json.GetProperty("errors").EnumerateArray());
    }
}
