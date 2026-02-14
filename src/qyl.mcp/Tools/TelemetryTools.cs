using System.ComponentModel;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for high-level agent run telemetry.
///     These wrap the ITelemetryStore interface for observability queries.
/// </summary>
[McpServerToolType]
internal sealed class TelemetryTools(ITelemetryStore store)
{
    [McpServerTool(Name = "qyl.search_agent_runs")]
    [Description("""
                 Search for AI agent run records.

                 An agent run represents a complete AI workflow execution,
                 potentially involving multiple LLM calls and tool invocations.

                 Filter by:
                 - Provider: 'anthropic', 'openai', 'google', 'azure'
                 - Model: Full or partial model name
                 - Error type: 'RateLimitError', 'TimeoutError', etc.
                 - Time range: Since a specific timestamp

                 Returns: List of agent runs with tokens, costs, and status
                 """)]
    public async Task<ToolResult<AgentRun[]>> SearchAgentRunsAsync(
        [Description("Filter by AI provider (e.g., 'anthropic', 'openai', 'google')")]
        string? provider = null,
        [Description("Filter by model name (partial match, e.g., 'claude-3')")]
        string? model = null,
        [Description("Filter by error type (e.g., 'RateLimitError', 'TimeoutError')")]
        string? errorType = null,
        [Description("Only include runs since this ISO timestamp")]
        DateTime? since = null)
    {
        try
        {
            var result = await store.SearchRunsAsync(provider, model, errorType, since).ConfigureAwait(false);
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
    [Description("""
                 Get detailed information about a specific agent run.

                 Returns the complete run record including:
                 - Provider and model used
                 - Token counts (input/output)
                 - Duration and timing
                 - Success/failure status
                 - Error details if failed

                 The run_id can be obtained from search_agent_runs.
                 """)]
    public async Task<ToolResult<AgentRun?>> GetAgentRunAsync(
        [Description("The unique run ID from search_agent_runs")]
        string runId)
    {
        try
        {
            var result = await store.GetRunAsync(runId).ConfigureAwait(false);
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
    [Description("""
                 Get aggregated token usage statistics.

                 Groups token consumption by:
                 - 'agent': Per agent/service
                 - 'model': Per AI model
                 - 'hour': Hourly breakdown

                 Returns input/output tokens, run counts, and time ranges.
                 Use this for cost analysis and usage monitoring.
                 """)]
    public async Task<ToolResult<TokenUsageSummary[]>> GetTokenUsageAsync(
        [Description("Start of time range (ISO timestamp)")]
        DateTime? since = null,
        [Description("End of time range (ISO timestamp)")]
        DateTime? until = null,
        [Description("Group by: 'agent', 'model', or 'hour' (default: agent)")]
        string groupBy = "agent")
    {
        try
        {
            var result = await store.GetTokenUsageAsync(since, until, groupBy).ConfigureAwait(false);
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
    [Description("""
                 List recent errors from agent runs.

                 Shows failed runs with:
                 - Error type and message
                 - Agent/service that failed
                 - When the error occurred
                 - Stack trace (if available)

                 Use this to quickly identify and diagnose failures.
                 """)]
    public async Task<ToolResult<AgentError[]>> ListErrorsAsync(
        [Description("Maximum errors to return (default: 50)")]
        int limit = 50,
        [Description("Filter by agent/service name")]
        string? agentName = null)
    {
        try
        {
            var result = await store.ListErrorsAsync(limit, agentName).ConfigureAwait(false);
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
    [Description("""
                 Get latency percentiles for agent operations.

                 Returns:
                 - P50, P95, P99 latencies
                 - Average, min, max latencies
                 - Sample count

                 Use this to monitor performance and identify slow operations.
                 """)]
    public async Task<ToolResult<LatencyStats>> GetLatencyStatsAsync(
        [Description("Filter by agent/service name")]
        string? agentName = null,
        [Description("Time window in hours (default: 24)")]
        int hours = 24)
    {
        try
        {
            var result = await store.GetLatencyStatsAsync(agentName, hours).ConfigureAwait(false);
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
    ValueTask<AgentRun?> GetRunAsync(string runId);
    ValueTask<AgentRun[]> SearchRunsAsync(string? provider, string? model, string? errorType, DateTime? since);
    ValueTask<TokenUsageSummary[]> GetTokenUsageAsync(DateTime? since, DateTime? until, string groupBy);
    ValueTask<AgentError[]> ListErrorsAsync(int limit, string? agentName);
    ValueTask<LatencyStats> GetLatencyStatsAsync(string? agentName, int hours);
}

#endregion
