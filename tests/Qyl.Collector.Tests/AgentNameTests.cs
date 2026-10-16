using System.Text.Json;
using Qyl.Telemetry.SemanticConventions.Names;
using static Qyl.Collector.Tests.OtlpTestSpans;

namespace Qyl.Collector.Tests;

/// <summary>
/// <c>gen_ai.agent.name</c> is a code-level constant of bounded cardinality that MEAI already puts into the
/// span name, so ingest persists it and the readers that name the agent get a value. The agent's id and
/// description stay denied: the id can identify an instance, the description is free text.
/// </summary>
[TestClass]
public sealed class AgentNameTests(TestContext testContext)
{
    [TestMethod]
    public async Task The_agent_name_is_persisted_and_read_by_get_agent_run_and_list_sessions()
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        await host.IngestTracesAsync(Request(QylTelemetryNames.Scopes.QylTelemetryApplication,
        [
            String("gen_ai.operation.name", "invoke_agent"),
            String("gen_ai.agent.name", "planner"),
            String("gen_ai.agent.id", "agent-7f3c"),
            String("gen_ai.agent.description", "Plans the order"),
            String("session.id", "agent-session")
        ], name: "invoke_agent planner"));

        var stored = Assert.ContainsSingle(await host.Store.GetSpansAsync("alpha", ct: testContext.CancellationToken));
        using var attributes = JsonDocument.Parse(stored.AttributesJson ?? "{}");
        Assert.AreEqual("planner", attributes.RootElement.GetProperty("gen_ai.agent.name").GetString());
        Assert.IsFalse(attributes.RootElement.TryGetProperty("gen_ai.agent.id", out _));
        Assert.IsFalse(attributes.RootElement.TryGetProperty("gen_ai.agent.description", out _));

        var run = await ReadAsync(host, "get_agent_run", """{"session_id":"agent-session"}""");
        Assert.AreEqual("planner", run.GetProperty("run").GetProperty("agent_name").GetString());

        var sessions = await ReadAsync(host, "list_sessions", "{}");
        var session = Assert.ContainsSingle(sessions.GetProperty("sessions").EnumerateArray());
        Assert.AreEqual("planner", session.GetProperty("agent_name").GetString());
    }

    private static async Task<JsonElement> ReadAsync(CollectorApiTestHost host, string operation, string input)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/" + operation, "reader", input);
        var json = await host.JsonAsync(response);
        Assert.IsTrue(response.IsSuccessStatusCode, json.GetRawText());
        return json;
    }
}
