using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying AI agent run telemetry (runs, tool calls, costs).
/// </summary>
[McpServerToolType]
public sealed class AgentTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_agent_runs")]
    [Description("""
                 List AI agent runs with optional filters.

                 Shows agent executions with:
                 - Agent name and model
                 - Status (running, completed, failed)
                 - Token usage and cost
                 - Duration

                 Example queries:
                 - All recent: list_agent_runs()
                 - By agent: list_agent_runs(agentName="copilot")
                 - Failures only: list_agent_runs(status="failed")

                 Returns: Table of agent runs with key metrics
                 """)]
    public Task<string> ListAgentRunsAsync(
        [Description("Maximum runs to return (default: 20)")]
        int limit = 20,
        [Description("Filter by agent name (partial match)")]
        string? agentName = null,
        [Description("Filter by status: 'running', 'completed', 'failed'")]
        string? status = null)
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/agent-runs?limit={limit}";
            if (!string.IsNullOrEmpty(agentName))
                url += $"&agentName={Uri.EscapeDataString(agentName)}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={Uri.EscapeDataString(status)}";

            var response = await client.GetFromJsonAsync<AgentRunsResponse>(
                url, AgentJsonContext.Default.AgentRunsResponse).ConfigureAwait(false);

            if (response?.Runs is null || response.Runs.Count is 0)
                return "No agent runs found matching the criteria.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Agent Runs ({response.Runs.Count} results)");
            sb.AppendLine();
            sb.AppendLine("| Run ID | Agent | Model | Status | Tokens (in/out) | Cost | Duration |");
            sb.AppendLine("|--------|-------|-------|--------|-----------------|------|----------|");

            foreach (var run in response.Runs)
            {
                var statusIcon = run.Status switch
                {
                    "failed" => "❌",
                    "running" => "🔄",
                    _ => "✅"
                };
                var runId = run.RunId.Length > 8 ? run.RunId[..8] : run.RunId;
                var durationStr = run.DurationMs > 0 ? $"{run.DurationMs:F0}ms" : "-";
                var costStr = run.CostUsd > 0 ? $"${run.CostUsd:F4}" : "-";
                var tokensStr = $"{run.InputTokens:N0}/{run.OutputTokens:N0}";

                sb.AppendLine($"| {runId} | {run.AgentName ?? "unknown"} | {run.Model ?? "unknown"} | {statusIcon} {run.Status} | {tokensStr} | {costStr} | {durationStr} |");
            }

            return sb.ToString();
        });
    }

    [McpServerTool(Name = "qyl.get_agent_run")]
    [Description("""
                 Get detailed info for a specific agent run.

                 Returns full details including:
                 - Trace ID for correlation
                 - Model and provider
                 - Token counts and cost
                 - Start/end timing
                 - Error details if failed

                 Returns: Full agent run details
                 """)]
    public Task<string> GetAgentRunAsync(
        [Description("The agent run ID to look up")]
        string runId)
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var run = await client.GetFromJsonAsync<AgentRunDto>(
                $"/api/v1/agent-runs/{Uri.EscapeDataString(runId)}",
                AgentJsonContext.Default.AgentRunDto).ConfigureAwait(false);

            if (run is null)
                return $"Agent run '{runId}' not found.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Agent Run: {run.RunId}");
            sb.AppendLine();
            sb.AppendLine($"- **Agent:** {run.AgentName ?? "unknown"}");
            sb.AppendLine($"- **Model:** {run.Model ?? "unknown"}");
            sb.AppendLine($"- **Status:** {run.Status}");

            if (!string.IsNullOrEmpty(run.TraceId))
                sb.AppendLine($"- **Trace ID:** {run.TraceId}");

            sb.AppendLine($"- **Input tokens:** {run.InputTokens:N0}");
            sb.AppendLine($"- **Output tokens:** {run.OutputTokens:N0}");

            if (run.CostUsd > 0)
                sb.AppendLine($"- **Cost:** ${run.CostUsd:F6}");

            if (run.DurationMs > 0)
                sb.AppendLine($"- **Duration:** {run.DurationMs:F0}ms");

            if (!string.IsNullOrEmpty(run.StartTime))
                sb.AppendLine($"- **Started:** {run.StartTime}");

            if (!string.IsNullOrEmpty(run.EndTime))
                sb.AppendLine($"- **Ended:** {run.EndTime}");

            if (!string.IsNullOrEmpty(run.ErrorMessage))
            {
                sb.AppendLine();
                sb.AppendLine($"### Error");
                sb.AppendLine($"```");
                sb.AppendLine(run.ErrorMessage);
                sb.AppendLine($"```");
            }

            return sb.ToString();
        });
    }

    [McpServerTool(Name = "qyl.get_tool_calls")]
    [Description("""
                 Get tool calls for an agent run.

                 Shows the sequence of tools invoked during an agent run:
                 - Tool name and status
                 - Execution duration
                 - Call sequence order

                 Returns: List of tool calls with details
                 """)]
    public Task<string> GetToolCallsAsync(
        [Description("The agent run ID to get tool calls for")]
        string runId)
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<ToolCallsResponse>(
                $"/api/v1/agent-runs/{Uri.EscapeDataString(runId)}/tools",
                AgentJsonContext.Default.ToolCallsResponse).ConfigureAwait(false);

            if (response?.ToolCalls is null || response.ToolCalls.Count is 0)
                return $"No tool calls found for agent run '{runId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Tool Calls for Run {(runId.Length > 8 ? runId[..8] : runId)} ({response.ToolCalls.Count} calls)");
            sb.AppendLine();
            sb.AppendLine("| # | Tool | Status | Duration |");
            sb.AppendLine("|---|------|--------|----------|");

            foreach (var call in response.ToolCalls)
            {
                var statusIcon = call.Status switch
                {
                    "error" => "❌",
                    _ => "✅"
                };
                var durationStr = call.DurationMs > 0 ? $"{call.DurationMs:F0}ms" : "-";
                sb.AppendLine($"| {call.Sequence} | {call.ToolName ?? "unknown"} | {statusIcon} {call.Status} | {durationStr} |");
            }

            return sb.ToString();
        });
    }
}

#region DTOs

internal sealed record AgentRunsResponse(
    [property: JsonPropertyName("runs")] List<AgentRunDto>? Runs,
    [property: JsonPropertyName("total")] int Total);

internal sealed record AgentRunDto(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("trace_id")] string? TraceId,
    [property: JsonPropertyName("agent_name")] string? AgentName,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("input_tokens")] long InputTokens,
    [property: JsonPropertyName("output_tokens")] long OutputTokens,
    [property: JsonPropertyName("cost_usd")] double CostUsd,
    [property: JsonPropertyName("duration_ms")] double DurationMs,
    [property: JsonPropertyName("start_time")] string? StartTime,
    [property: JsonPropertyName("end_time")] string? EndTime,
    [property: JsonPropertyName("error_message")] string? ErrorMessage);

internal sealed record ToolCallsResponse(
    [property: JsonPropertyName("tool_calls")] List<ToolCallDto>? ToolCalls);

internal sealed record ToolCallDto(
    [property: JsonPropertyName("tool_name")] string? ToolName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("duration_ms")] double DurationMs,
    [property: JsonPropertyName("sequence")] int Sequence);

#endregion

[JsonSerializable(typeof(AgentRunsResponse))]
[JsonSerializable(typeof(AgentRunDto))]
[JsonSerializable(typeof(ToolCallsResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class AgentJsonContext : JsonSerializerContext;
