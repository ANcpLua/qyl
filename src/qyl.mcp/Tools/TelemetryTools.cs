using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

internal class TelemetryTools
{
    private readonly ITelemetryStore _store;

    public TelemetryTools(ITelemetryStore? store = null)
    {
        _store = store ?? InMemoryTelemetryStore.Instance;
    }

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
        DateTime? since = null)
    {
        return await _store.SearchRunsAsync(provider, model, errorType, since).ConfigureAwait(false);
    }

    [McpServerTool(Name = "qyl.get_agent_run")]
    [Description("Get details of a specific agent run by ID.")]
    public async Task<AgentRun?> GetAgentRunAsync(
        [Description("The unique run ID")] string runId)
    {
        return await _store.GetRunAsync(runId).ConfigureAwait(false);
    }

    [McpServerTool(Name = "qyl.get_token_usage")]
    [Description("Get token usage statistics for agents within a time range.")]
    public async Task<TokenUsageSummary[]> GetTokenUsageAsync(
        [Description("Start of time range")] DateTime? since = null,
        [Description("End of time range")] DateTime? until = null,
        [Description("Group by: 'agent', 'model', or 'hour'")]
        string groupBy = "agent")
    {
        return await _store.GetTokenUsageAsync(since, until, groupBy).ConfigureAwait(false);
    }

    [McpServerTool(Name = "qyl.list_errors")]
    [Description("List recent errors from agent runs.")]
    public async Task<AgentError[]> ListErrorsAsync(
        [Description("Maximum number of errors to return")]
        int limit = 50,
        [Description("Filter by agent name")] string? agentName = null)
    {
        return await _store.ListErrorsAsync(limit, agentName).ConfigureAwait(false);
    }

    [McpServerTool(Name = "qyl.get_latency_stats")]
    [Description("Get latency statistics for agent operations.")]
    public async Task<LatencyStats> GetLatencyStatsAsync(
        [Description("Filter by agent name")] string? agentName = null,
        [Description("Time range in hours (default: 24)")]
        int hours = 24)
    {
        return await _store.GetLatencyStatsAsync(agentName, hours).ConfigureAwait(false);
    }
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
    Task RecordRunAsync(AgentRun run);
    Task<AgentRun?> GetRunAsync(string runId);
    Task<AgentRun[]> SearchRunsAsync(string? provider, string? model, string? errorType, DateTime? since);
    Task<TokenUsageSummary[]> GetTokenUsageAsync(DateTime? since, DateTime? until, string groupBy);
    Task<AgentError[]> ListErrorsAsync(int limit, string? agentName);
    Task<LatencyStats> GetLatencyStatsAsync(string? agentName, int hours);
}

public sealed class InMemoryTelemetryStore : ITelemetryStore
{
    public static readonly InMemoryTelemetryStore Instance = new();
    private readonly Lock _lock = new();

    private readonly List<AgentRun> _runs = [];

    public Task RecordRunAsync(AgentRun run)
    {
        lock (_lock)
        {
            _runs.Add(run);

            if (_runs.Count > 10000) _runs.RemoveAt(0);
        }

        return Task.CompletedTask;
    }

    public Task<AgentRun?> GetRunAsync(string runId)
    {
        lock (_lock)
        {
            return Task.FromResult(_runs.FirstOrDefault(r => r.RunId == runId));
        }
    }

    public Task<AgentRun[]> SearchRunsAsync(string? provider, string? model, string? errorType, DateTime? since)
    {
        lock (_lock)
        {
            var query = _runs.AsEnumerable();

            if (!string.IsNullOrEmpty(provider))
                query = query.Where(r => r.Provider?.Equals(provider, StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrEmpty(model))
                query = query.Where(r => r.Model?.Contains(model, StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrEmpty(errorType))
                query = query.Where(r => r.ErrorType?.Equals(errorType, StringComparison.OrdinalIgnoreCase) == true);

            if (since.HasValue)
                query = query.Where(r => r.StartedAt >= since.Value);

            return Task.FromResult(query.OrderByDescending(r => r.StartedAt).Take(100).ToArray());
        }
    }

    public Task<TokenUsageSummary[]> GetTokenUsageAsync(DateTime? since, DateTime? until, string groupBy)
    {
        lock (_lock)
        {
            var query = _runs.AsEnumerable();

            if (since.HasValue)
                query = query.Where(r => r.StartedAt >= since.Value);

            if (until.HasValue)
                query = query.Where(r => r.StartedAt <= until.Value);

            var grouped = groupBy.ToLowerInvariant() switch
            {
                "model" => query.GroupBy(r => r.Model ?? "unknown"),
                "hour" => query.GroupBy(r => r.StartedAt.ToString("yyyy-MM-dd HH:00")),
                _ => query.GroupBy(r => r.AgentName)
            };

            return Task.FromResult(grouped.Select(g => new TokenUsageSummary(
                g.Key,
                g.Sum(r => r.InputTokens),
                g.Sum(r => r.OutputTokens),
                g.Count(),
                g.Min(r => r.StartedAt),
                g.Max(r => r.StartedAt)
            )).ToArray());
        }
    }

    public Task<AgentError[]> ListErrorsAsync(int limit, string? agentName)
    {
        lock (_lock)
        {
            var query = _runs.Where(r => r is { Success: false, ErrorMessage: not null });

            if (!string.IsNullOrEmpty(agentName))
                query = query.Where(r => r.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(query
                .OrderByDescending(r => r.StartedAt)
                .Take(limit)
                .Select(r => new AgentError(
                    r.RunId,
                    r.AgentName,
                    r.ErrorType ?? "Unknown",
                    r.ErrorMessage!,
                    r.StartedAt,
                    null))
                .ToArray());
        }
    }

    public Task<LatencyStats> GetLatencyStatsAsync(string? agentName, int hours)
    {
        lock (_lock)
        {
            var since = TimeProvider.System.GetUtcNow().AddHours(-hours);
            var query = _runs.Where(r => r.StartedAt >= since && r.Duration.HasValue);

            if (!string.IsNullOrEmpty(agentName))
                query = query.Where(r => r.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase));

            var durations = query.Select(r => r.Duration!.Value.TotalMilliseconds).OrderBy(d => d).ToList();

            if (durations.Count == 0) return Task.FromResult(new LatencyStats(agentName, 0, 0, 0, 0, 0, 0, 0));

            return Task.FromResult(new LatencyStats(
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

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}

#endregion
