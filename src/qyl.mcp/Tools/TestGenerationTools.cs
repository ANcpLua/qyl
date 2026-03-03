using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tool for generating test code from error context.
///     Fetches error details, stack trace, and events, then uses
///     an LLM to generate a regression test that would catch the error.
/// </summary>
[McpServerToolType]
internal sealed class TestGenerationTools(HttpClient http, IConfiguration config)
{
    private readonly IChatClient? _llm = AgentLlmFactory.TryCreate(config);

    [McpServerTool(Name = "qyl.generate_test_from_error", Title = "Generate Test from Error",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Generate a regression test based on an error issue.
                 Fetches the error's stack trace, context, and events,
                 then uses an LLM to produce test code that would detect
                 if this error reoccurs. Supports C#/xUnit and TypeScript/Jest.
                 """)]
    public async Task<string> GenerateTestAsync(
        [Description("The error issue ID")] string issueId,
        [Description("Target test framework: 'xunit' (default) or 'jest'")] string? framework = null,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            if (_llm is null)
                return "Test generation requires an LLM. Set QYL_AGENT_API_KEY to enable.";

            string targetFramework = framework?.ToLowerInvariant() switch
            {
                "jest" => "TypeScript with Jest",
                _ => "C# with xUnit v3"
            };

            // Fetch error details
            using HttpResponseMessage issueResp = await http
                .GetAsync($"/api/v1/errors/{Uri.EscapeDataString(issueId)}", ct)
                .ConfigureAwait(false);

            if (issueResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"Error issue '{issueId}' not found.";

            issueResp.EnsureSuccessStatusCode();
            string issueJson = await issueResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Fetch recent events for stack traces
            using HttpResponseMessage eventsResp = await http
                .GetAsync($"/api/v1/errors/{Uri.EscapeDataString(issueId)}/events?limit=3", ct)
                .ConfigureAwait(false);

            string eventsJson = eventsResp.IsSuccessStatusCode
                ? await eventsResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : "[]";

            string prompt = BuildTestPrompt(issueJson, eventsJson, targetFramework);

            ChatResponse response = await _llm.GetResponseAsync(prompt, cancellationToken: ct)
                .ConfigureAwait(false);

            return $"## Generated Test for Issue {issueId}\n\n{response.Text}";
        });

    private static string BuildTestPrompt(string issueJson, string eventsJson, string framework) =>
        $$"""
        You are a test engineer. Generate a regression test that would catch this error if it reoccurs.

        ## Error Details
        ```json
        {{issueJson}}
        ```

        ## Recent Events (with stack traces)
        ```json
        {{eventsJson}}
        ```

        ## Requirements
        - Write the test in {{framework}}
        - The test should verify the specific behavior that caused this error
        - Include arrange/act/assert structure
        - Add comments explaining what the test validates
        - If the error is in a specific method, test that method's edge cases
        - Include setup code (mocks, fixtures) as needed
        - Output the complete test file in a code block
        """;
}
