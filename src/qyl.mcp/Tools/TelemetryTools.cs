using System.ComponentModel;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
internal sealed class TelemetryTools(ITelemetryStore store)
{
    private readonly ITelemetryStore _store = store;

    [McpServerTool(Name = "qyl.search_agent_runs")]
    [Description("Search for agent run records by provider, model, error type, or time range.")]
    public async Task<ToolResult<AgentRun[]>> SearchAgentRunsAsync(
        [Description("Filter by AI provider (e.g., 'anthropic', 'openai', 'google')")]
        string? provider = null,
        [Description("Filter by model name (e.g., 'claude-4-haiku', 'gpt-5o')")]
        string? model = null,
        [Description("Filter by error type (e.g., 'RateLimitError', 'TimeoutError')")]
        string? errorType = null,
        [Description("Only include runs since this timestamp")]
        DateTime? since = null)
    {
        try
        {
            var result = await _store.SearchRunsAsync(provider, model, errorType, since).ConfigureAwait(false);
            return ToolResult.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error<AgentRun[]>($"Failed to connect to qyl collector: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Error<AgentRun[]>($"Search failed: {ex.Message}");
        }
    }

    [McpServerTool(Name = "qyl.get_agent_run")]
    [Description("Get details of a specific agent run by ID.")]
    public async Task<ToolResult<AgentRun?>> GetAgentRunAsync(
        [Description("The unique run ID")] string runId)
    {
        try
        {
            var result = await _store.GetRunAsync(runId).ConfigureAwait(false);
            return ToolResult.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error<AgentRun?>($"Failed to connect to qyl collector: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Error<AgentRun?>($"Get run failed: {ex.Message}");
        }
    }

    [McpServerTool(Name = "qyl.get_token_usage")]
    [Description("Get token usage statistics for agents within a time range.")]
    public async Task<ToolResult<TokenUsageSummary[]>> GetTokenUsageAsync(
        [Description("Start of time range")] DateTime? since = null,
        [Description("End of time range")] DateTime? until = null,
        [Description("Group by: 'agent', 'model', or 'hour'")]
        string groupBy = "agent")
    {
        try
        {
            var result = await _store.GetTokenUsageAsync(since, until, groupBy).ConfigureAwait(false);
            return ToolResult.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error<TokenUsageSummary[]>($"Failed to connect to qyl collector: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Error<TokenUsageSummary[]>($"Get token usage failed: {ex.Message}");
        }
    }

    [McpServerTool(Name = "qyl.list_errors")]
    [Description("List recent errors from agent runs.")]
    public async Task<ToolResult<AgentError[]>> ListErrorsAsync(
        [Description("Maximum number of errors to return")]
        int limit = 50,
        [Description("Filter by agent name")] string? agentName = null)
    {
        try
        {
            var result = await _store.ListErrorsAsync(limit, agentName).ConfigureAwait(false);
            return ToolResult.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error<AgentError[]>($"Failed to connect to qyl collector: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Error<AgentError[]>($"List errors failed: {ex.Message}");
        }
    }

    [McpServerTool(Name = "qyl.get_latency_stats")]
    [Description("Get latency statistics for agent operations.")]
    public async Task<ToolResult<LatencyStats>> GetLatencyStatsAsync(
        [Description("Filter by agent name")] string? agentName = null,
        [Description("Time range in hours (default: 24)")]
        int hours = 24)
    {
        try
        {
            var result = await _store.GetLatencyStatsAsync(agentName, hours).ConfigureAwait(false);
            return ToolResult.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Error<LatencyStats>($"Failed to connect to qyl collector: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Error<LatencyStats>($"Get latency stats failed: {ex.Message}");
        }
    }
}

/// <summary>
///     Result wrapper for MCP tool responses with error handling.
/// </summary>
public record ToolResult<T>(bool Success, T? Data, string? Error)
{
    public static implicit operator T?(ToolResult<T> result) => result.Data;
}

public static class ToolResult
{
    public static ToolResult<T> Ok<T>(T data) => new(true, data, null);
    public static ToolResult<T> Error<T>(string message) => new(false, default, message);
}

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
    ValueTask RecordRunAsync(AgentRun run);
    ValueTask<AgentRun?> GetRunAsync(string runId);
    ValueTask<AgentRun[]> SearchRunsAsync(string? provider, string? model, string? errorType, DateTime? since);
    ValueTask<TokenUsageSummary[]> GetTokenUsageAsync(DateTime? since, DateTime? until, string groupBy);
    ValueTask<AgentError[]> ListErrorsAsync(int limit, string? agentName);
    ValueTask<LatencyStats> GetLatencyStatsAsync(string? agentName, int hours);
}

#endregion
