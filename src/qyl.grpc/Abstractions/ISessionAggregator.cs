using qyl.Grpc.Models;

namespace qyl.Grpc.Abstractions;

/// <summary>
/// Aggregates spans into sessions with GenAI statistics.
/// </summary>
public interface ISessionAggregator
{
    void AddSpan(SpanModel span);
    SessionModel? GetSession(string sessionId);
    IReadOnlyList<SessionModel> GetRecentSessions(int limit = 100);
    IReadOnlyList<SessionModel> Query(SessionQuery query);
    SessionStatistics GetStatistics();
    long SessionCount { get; }
}

public sealed record SessionQuery(
    string? ServiceName = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    long? MinTokens = null,
    bool? HasErrors = null,
    int Limit = 100,
    int Offset = 0);

public sealed record SessionModel(
    string SessionId,
    string ServiceName,
    DateTimeOffset StartTime,
    DateTimeOffset LastActivity,
    double DurationMinutes,
    int SpanCount,
    int ErrorCount,
    double ErrorRate,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalTokens,
    decimal TotalCostUsd,
    int ToolCallCount,
    string? PrimaryModel,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> TraceIds,
    IReadOnlyList<SpanModel> Spans,
    bool IsActive);

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
