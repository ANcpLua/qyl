using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tool that delegates natural language observability questions to an embedded LLM agent.
///     The agent has access to search_spans, get_trace, get_genai_stats, search_logs,
///     get_storage_stats, list_sessions, and get_system_context tools internally.
///
///     Follows the Sentry MCP "Agent-in-Tool" pattern: the calling agent (Claude/Cursor)
///     sends a natural language query, the embedded agent translates it into structured
///     observability queries, and returns the results.
/// </summary>
[McpServerToolType]
internal sealed class InvestigateTools(IAgentProvider agent)
{
    [McpServerTool(Name = "qyl.investigate")]
    [Description("""
                 Investigate observability data using natural language.

                 Translates your question into the right queries across
                 spans, traces, logs, GenAI usage, and system health.

                 The embedded agent has access to search_spans, get_trace,
                 get_genai_stats, search_logs, get_storage_stats, list_sessions,
                 and get_system_context tools internally.

                 Examples:
                 - "What errors happened in the last hour?"
                 - "Show me the slowest GenAI calls today"
                 - "How much did we spend on Claude vs GPT this week?"
                 - "Trace the request that failed at 3pm"
                 - "What's the error rate for the api-gateway service?"
                 - "Summarize system health"

                 Returns: Investigation results from observability data
                 """)]
    public Task<string> InvestigateAsync(
        [Description("Natural language question about your observability data")]
        string question,
        [Description("Additional context: service name, time range hint, session ID, etc.")]
        string? context = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            if (!agent.IsAvailable)
                return "Investigation agent unavailable. No LLM configured on the collector. " +
                       "Use the simple query tools (search_spans, list_errors, get_genai_stats) instead, " +
                       "or configure an LLM provider via QYL_LLM_PROVIDER.";

            var contentBuilder = new StringBuilder();
            var toolTrace = new StringBuilder();
            long? outputTokens = null;
            string? error = null;

            await foreach (var chunk in agent.InvestigateAsync(
                question,
                ObservabilitySystemPrompt.Prompt,
                context).ConfigureAwait(false))
            {
                if (chunk.Content is not null)
                {
                    contentBuilder.Append(chunk.Content);
                }
                else if (chunk.ToolName is not null && chunk.ToolResult is null)
                {
                    // Tool call event — record for trace
                    toolTrace.AppendLine($"- Called: {chunk.ToolName}");
                }
                else if (chunk.ToolName is not null && chunk.ToolResult is not null)
                {
                    // Tool result — record for trace
                    toolTrace.AppendLine($"  Result: {Truncate(chunk.ToolResult, 200)}");
                }
                else if (chunk.Error is not null)
                {
                    error = chunk.Error;
                }
                else if (chunk.IsCompleted)
                {
                    outputTokens = chunk.OutputTokens;
                }
            }

            if (error is not null)
                return $"Investigation error: {error}";

            var result = contentBuilder.ToString();
            if (string.IsNullOrWhiteSpace(result))
                return "Investigation completed with no output. Try rephrasing your question.";

            // Append tool trace and token info as metadata
            if (toolTrace.Length > 0)
            {
                result += "\n\n---\n<details><summary>Tool calls</summary>\n\n";
                result += toolTrace.ToString();
                result += "</details>";
            }

            if (outputTokens > 0)
                result += $"\n\n*Tokens: {outputTokens}*";

            return result;
        });

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}
