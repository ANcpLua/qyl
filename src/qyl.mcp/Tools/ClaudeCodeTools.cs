using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying Claude Code session telemetry via the qyl collector.
/// </summary>
[McpServerToolType]
public sealed class ClaudeCodeTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.claude_code_sessions")]
    [Description("""
                 List Claude Code sessions with usage statistics.

                 Shows all Claude Code CLI sessions captured via OTLP telemetry,
                 including prompt counts, tool calls, API calls, token usage, and costs.

                 Requires CLAUDE_CODE_ENABLE_TELEMETRY=1 on the Claude Code CLI
                 with OTLP exporter pointed at the qyl collector.

                 Example queries:
                 - Recent sessions: claude_code_sessions()
                 - Last 10: claude_code_sessions(limit=10)

                 Returns: Session list with summary stats table
                 """)]
    public Task<string> GetClaudeCodeSessionsAsync(
        [Description("Maximum sessions to return (default: 20)")]
        int limit = 20) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<ClaudeCodeSessionsDto>(
                $"/api/v1/claude-code/sessions?limit={limit}",
                ClaudeCodeMcpJsonContext.Default.ClaudeCodeSessionsDto).ConfigureAwait(false);

            if (response?.Sessions is null || response.Sessions.Count is 0)
                return "No Claude Code sessions found. Ensure CLAUDE_CODE_ENABLE_TELEMETRY=1 is set and OTLP is configured.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Claude Code Sessions ({response.Total} total)");
            sb.AppendLine();
            sb.AppendLine("| Session | Start | Prompts | Tools | API Calls | Cost | Tokens (in/out) | Models |");
            sb.AppendLine("|---------|-------|---------|-------|-----------|------|-----------------|--------|");

            foreach (var s in response.Sessions)
            {
                var models = s.Models is { Count: > 0 } ? string.Join(", ", s.Models) : "-";
                var sessionShort = s.SessionId.Length > 12 ? s.SessionId[..12] + "..." : s.SessionId;
                sb.AppendLine(
                    $"| {sessionShort} | {s.StartTime:MM-dd HH:mm} | {s.TotalPrompts} | {s.TotalToolCalls} | {s.TotalApiCalls} | ${s.TotalCostUsd:F4} | {s.TotalInputTokens:N0}/{s.TotalOutputTokens:N0} | {models} |");
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.claude_code_timeline")]
    [Description("""
                 Get the event timeline for a Claude Code session.

                 Shows all events in chronological order, grouped by prompt.
                 Events include: prompts, API calls, tool uses, tool results, errors.

                 Use claude_code_sessions first to find session IDs.

                 Example: claude_code_timeline(session_id="abc123")

                 Returns: Chronological event timeline with prompt grouping
                 """)]
    public Task<string> GetClaudeCodeTimelineAsync(
        [Description("The session ID to get timeline for")]
        string sessionId) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<ClaudeCodeTimelineDto>(
                $"/api/v1/claude-code/sessions/{Uri.EscapeDataString(sessionId)}/timeline",
                ClaudeCodeMcpJsonContext.Default.ClaudeCodeTimelineDto).ConfigureAwait(false);

            if (response?.Events is null || response.Events.Count is 0)
                return $"No events found for session {sessionId}.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Claude Code Timeline — {sessionId}");
            sb.AppendLine($"*{response.Total} events*");
            sb.AppendLine();

            string? currentPromptId = null;
            foreach (var e in response.Events)
            {
                if (e.PromptId != currentPromptId)
                {
                    currentPromptId = e.PromptId;
                    if (currentPromptId is not null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"### Prompt {currentPromptId}");
                    }
                }

                var time = e.Timestamp.ToString("HH:mm:ss");
                var details = new List<string>();

                if (e.Model is not null) details.Add($"model={e.Model}");
                if (e.ToolName is not null) details.Add($"tool={e.ToolName}");
                if (e.DurationMs.HasValue) details.Add($"{e.DurationMs:F0}ms");
                if (e.CostUsd is > 0) details.Add($"${e.CostUsd:F6}");
                if (e.InputTokens.HasValue || e.OutputTokens.HasValue)
                    details.Add($"{e.InputTokens ?? 0}in/{e.OutputTokens ?? 0}out");
                if (e.Success == false) details.Add("FAILED");
                if (e.Error is not null) details.Add($"error: {e.Error}");
                if (e.Decision is not null) details.Add($"decision={e.Decision}");

                var detailStr = details.Count > 0 ? $" ({string.Join(", ", details)})" : "";
                sb.AppendLine($"- **{time}** `{e.EventName}`{detailStr}");
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.claude_code_tools")]
    [Description("""
                 Get tool usage breakdown for a Claude Code session.

                 Shows which tools were called, how often, success/failure rates,
                 average duration, and accept/reject counts.

                 Use claude_code_sessions first to find session IDs.

                 Example: claude_code_tools(session_id="abc123")

                 Returns: Tool usage summary table
                 """)]
    public Task<string> GetClaudeCodeToolsAsync(
        [Description("The session ID to get tool usage for")]
        string sessionId) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<ClaudeCodeToolSummaryDto>(
                $"/api/v1/claude-code/sessions/{Uri.EscapeDataString(sessionId)}/tools",
                ClaudeCodeMcpJsonContext.Default.ClaudeCodeToolSummaryDto).ConfigureAwait(false);

            if (response?.Tools is null || response.Tools.Count is 0)
                return $"No tool usage found for session {sessionId}.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Claude Code Tool Usage — {sessionId}");
            sb.AppendLine();
            sb.AppendLine("| Tool | Calls | Success | Failed | Avg Duration | Accept | Reject |");
            sb.AppendLine("|------|-------|---------|--------|--------------|--------|--------|");

            foreach (var t in response.Tools)
            {
                sb.AppendLine(
                    $"| {t.ToolName} | {t.CallCount} | {t.SuccessCount} | {t.FailureCount} | {t.AvgDurationMs:F0}ms | {t.AcceptCount} | {t.RejectCount} |");
            }

            sb.AppendLine();
            sb.AppendLine($"**Total:** {response.Tools.Sum(static t => t.CallCount)} calls across {response.Tools.Count} tools");

            return sb.ToString();
        });
}

#region DTOs

internal sealed record ClaudeCodeSessionsDto
{
    [JsonPropertyName("sessions")] public IReadOnlyList<ClaudeCodeSessionDto>? Sessions { get; init; }
    [JsonPropertyName("total")] public int Total { get; init; }
}

internal sealed record ClaudeCodeSessionDto
{
    [JsonPropertyName("sessionId")] public required string SessionId { get; init; }
    [JsonPropertyName("startTime")] public DateTimeOffset StartTime { get; init; }
    [JsonPropertyName("lastActivityTime")] public DateTimeOffset LastActivityTime { get; init; }
    [JsonPropertyName("totalPrompts")] public int TotalPrompts { get; init; }
    [JsonPropertyName("totalApiCalls")] public int TotalApiCalls { get; init; }
    [JsonPropertyName("totalToolCalls")] public int TotalToolCalls { get; init; }
    [JsonPropertyName("totalCostUsd")] public double TotalCostUsd { get; init; }
    [JsonPropertyName("totalInputTokens")] public long TotalInputTokens { get; init; }
    [JsonPropertyName("totalOutputTokens")] public long TotalOutputTokens { get; init; }
    [JsonPropertyName("models")] public IReadOnlyList<string>? Models { get; init; }
    [JsonPropertyName("terminalType")] public string? TerminalType { get; init; }
    [JsonPropertyName("claudeCodeVersion")] public string? ClaudeCodeVersion { get; init; }
}

internal sealed record ClaudeCodeTimelineDto
{
    [JsonPropertyName("sessionId")] public required string SessionId { get; init; }
    [JsonPropertyName("events")] public IReadOnlyList<ClaudeCodeEventDto>? Events { get; init; }
    [JsonPropertyName("total")] public int Total { get; init; }
}

internal sealed record ClaudeCodeEventDto
{
    [JsonPropertyName("eventName")] public required string EventName { get; init; }
    [JsonPropertyName("promptId")] public string? PromptId { get; init; }
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; init; }
    [JsonPropertyName("toolName")] public string? ToolName { get; init; }
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("costUsd")] public double? CostUsd { get; init; }
    [JsonPropertyName("durationMs")] public double? DurationMs { get; init; }
    [JsonPropertyName("inputTokens")] public long? InputTokens { get; init; }
    [JsonPropertyName("outputTokens")] public long? OutputTokens { get; init; }
    [JsonPropertyName("success")] public bool? Success { get; init; }
    [JsonPropertyName("decision")] public string? Decision { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("promptLength")] public int? PromptLength { get; init; }
}

internal sealed record ClaudeCodeToolSummaryDto
{
    [JsonPropertyName("sessionId")] public required string SessionId { get; init; }
    [JsonPropertyName("tools")] public IReadOnlyList<ClaudeCodeToolDto>? Tools { get; init; }
    [JsonPropertyName("total")] public int Total { get; init; }
}

internal sealed record ClaudeCodeToolDto
{
    [JsonPropertyName("toolName")] public required string ToolName { get; init; }
    [JsonPropertyName("callCount")] public int CallCount { get; init; }
    [JsonPropertyName("successCount")] public int SuccessCount { get; init; }
    [JsonPropertyName("failureCount")] public int FailureCount { get; init; }
    [JsonPropertyName("avgDurationMs")] public double AvgDurationMs { get; init; }
    [JsonPropertyName("acceptCount")] public int AcceptCount { get; init; }
    [JsonPropertyName("rejectCount")] public int RejectCount { get; init; }
}

#endregion

[JsonSerializable(typeof(ClaudeCodeSessionsDto))]
[JsonSerializable(typeof(ClaudeCodeTimelineDto))]
[JsonSerializable(typeof(ClaudeCodeToolSummaryDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class ClaudeCodeMcpJsonContext : JsonSerializerContext;
