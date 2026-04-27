using System.Net;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tool for generating test code from error context.
///     Fetches error details, stack trace, and events, then uses
///     an LLM to generate a regression test that would catch the error.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Loom)]
internal sealed partial class TestGenerationTools(HttpClient http, IQylMcpAgentsBuilder agents)
{

    [McpServerTool(Name = "qyl.generate_test_from_error", Title = "Generate Test from Error",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> GenerateTestAsync(
        string issueId,
        string? framework = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            if (!agents.IsConfigured)
                return "Test generation requires an LLM. Set QYL_AGENT_API_KEY to enable.";

            var targetFramework = framework?.ToLowerInvariant() switch
            {
                "jest" => "TypeScript with Jest",
                _ => "C# with xUnit v3"
            };

            // Fetch error details
            using var issueResp = await http
                .GetAsync($"/api/v1/errors/{Uri.EscapeDataString(issueId)}", ct)
                .ConfigureAwait(false);

            if (issueResp.StatusCode == HttpStatusCode.NotFound)
                return $"Error issue '{issueId}' not found.";

            issueResp.EnsureSuccessStatusCode();
            var issueJson = await issueResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Fetch recent events for stack traces
            using var eventsResp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/events?limit=3", ct)
                .ConfigureAwait(false);

            var eventsJson = eventsResp.IsSuccessStatusCode
                ? await eventsResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : "[]";

            var agent = agents.BuildTestGenerationAgent();

            var userMessage = BuildTestUserMessage(issueJson, eventsJson, targetFramework);

            var response = await agent.RunAsync(userMessage, cancellationToken: ct).ConfigureAwait(false);

            return $"## Generated Test for Issue {issueId}\n\n{response.Text}";
        });

    private static string BuildTestUserMessage(string issueJson, string eventsJson, string framework) =>
        $"""
         Target framework: {framework}

         ## Error Details
         ```json
         {issueJson}
         ```

         ## Recent Events (with stack traces)
         ```json
         {eventsJson}
         ```
         """;
}
