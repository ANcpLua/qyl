using System.Collections.Concurrent;
using System.Text.Json;
using qyl.collector.Ingestion;
using qyl.collector.Storage;

namespace qyl.collector.Query;

public sealed class SessionAggregator
{
    private readonly ConcurrentDictionary<string, SessionStats> _sessions = new();
    private readonly DuckDbStore _store;

    public SessionAggregator(DuckDbStore store) =>
        _store = store;

    public void TrackSpan(SpanRecord span)
    {
        var sessionId = span.SessionId ?? span.TraceId;

        _sessions.AddOrUpdate(
            sessionId,
            _ => CreateInitialStats(span, sessionId),
            (_, existing) => UpdateStats(existing, span)
        );
    }

    public IReadOnlyList<SessionSummary> GetSessions(int limit = 100) =>
    [
        .. _sessions.Values
            .OrderByDescending(s => s.LastActivity)
            .Take(limit)
            .Select(ToSummary)
    ];

    public SessionSummary? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var stats)
            ? ToSummary(stats)
            : null;

    public IReadOnlyList<SessionSummary> GetSessionsByService(string serviceName, int limit = 100) =>
    [
        .. _sessions.Values
            .Where(s => s.Services.Contains(serviceName))
            .OrderByDescending(s => s.LastActivity)
            .Take(limit)
            .Select(ToSummary)
    ];

    private static SessionStats CreateInitialStats(SpanRecord span, string sessionId)
    {
        var genAi = GenAiExtractor.Extract(span.Attributes);

        return new SessionStats
        {
            SessionId = sessionId,
            StartTime = span.StartTime,
            LastActivity = span.EndTime,
            SpanCount = 1,
            ErrorCount = span.StatusCode == 2 ? 1 : 0,
            TotalDurationMs = (span.EndTime - span.StartTime).TotalMilliseconds,
            Services = [GetServiceName(span)],
            TraceIds = [span.TraceId],
            InputTokens = genAi.InputTokens ?? 0,
            OutputTokens = genAi.OutputTokens ?? 0,
            GenAiRequestCount = genAi.IsGenAi ? 1 : 0,
            Models = genAi.Model is not null ? [genAi.Model] : [],
            TotalCostUsd = span.CostUsd ?? 0
        };
    }

    private static SessionStats UpdateStats(SessionStats existing, SpanRecord span)
    {
        var genAi = GenAiExtractor.Extract(span.Attributes);

        existing.SpanCount++;
        if (span.StatusCode == 2) existing.ErrorCount++;

        if (span.StartTime < existing.StartTime)
            existing.StartTime = span.StartTime;

        if (span.EndTime > existing.LastActivity)
            existing.LastActivity = span.EndTime;

        existing.TotalDurationMs += (span.EndTime - span.StartTime).TotalMilliseconds;
        existing.Services.Add(GetServiceName(span));
        existing.TraceIds.Add(span.TraceId);

        if (genAi.IsGenAi)
        {
            existing.GenAiRequestCount++;
            existing.InputTokens += genAi.InputTokens ?? 0;
            existing.OutputTokens += genAi.OutputTokens ?? 0;
            existing.TotalCostUsd += span.CostUsd ?? 0;

            if (genAi.Model is not null)
                existing.Models.Add(genAi.Model);
        }

        return existing;
    }

    private static string GetServiceName(SpanRecord span)
    {
        if (span.Attributes is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(span.Attributes);
                if (doc.RootElement.TryGetProperty("service.name", out var svc) &&
                    svc.ValueKind == JsonValueKind.String)
                {
                    return svc.GetString() ?? "unknown";
                }
            }
            catch
            {
            }
        }

        return "unknown";
    }

    private static SessionSummary ToSummary(SessionStats stats) =>
        new()
        {
            SessionId = stats.SessionId,
            StartTime = stats.StartTime,
            LastActivity = stats.LastActivity,
            SpanCount = stats.SpanCount,
            ErrorCount = stats.ErrorCount,
            ErrorRate = stats.SpanCount > 0 ? (double)stats.ErrorCount / stats.SpanCount : 0,
            DurationMs = (stats.LastActivity - stats.StartTime).TotalMilliseconds,
            TraceCount = stats.TraceIds.Count,
            Services = [.. stats.Services],
            InputTokens = stats.InputTokens,
            OutputTokens = stats.OutputTokens,
            TotalTokens = stats.InputTokens + stats.OutputTokens,
            GenAiRequestCount = stats.GenAiRequestCount,
            Models = [.. stats.Models],
            TotalCostUsd = stats.TotalCostUsd
        };

    private sealed class SessionStats
    {
        public required string SessionId { get; init; }
        public DateTime StartTime { get; set; }
        public DateTime LastActivity { get; set; }
        public int SpanCount { get; set; }
        public int ErrorCount { get; set; }
        public double TotalDurationMs { get; set; }
        public HashSet<string> Services { get; init; } = [];
        public HashSet<string> TraceIds { get; init; } = [];
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int GenAiRequestCount { get; set; }
        public HashSet<string> Models { get; init; } = [];
        public decimal TotalCostUsd { get; set; }
    }
}

public sealed record SessionSummary
{
    public required string SessionId { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime LastActivity { get; init; }
    public int SpanCount { get; init; }
    public int ErrorCount { get; init; }
    public double ErrorRate { get; init; }
    public double DurationMs { get; init; }
    public int TraceCount { get; init; }
    public IReadOnlyList<string> Services { get; init; } = [];
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens { get; init; }
    public int GenAiRequestCount { get; init; }
    public IReadOnlyList<string> Models { get; init; } = [];
    public decimal TotalCostUsd { get; init; }
}
