using System.Collections.Concurrent;
using qyl.grpc.Abstractions;
using qyl.grpc.Models;

namespace qyl.grpc.Stores;

/// <summary>
/// Aggregates spans into sessions with GenAI statistics.
/// </summary>
public sealed class SessionAggregator : ISessionAggregator
{
    private readonly ConcurrentDictionary<string, SessionBuilder> _sessions = new();
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public long SessionCount => _sessions.Count;

    public void AddSpan(SpanModel span)
    {
        var sessionId = GetSessionId(span);
        var builder = _sessions.GetOrAdd(sessionId, _ => new SessionBuilder(sessionId, span.Resource.ServiceName));
        builder.AddSpan(span);
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
        else if (query.HasErrors == false)
            sessions = sessions.Where(s => s.ErrorCount == 0);

        return sessions
            .OrderByDescending(s => s.LastActivity)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToList();
    }

    public SessionStatistics GetStatistics()
    {
        var sessions = _sessions.Values.Select(b => b.Build()).ToList();
        var cutoff = DateTimeOffset.UtcNow - _sessionTimeout;

        var modelCounts = sessions
            .SelectMany(s => s.Models)
            .GroupBy(m => m)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());

        return new SessionStatistics(
            TotalSessions: sessions.Count,
            ActiveSessions: sessions.Count(s => s.LastActivity > cutoff),
            TotalSpans: sessions.Sum(s => s.SpanCount),
            TotalInputTokens: sessions.Sum(s => s.TotalInputTokens),
            TotalOutputTokens: sessions.Sum(s => s.TotalOutputTokens),
            TotalCostUsd: sessions.Sum(s => s.TotalCostUsd),
            TotalToolCalls: sessions.Sum(s => s.ToolCallCount),
            TotalErrors: sessions.Sum(s => s.ErrorCount),
            TopModels: modelCounts);
    }

    private static string GetSessionId(SpanModel span)
    {
        // Try session.id attribute first, fallback to trace ID
        if (span.Attributes.TryGetValue("session.id", out var attr) && attr is StringValue sv)
            return sv.Value;

        return span.TraceId;
    }

    private sealed class SessionBuilder
    {
        private readonly string _sessionId;
        private readonly string _serviceName;
        private readonly List<SpanModel> _spans = [];
        private readonly HashSet<string> _traceIds = [];
        private readonly HashSet<string> _models = [];
        private readonly Lock _lock = new();

        private DateTimeOffset _startTime = DateTimeOffset.MaxValue;
        private DateTimeOffset _lastActivity = DateTimeOffset.MinValue;
        private int _errorCount;
        private long _inputTokens;
        private long _outputTokens;
        private decimal _costUsd;
        private int _toolCallCount;
        private string? _primaryModel;

        public SessionBuilder(string sessionId, string serviceName)
        {
            _sessionId = sessionId;
            _serviceName = serviceName;
        }

        public void AddSpan(SpanModel span)
        {
            lock (_lock)
            {
                _spans.Add(span);
                _traceIds.Add(span.TraceId);

                if (span.StartTime < _startTime)
                    _startTime = span.StartTime;

                if (span.EndTime > _lastActivity)
                    _lastActivity = span.EndTime;

                if (span.Status == SpanStatus.Error)
                    _errorCount++;

                // Extract GenAI info
                var genAi = ExtractGenAiInfo(span);
                if (genAi is not null)
                {
                    _inputTokens += genAi.InputTokens;
                    _outputTokens += genAi.OutputTokens;
                    _costUsd += genAi.CostUsd;

                    if (genAi.IsToolCall)
                        _toolCallCount++;

                    if (genAi.Model is not null)
                    {
                        _models.Add(genAi.Model);
                        _primaryModel ??= genAi.Model;
                    }
                }
            }
        }

        public SessionModel Build()
        {
            lock (_lock)
            {
                var duration = _lastActivity > _startTime
                    ? (_lastActivity - _startTime).TotalMinutes
                    : 0;

                var errorRate = _spans.Count > 0 ? (double)_errorCount / _spans.Count : 0;
                var isActive = DateTimeOffset.UtcNow - _lastActivity < TimeSpan.FromMinutes(30);

                return new SessionModel(
                    SessionId: _sessionId,
                    ServiceName: _serviceName,
                    StartTime: _startTime == DateTimeOffset.MaxValue ? DateTimeOffset.UtcNow : _startTime,
                    LastActivity: _lastActivity == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : _lastActivity,
                    DurationMinutes: duration,
                    SpanCount: _spans.Count,
                    ErrorCount: _errorCount,
                    ErrorRate: errorRate,
                    TotalInputTokens: _inputTokens,
                    TotalOutputTokens: _outputTokens,
                    TotalTokens: _inputTokens + _outputTokens,
                    TotalCostUsd: _costUsd,
                    ToolCallCount: _toolCallCount,
                    PrimaryModel: _primaryModel,
                    Models: _models.ToList(),
                    TraceIds: _traceIds.ToList(),
                    Spans: _spans.ToList(),
                    IsActive: isActive);
            }
        }

        private static GenAiInfo? ExtractGenAiInfo(SpanModel span)
        {
            var attrs = span.Attributes;

            var system = GetString(attrs, "gen_ai.system");
            var model = GetString(attrs, "gen_ai.response.model") ?? GetString(attrs, "gen_ai.request.model");

            if (system is null && model is null)
                return null;

            return new GenAiInfo(
                System: system,
                Model: model,
                InputTokens: GetLong(attrs, "gen_ai.usage.input_tokens") ?? GetLong(attrs, "gen_ai.response.prompt_tokens") ?? 0,
                OutputTokens: GetLong(attrs, "gen_ai.usage.output_tokens") ?? GetLong(attrs, "gen_ai.response.completion_tokens") ?? 0,
                CostUsd: GetDecimal(attrs, "gen_ai.usage.cost") ?? 0,
                IsToolCall: GetString(attrs, "gen_ai.operation.name") == "tool_call");
        }

        private static string? GetString(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
            attrs.TryGetValue(key, out var v) && v is StringValue sv ? sv.Value : null;

        private static long? GetLong(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
            attrs.TryGetValue(key, out var v) && v is IntValue iv ? iv.Value : null;

        private static decimal? GetDecimal(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
            attrs.TryGetValue(key, out var v) && v is DoubleValue dv ? (decimal)dv.Value : null;
    }

    private sealed record GenAiInfo(
        string? System,
        string? Model,
        long InputTokens,
        long OutputTokens,
        decimal CostUsd,
        bool IsToolCall);
}
