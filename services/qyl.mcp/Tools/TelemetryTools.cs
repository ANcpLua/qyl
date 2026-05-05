using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
internal sealed partial class TelemetryTools(ITelemetryStore store)
{
    [QylCapability("genai_observability", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.search_agent_runs", Title = "Search Agent Runs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> SearchAgentRunsAsync(
        string? provider = null,
        string? model = null,
        string? errorType = null,
        DateTime? since = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await store.SearchRunsAsync(provider, model, errorType, since)
                .ConfigureAwait(false);
            return result.Length is 0
                ? "No agent runs found."
                : JsonSerializer.Serialize(result, TelemetryToolsJsonContext.Default.AgentRunArray);
        });

    [QylCapability("genai_observability", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.get_agent_run", Title = "Get Agent Run",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> GetAgentRunAsync(
        string runId) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await store.GetRunAsync(runId).ConfigureAwait(false);
            return result is null
                ? $"Agent run '{runId}' not found."
                : JsonSerializer.Serialize(result, TelemetryToolsJsonContext.Default.AgentRun);
        });

    [QylCapability("genai_observability", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.get_token_usage", Title = "Get Token Usage",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> GetTokenUsageAsync(
        DateTime? since = null,
        DateTime? until = null,
        string groupBy = "agent") =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await store.GetTokenUsageAsync(since, until, groupBy)
                .ConfigureAwait(false);
            return result.Length is 0
                ? "No token usage data available."
                : JsonSerializer.Serialize(result, TelemetryToolsJsonContext.Default.TokenUsageSummaryArray);
        });

    [McpServerTool(Name = "qyl.list_errors", Title = "List Errors",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> ListErrorsAsync(
        int limit = 50,
        string? agentName = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await store.ListErrorsAsync(limit, agentName)
                .ConfigureAwait(false);
            return result.Length is 0
                ? "No errors found."
                : JsonSerializer.Serialize(result, TelemetryToolsJsonContext.Default.AgentErrorArray);
        });

    [McpServerTool(Name = "qyl.get_latency_stats", Title = "Get Latency Stats",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> GetLatencyStatsAsync(
        string? agentName = null,
        int hours = 24) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await store.GetLatencyStatsAsync(agentName, hours)
                .ConfigureAwait(false);
            return JsonSerializer.Serialize(result, TelemetryToolsJsonContext.Default.LatencyStats);
        });
}

[JsonSerializable(typeof(AgentRun[]))]
[JsonSerializable(typeof(AgentRun))]
[JsonSerializable(typeof(TokenUsageSummary[]))]
[JsonSerializable(typeof(AgentError[]))]
[JsonSerializable(typeof(LatencyStats))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class TelemetryToolsJsonContext : JsonSerializerContext;

#region Data Models

public record AgentRun(
    string RunId,
    string AgentName,
    string? Provider,
    string? Model,
    DateTime StartedAt,
    DateTime? CompletedAt,
    TimeSpan? Duration,
    int InputTokens,
    int OutputTokens,
    bool Success,
    string? ErrorType,
    string? ErrorMessage);

public record TokenUsageSummary(
    string GroupKey,
    int TotalInputTokens,
    int TotalOutputTokens,
    int RunCount,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public record AgentError(
    string RunId,
    string AgentName,
    string ErrorType,
    string ErrorMessage,
    DateTime OccurredAt,
    string? StackTrace);

public record LatencyStats(
    string? AgentName,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double AvgMs,
    double MinMs,
    double MaxMs,
    int SampleCount);

#endregion

#region Store Interface

public interface ITelemetryStore
{
    ValueTask<AgentRun?> GetRunAsync(string runId);

    ValueTask<AgentRun[]> SearchRunsAsync(string? provider, string? model, string? errorType, DateTime? since);

    ValueTask<TokenUsageSummary[]> GetTokenUsageAsync(DateTime? since, DateTime? until, string groupBy);

    ValueTask<AgentError[]> ListErrorsAsync(int limit, string? agentName);

    ValueTask<LatencyStats> GetLatencyStatsAsync(string? agentName, int hours);
}

#endregion
