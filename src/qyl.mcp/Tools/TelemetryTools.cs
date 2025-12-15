using System.ComponentModel;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

internal class TelemetryTools
{
    private readonly ITelemetryStore _store;

    public TelemetryTools(ITelemetryStore? store = null) => _store = store ?? InMemoryTelemetryStore.Instance;

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
        await _store.SearchRunsAsync(provider, model, errorType, since).ConfigureAwait(false);

    [McpServerTool(Name = "qyl.get_agent_run")]
    [Description("Get details of a specific agent run by ID.")]
    public async Task<AgentRun?> GetAgentRunAsync(
        [Description("The unique run ID")] string runId) =>
        await _store.GetRunAsync(runId).ConfigureAwait(false);

    [McpServerTool(Name = "qyl.get_token_usage")]
    [Description("Get token usage statistics for agents within a time range.")]
    public async Task<TokenUsageSummary[]> GetTokenUsageAsync(
        [Description("Start of time range")] DateTime? since = null,
        [Description("End of time range")] DateTime? until = null,
        [Description("Group by: 'agent', 'model', or 'hour'")]
        string groupBy = "agent") =>
        await _store.GetTokenUsageAsync(since, until, groupBy).ConfigureAwait(false);

    [McpServerTool(Name = "qyl.list_errors")]
    [Description("List recent errors from agent runs.")]
    public async Task<AgentError[]> ListErrorsAsync(
        [Description("Maximum number of errors to return")]
        int limit = 50,
        [Description("Filter by agent name")] string? agentName = null) =>
        await _store.ListErrorsAsync(limit, agentName).ConfigureAwait(false);

    [McpServerTool(Name = "qyl.get_latency_stats")]
    [Description("Get latency statistics for agent operations.")]
    public async Task<LatencyStats> GetLatencyStatsAsync(
        [Description("Filter by agent name")] string? agentName = null,
        [Description("Time range in hours (default: 24)")]
        int hours = 24) =>
        await _store.GetLatencyStatsAsync(agentName, hours).ConfigureAwait(false);
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

#region Telemetry Store Interface

public interface ITelemetryStore
{
    ValueTask RecordRunAsync(AgentRun run);
    ValueTask<AgentRun?> GetRunAsync(string runId);
    ValueTask<AgentRun[]> SearchRunsAsync(string? provider, string? model, string? errorType, DateTime? since);
    ValueTask<TokenUsageSummary[]> GetTokenUsageAsync(DateTime? since, DateTime? until, string groupBy);
    ValueTask<AgentError[]> ListErrorsAsync(int limit, string? agentName);
    ValueTask<LatencyStats> GetLatencyStatsAsync(string? agentName, int hours);
}

public sealed class InMemoryTelemetryStore : ITelemetryStore
{
    public static readonly InMemoryTelemetryStore Instance = new();
    private readonly Lock _lock = new();

    private readonly List<AgentRun> _runs = [];

    public ValueTask RecordRunAsync(AgentRun run)
    {
        lock (_lock)
        {
            _runs.Add(run);

            if (_runs.Count > 10000) _runs.RemoveAt(0);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<AgentRun?> GetRunAsync(string runId)
    {
        lock (_lock)
        {
            return ValueTask.FromResult(_runs.Find(r => r.RunId == runId));
        }
    }

    public ValueTask<AgentRun[]> SearchRunsAsync(string? provider, string? model, string? errorType, DateTime? since)
    {
        lock (_lock)
        {
            var results = _runs.FindAll(r =>
                (string.IsNullOrEmpty(provider) ||
                 r.Provider?.Equals(provider, StringComparison.OrdinalIgnoreCase) == true) &&
                (string.IsNullOrEmpty(model) || r.Model?.Contains(model, StringComparison.OrdinalIgnoreCase) == true) &&
                (string.IsNullOrEmpty(errorType) ||
                 r.ErrorType?.Equals(errorType, StringComparison.OrdinalIgnoreCase) == true) &&
                (!since.HasValue || r.StartedAt >= since.Value));

            results.Sort((a, b) => b.StartedAt.CompareTo(a.StartedAt));
            return ValueTask.FromResult(results.Take(100).ToArray());
        }
    }

    public ValueTask<TokenUsageSummary[]> GetTokenUsageAsync(DateTime? since, DateTime? until, string groupBy)
    {
        lock (_lock)
        {
            var filtered = _runs.FindAll(r =>
                (!since.HasValue || r.StartedAt >= since.Value) &&
                (!until.HasValue || r.StartedAt <= until.Value));

            Func<AgentRun, string> keySelector = groupBy.ToLowerInvariant() switch
            {
                "model" => r => r.Model ?? "unknown",
                "hour" => r => r.StartedAt.ToString("yyyy-MM-dd HH:00"),
                _ => r => r.AgentName
            };

            var summaries = filtered.AggregateBy(
                    keySelector,
                    _ => (InputTokens: 0, OutputTokens: 0, Count: 0, MinTime: DateTime.MaxValue,
                        MaxTime: DateTime.MinValue),
                    (acc, run) => (
                        acc.InputTokens + run.InputTokens,
                        acc.OutputTokens + run.OutputTokens,
                        acc.Count + 1,
                        run.StartedAt < acc.MinTime ? run.StartedAt : acc.MinTime,
                        run.StartedAt > acc.MaxTime ? run.StartedAt : acc.MaxTime
                    ))
                .Select(kv => new TokenUsageSummary(
                    kv.Key,
                    kv.Value.InputTokens,
                    kv.Value.OutputTokens,
                    kv.Value.Count,
                    kv.Value.MinTime,
                    kv.Value.MaxTime))
                .ToArray();

            return ValueTask.FromResult(summaries);
        }
    }

    public ValueTask<AgentError[]> ListErrorsAsync(int limit, string? agentName)
    {
        lock (_lock)
        {
            var errors = _runs.FindAll(r =>
                r is { Success: false, ErrorMessage: not null } &&
                (string.IsNullOrEmpty(agentName) || r.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase)));

            errors.Sort((a, b) => b.StartedAt.CompareTo(a.StartedAt));

            // Take limit and convert
            var result = errors.Count > limit
                ? errors.GetRange(0, limit).ConvertAll(ToAgentError)
                : errors.ConvertAll(ToAgentError);

            return ValueTask.FromResult(result.ToArray());
        }
    }

    public ValueTask<LatencyStats> GetLatencyStatsAsync(string? agentName, int hours)
    {
        lock (_lock)
        {
            var since = TimeProvider.System.GetUtcNow().AddHours(-hours);
            var filtered = _runs.FindAll(r =>
                r.StartedAt >= since &&
                r.Duration.HasValue &&
                (string.IsNullOrEmpty(agentName) || r.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase)));

            if (filtered.Count is 0)
                return ValueTask.FromResult(new LatencyStats(agentName, 0, 0, 0, 0, 0, 0, 0));

            var durations = filtered.ConvertAll(r => r.Duration!.Value.TotalMilliseconds);
            durations.Sort();

            return ValueTask.FromResult(new LatencyStats(
                agentName,
                Percentile(durations, 0.50),
                Percentile(durations, 0.95),
                Percentile(durations, 0.99),
                durations.Average(),
                durations.Min(),
                durations.Max(),
                durations.Count
            ));
        }
    }

    private static AgentError ToAgentError(AgentRun r) => new(
        r.RunId,
        r.AgentName,
        r.ErrorType ?? "Unknown",
        r.ErrorMessage!,
        r.StartedAt,
        null);

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count is 0) return 0;
        var index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}

#endregion
