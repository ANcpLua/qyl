using System.Collections.Concurrent;
using qyl.grpc.Extraction;
using qyl.grpc.Models;

namespace qyl.grpc.Aggregation;

/// <summary>
/// Aggregates spans into AI sessions.
/// Sessions group multiple traces/spans by conversation or user session.
/// </summary>
public sealed class SessionAggregator : ISessionAggregator
{
    private readonly ConcurrentDictionary<string, SessionBuilder> _sessions = new();
    private readonly int _maxSessions;
    private readonly TimeSpan _sessionTimeout;

    public SessionAggregator(int maxSessions = 10_000, TimeSpan? sessionTimeout = null)
    {
        _maxSessions = maxSessions;
        _sessionTimeout = sessionTimeout ?? TimeSpan.FromHours(24);
    }

    public void AddSpan(SpanModel span)
    {
        var enriched = GenAiExtractor.Enrich(span);
        var sessionId = ResolveSessionId(enriched);

        var builder = _sessions.GetOrAdd(sessionId, id => new SessionBuilder(id, span.Resource.ServiceName));
        builder.AddSpan(enriched);

        EvictExpiredSessions();
    }

    public void AddSpan(EnrichedSpan enriched)
    {
        var sessionId = ResolveSessionId(enriched);
        var builder = _sessions.GetOrAdd(sessionId, id => new SessionBuilder(id, enriched.ServiceName));
        builder.AddSpan(enriched);

        EvictExpiredSessions();
    }

    public SessionModel? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var builder) ? builder.Build() : null;

    public IReadOnlyList<SessionModel> GetRecentSessions(int limit = 100) =>
        _sessions.Values
            .Select(b => b.Build())
            .OrderByDescending(s => s.LastActivity)
            .Take(limit)
            .ToList();

    public IReadOnlyList<SessionModel> Query(SessionQuery query)
    {
        var sessions = _sessions.Values.Select(b => b.Build()).AsEnumerable();

        if (query.ServiceName is not null)
            sessions = sessions.Where(s => s.ServiceName == query.ServiceName);

        if (query.From.HasValue)
            sessions = sessions.Where(s => s.LastActivity >= query.From.Value);

        if (query.To.HasValue)
            sessions = sessions.Where(s => s.StartTime <= query.To.Value);

        if (query.MinTokens.HasValue)
            sessions = sessions.Where(s => s.TotalTokens >= query.MinTokens.Value);

        if (query.HasErrors == true)
            sessions = sessions.Where(s => s.ErrorCount > 0);

        return sessions
            .OrderByDescending(s => s.LastActivity)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToList();
    }

    public SessionStatistics GetStatistics()
    {
        var sessions = _sessions.Values.Select(b => b.Build()).ToList();

        return new SessionStatistics(
            TotalSessions: sessions.Count,
            ActiveSessions: sessions.Count(s => s.LastActivity > DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5)),
            TotalSpans: sessions.Sum(s => s.SpanCount),
            TotalInputTokens: sessions.Sum(s => s.TotalInputTokens),
            TotalOutputTokens: sessions.Sum(s => s.TotalOutputTokens),
            TotalCostUsd: sessions.Sum(s => s.TotalCostUsd),
            TotalToolCalls: sessions.Sum(s => s.ToolCallCount),
            TotalErrors: sessions.Sum(s => s.ErrorCount),
            TopModels: sessions
                .Where(s => s.PrimaryModel is not null)
                .GroupBy(s => s.PrimaryModel!)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Count())
        );
    }

    public long SessionCount => _sessions.Count;

    private static string ResolveSessionId(EnrichedSpan enriched)
    {
        if (enriched.SessionId is not null)
            return enriched.SessionId;

        return enriched.TraceId;
    }

    private void EvictExpiredSessions()
    {
        if (_sessions.Count <= _maxSessions) return;

        var cutoff = DateTimeOffset.UtcNow - _sessionTimeout;
        var expired = _sessions
            .Where(kv => kv.Value.LastActivity < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expired)
            _sessions.TryRemove(key, out _);

        if (_sessions.Count > _maxSessions)
        {
            var oldest = _sessions
                .OrderBy(kv => kv.Value.LastActivity)
                .Take(_sessions.Count - _maxSessions)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in oldest)
                _sessions.TryRemove(key, out _);
        }
    }

    private sealed class SessionBuilder
    {
        private readonly string _sessionId;
        private readonly string _serviceName;
        private readonly List<EnrichedSpan> _spans = [];
        private readonly HashSet<string> _traceIds = [];
        private readonly HashSet<string> _models = [];
        private readonly object _lock = new();

        public DateTimeOffset LastActivity { get; private set; } = DateTimeOffset.UtcNow;

        public SessionBuilder(string sessionId, string serviceName)
        {
            _sessionId = sessionId;
            _serviceName = serviceName;
        }

        public void AddSpan(EnrichedSpan span)
        {
            lock (_lock)
            {
                _spans.Add(span);
                _traceIds.Add(span.TraceId);

                if (span.GenAiModel is not null)
                    _models.Add(span.GenAiModel);

                LastActivity = DateTimeOffset.UtcNow;
            }
        }

        public SessionModel Build()
        {
            lock (_lock)
            {
                if (_spans.Count == 0)
                {
                    return new SessionModel(
                        SessionId: _sessionId,
                        ServiceName: _serviceName,
                        Spans: [],
                        TraceIds: [],
                        StartTime: DateTimeOffset.MinValue,
                        LastActivity: LastActivity,
                        SpanCount: 0,
                        ErrorCount: 0,
                        ErrorRate: 0,
                        TotalInputTokens: 0,
                        TotalOutputTokens: 0,
                        TotalTokens: 0,
                        TotalCostUsd: 0,
                        ToolCallCount: 0,
                        PrimaryModel: null,
                        Models: []);
                }

                var spans = _spans.OrderBy(s => s.StartTime).ToList();
                var errorCount = spans.Count(s => s.Status == SpanStatus.Error);

                var genAiSpans = spans.Where(s => s.IsGenAiSpan).ToList();
                var totalInput = genAiSpans.Sum(s => s.GenAiInputTokens);
                var totalOutput = genAiSpans.Sum(s => s.GenAiOutputTokens);
                var totalCost = genAiSpans.Sum(s => s.GenAiCostUsd);
                var toolCalls = genAiSpans.Count(s => s.GenAi?.IsToolCall == true);

                var primaryModel = _models
                    .GroupBy(m => m)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key;

                return new SessionModel(
                    SessionId: _sessionId,
                    ServiceName: _serviceName,
                    Spans: spans,
                    TraceIds: _traceIds.ToList(),
                    StartTime: spans.Min(s => s.StartTime),
                    LastActivity: spans.Max(s => s.StartTime),
                    SpanCount: spans.Count,
                    ErrorCount: errorCount,
                    ErrorRate: spans.Count > 0 ? (double)errorCount / spans.Count : 0,
                    TotalInputTokens: totalInput,
                    TotalOutputTokens: totalOutput,
                    TotalTokens: totalInput + totalOutput,
                    TotalCostUsd: totalCost,
                    ToolCallCount: toolCalls,
                    PrimaryModel: primaryModel,
                    Models: _models.ToList());
            }
        }
    }
}

public interface ISessionAggregator
{
    void AddSpan(SpanModel span);
    void AddSpan(EnrichedSpan enriched);
    SessionModel? GetSession(string sessionId);
    IReadOnlyList<SessionModel> GetRecentSessions(int limit = 100);
    IReadOnlyList<SessionModel> Query(SessionQuery query);
    SessionStatistics GetStatistics();
    long SessionCount { get; }
}

/// <summary>
/// An AI session - a group of related LLM interactions.
/// </summary>
public sealed record SessionModel(
    string SessionId,
    string ServiceName,
    IReadOnlyList<EnrichedSpan> Spans,
    IReadOnlyList<string> TraceIds,
    DateTimeOffset StartTime,
    DateTimeOffset LastActivity,
    int SpanCount,
    int ErrorCount,
    double ErrorRate,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalTokens,
    decimal TotalCostUsd,
    int ToolCallCount,
    string? PrimaryModel,
    IReadOnlyList<string> Models)
{
    public TimeSpan Duration => LastActivity - StartTime;
    public double DurationMinutes => Duration.TotalMinutes;
    public bool HasErrors => ErrorCount > 0;
    public bool IsActive => LastActivity > DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
}

public sealed record SessionQuery(
    string? ServiceName = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    long? MinTokens = null,
    bool? HasErrors = null,
    int Limit = 100,
    int Offset = 0);

public sealed record SessionStatistics(
    int TotalSessions,
    int ActiveSessions,
    long TotalSpans,
    long TotalInputTokens,
    long TotalOutputTokens,
    decimal TotalCostUsd,
    int TotalToolCalls,
    int TotalErrors,
    IReadOnlyDictionary<string, int> TopModels);
