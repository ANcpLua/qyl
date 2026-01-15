namespace qyl.mcp.Tools;

[McpServerToolType]
internal sealed class TelemetryTools(ITelemetryStore store)
{
    [McpServerTool(Name = "qyl.search_agent_runs")]
    [Description("Search for agent run records by provider, model, error type, or time range.")]
    public async Task<AgentRun[]> SearchAgentRunsAsync(
        [Description("Filter by AI provider (e.g., 'anthropic', 'openai', 'google')")]
        string? provider = null,
        [Description("Filter by model name (e.g., 'claude-4-haiku', 'gpt-5o')")]
        string? model = null,
        [Description("Filter by error type (e.g., 'RateLimitError', 'TimeoutError')")]
        string? errorType = null,
        [Description("Only include runs since this timestamp")]
        DateTime? since = null) =>
        await store.SearchRunsAsync(provider, model, errorType, since).ConfigureAwait(false);

    [McpServerTool(Name = "qyl.get_agent_run")]
    [Description("Get details of a specific agent run by ID.")]
    public async Task<AgentRun?> GetAgentRunAsync(
        [Description("The unique run ID")] string runId) =>
        await store.GetRunAsync(runId).ConfigureAwait(false);

    [McpServerTool(Name = "qyl.get_token_usage")]
    [Description("Get token usage statistics for agents within a time range.")]
    public async Task<TokenUsageSummary[]> GetTokenUsageAsync(
        [Description("Start of time range")] DateTime? since = null,
        [Description("End of time range")] DateTime? until = null,
        [Description("Group by: 'agent', 'model', or 'hour'")]
        string groupBy = "agent") =>
        await store.GetTokenUsageAsync(since, until, groupBy).ConfigureAwait(false);

    [McpServerTool(Name = "qyl.list_errors")]
    [Description("List recent errors from agent runs.")]
    public async Task<AgentError[]> ListErrorsAsync(
        [Description("Maximum number of errors to return")]
        int limit = 50,
        [Description("Filter by agent name")] string? agentName = null) =>
        await store.ListErrorsAsync(limit, agentName).ConfigureAwait(false);

    [McpServerTool(Name = "qyl.get_latency_stats")]
    [Description("Get latency statistics for agent operations.")]
    public async Task<LatencyStats> GetLatencyStatsAsync(
        [Description("Filter by agent name")] string? agentName = null,
        [Description("Time range in hours (default: 24)")]
        int hours = 24) =>
        await store.GetLatencyStatsAsync(agentName, hours).ConfigureAwait(false);
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
