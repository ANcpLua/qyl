using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for high-level agent run telemetry.
///     These wrap the ITelemetryStore interface for observability queries.
/// </summary>
[McpServerToolType]
internal sealed class TelemetryTools(ITelemetryStore store)
{
    [McpServerTool(Name = "qyl.search_agent_runs", Title = "Search Agent Runs",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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
    public Task<string> SearchAgentRunsAsync(
        [Description("Filter by AI provider (e.g., 'anthropic', 'openai', 'google')")]
        string? provider = null,
        [Description("Filter by model name (partial match, e.g., 'claude-3')")]
        string? model = null,
        [Description("Filter by error type (e.g., 'RateLimitError', 'TimeoutError')")]
        string? errorType = null,
        [Description("Only include runs since this ISO timestamp")]
        DateTime? since = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await store.SearchRunsAsync(provider, model, errorType, since)
                .ConfigureAwait(false);
            return result.Length is 0
                ? "No agent runs found."
                : JsonSerializer.Serialize(result, TelemetryToolsJsonContext.Default.AgentRunArray);
        });

    [McpServerTool(Name = "qyl.get_agent_run", Title = "Get Agent Run",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
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
    public Task<string> GetAgentRunAsync(
        [Description("The unique run ID from search_agent_runs")]
        string runId) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await store.GetRunAsync(runId).ConfigureAwait(false);
            return result is null
                ? $"Agent run '{runId}' not found."
                : JsonSerializer.Serialize(result, TelemetryToolsJsonContext.Default.AgentRun);
        });

    [McpServerTool(Name = "qyl.get_token_usage", Title = "Get Token Usage",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Get aggregated token usage statistics.

                 Groups token consumption by:
                 - 'agent': Per agent/service
                 - 'model': Per AI model
                 - 'hour': Hourly breakdown

                 Returns input/output tokens, run counts, and time ranges.
                 Use this for cost analysis and usage monitoring.
                 """)]
    public Task<string> GetTokenUsageAsync(
        [Description("Start of time range (ISO timestamp)")]
        DateTime? since = null,
        [Description("End of time range (ISO timestamp)")]
        DateTime? until = null,
        [Description("Group by: 'agent', 'model', or 'hour' (default: agent)")]
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
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 List recent errors from agent runs.

                 Shows failed runs with:
                 - Error type and message
                 - Agent/service that failed
                 - When the error occurred
                 - Stack trace (if available)

                 Use this to quickly identify and diagnose failures.
                 """)]
    public Task<string> ListErrorsAsync(
        [Description("Maximum errors to return (default: 50)")]
        int limit = 50,
        [Description("Filter by agent/service name")]
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
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Get latency percentiles for agent operations.

                 Returns:
                 - P50, P95, P99 latencies
                 - Average, min, max latencies
                 - Sample count

                 Use this to monitor performance and identify slow operations.
                 """)]
    public Task<string> GetLatencyStatsAsync(
        [Description("Filter by agent/service name")]
        string? agentName = null,
        [Description("Time window in hours (default: 24)")]
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

/// <summary>Represents a completed AI agent workflow execution with token usage and status.</summary>
/// <param name="RunId">Unique identifier for the run.</param>
/// <param name="AgentName">Name of the agent or service that executed the run.</param>
/// <param name="Provider">AI provider used (e.g. 'anthropic', 'openai').</param>
/// <param name="Model">Model name used for the run.</param>
/// <param name="StartedAt">When the run started.</param>
/// <param name="CompletedAt">When the run completed, if finished.</param>
/// <param name="Duration">Total duration of the run.</param>
/// <param name="InputTokens">Number of input tokens consumed.</param>
/// <param name="OutputTokens">Number of output tokens produced.</param>
/// <param name="Success">Whether the run completed successfully.</param>
/// <param name="ErrorType">Type of error if the run failed.</param>
/// <param name="ErrorMessage">Error message if the run failed.</param>
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

/// <summary>Aggregated token usage statistics for a grouping key (agent, model, or hour).</summary>
/// <param name="GroupKey">Grouping key value (agent name, model name, or hour label).</param>
/// <param name="TotalInputTokens">Total input tokens consumed in the group.</param>
/// <param name="TotalOutputTokens">Total output tokens produced in the group.</param>
/// <param name="RunCount">Number of runs in the group.</param>
/// <param name="PeriodStart">Earliest run timestamp in the group.</param>
/// <param name="PeriodEnd">Latest run timestamp in the group.</param>
public record TokenUsageSummary(
    string GroupKey,
    int TotalInputTokens,
    int TotalOutputTokens,
    int RunCount,
    DateTime PeriodStart,
    DateTime PeriodEnd);

/// <summary>Represents an error that occurred during an agent run.</summary>
/// <param name="RunId">The run ID where the error occurred.</param>
/// <param name="AgentName">Name of the agent or service that failed.</param>
/// <param name="ErrorType">Classification of the error (e.g. 'RateLimitError').</param>
/// <param name="ErrorMessage">Human-readable error message.</param>
/// <param name="OccurredAt">When the error occurred.</param>
/// <param name="StackTrace">Stack trace if available.</param>
public record AgentError(
    string RunId,
    string AgentName,
    string ErrorType,
    string ErrorMessage,
    DateTime OccurredAt,
    string? StackTrace);

/// <summary>Latency percentile statistics for agent operations over a time window.</summary>
/// <param name="AgentName">Agent or service name, or null for all agents.</param>
/// <param name="P50Ms">50th percentile latency in milliseconds.</param>
/// <param name="P95Ms">95th percentile latency in milliseconds.</param>
/// <param name="P99Ms">99th percentile latency in milliseconds.</param>
/// <param name="AvgMs">Average latency in milliseconds.</param>
/// <param name="MinMs">Minimum latency in milliseconds.</param>
/// <param name="MaxMs">Maximum latency in milliseconds.</param>
/// <param name="SampleCount">Number of samples used in the calculation.</param>
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

/// <summary>Abstraction for querying agent run telemetry from a backing store.</summary>
public interface ITelemetryStore
{
    /// <summary>Retrieves a single agent run by its ID.</summary>
    /// <param name="runId">Unique identifier of the run.</param>
    /// <returns>The agent run, or <c>null</c> if not found.</returns>
    ValueTask<AgentRun?> GetRunAsync(string runId);

    /// <summary>Searches agent runs with optional provider, model, error type, and time filters.</summary>
    /// <param name="provider">Filter by AI provider name.</param>
    /// <param name="model">Filter by model name with partial matching.</param>
    /// <param name="errorType">Filter by error type classification.</param>
    /// <param name="since">Only include runs after this timestamp.</param>
    /// <returns>An array of matching agent runs.</returns>
    ValueTask<AgentRun[]> SearchRunsAsync(string? provider, string? model, string? errorType, DateTime? since);

    /// <summary>Retrieves aggregated token usage grouped by agent, model, or hour.</summary>
    /// <param name="since">Start of the time range.</param>
    /// <param name="until">End of the time range.</param>
    /// <param name="groupBy">Grouping key: 'agent', 'model', or 'hour'.</param>
    /// <returns>An array of token usage summaries per group.</returns>
    ValueTask<TokenUsageSummary[]> GetTokenUsageAsync(DateTime? since, DateTime? until, string groupBy);

    /// <summary>Lists recent errors from agent runs.</summary>
    /// <param name="limit">Maximum number of errors to return.</param>
    /// <param name="agentName">Optional filter by agent or service name.</param>
    /// <returns>An array of agent errors.</returns>
    ValueTask<AgentError[]> ListErrorsAsync(int limit, string? agentName);

    /// <summary>Computes latency percentile statistics for agent operations.</summary>
    /// <param name="agentName">Optional filter by agent or service name.</param>
    /// <param name="hours">Time window in hours.</param>
    /// <returns>Latency statistics including P50, P95, P99, and sample count.</returns>
    ValueTask<LatencyStats> GetLatencyStatsAsync(string? agentName, int hours);
}

#endregion
