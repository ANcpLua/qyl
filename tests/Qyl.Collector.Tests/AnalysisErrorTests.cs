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

    [TestMethod]
    [DataRow("query_metric", """{"metric_name":"absent.metric"}""", "metric", "absent.metric", "list_metrics")]
    [DataRow("get_trace", """{"trace_id":"0000000000000000000000000000abcd"}""", "trace", "0000000000000000000000000000abcd", "list_traces")]
    [DataRow("list_failures", """{"failure_id":"fail_absent"}""", "failure", "fail_absent", "triage")]
    [DataRow("run_saved_query", """{"id":"sq_absent"}""", "saved_query", "sq_absent", "list_saved_queries")]
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
