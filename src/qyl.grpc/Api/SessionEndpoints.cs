using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using qyl.grpc.Abstractions;

namespace qyl.grpc.Api;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1/sessions");

        api.MapGet("/", GetSessions).WithName("GetSessions");
        api.MapGet("/{sessionId}", GetSession).WithName("GetSession");
        api.MapGet("/stats", GetStatistics).WithName("GetSessionStatistics");

        return endpoints;
    }

    private static SessionListResponse GetSessions(
        ISessionAggregator aggregator,
        string? serviceName = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        long? minTokens = null,
        bool? hasErrors = null,
        int limit = 100,
        int offset = 0)
    {
        var query = new SessionQuery(
            ServiceName: serviceName,
            From: from,
            To: to,
            MinTokens: minTokens,
            HasErrors: hasErrors,
            Limit: limit,
            Offset: offset);

        var sessions = aggregator.Query(query);

        return new SessionListResponse(
            Sessions: sessions.Select(s => new SessionDto(
                SessionId: s.SessionId,
                ServiceName: s.ServiceName,
                StartTime: s.StartTime,
                LastActivity: s.LastActivity,
                DurationMinutes: s.DurationMinutes,
                SpanCount: s.SpanCount,
                ErrorCount: s.ErrorCount,
                ErrorRate: s.ErrorRate,
                TotalInputTokens: s.TotalInputTokens,
                TotalOutputTokens: s.TotalOutputTokens,
                TotalTokens: s.TotalTokens,
                TotalCostUsd: s.TotalCostUsd,
                ToolCallCount: s.ToolCallCount,
                PrimaryModel: s.PrimaryModel,
                Models: s.Models,
                IsActive: s.IsActive
            )).ToList(),
            Total: (int)aggregator.SessionCount,
            HasMore: offset + limit < aggregator.SessionCount);
    }

    private static Results<Ok<SessionDetailResponse>, NotFound> GetSession(
        ISessionAggregator aggregator,
        string sessionId)
    {
        var session = aggregator.GetSession(sessionId);
        if (session is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new SessionDetailResponse(
            SessionId: session.SessionId,
            ServiceName: session.ServiceName,
            StartTime: session.StartTime,
            LastActivity: session.LastActivity,
            DurationMinutes: session.DurationMinutes,
            SpanCount: session.SpanCount,
            ErrorCount: session.ErrorCount,
            ErrorRate: session.ErrorRate,
            TotalInputTokens: session.TotalInputTokens,
            TotalOutputTokens: session.TotalOutputTokens,
            TotalTokens: session.TotalTokens,
            TotalCostUsd: session.TotalCostUsd,
            ToolCallCount: session.ToolCallCount,
            PrimaryModel: session.PrimaryModel,
            Models: session.Models,
            TraceIds: session.TraceIds,
            SpanCount2: session.Spans.Count));
    }

    private static SessionStatisticsResponse GetStatistics(ISessionAggregator aggregator)
    {
        var stats = aggregator.GetStatistics();

        return new SessionStatisticsResponse(
            TotalSessions: stats.TotalSessions,
            ActiveSessions: stats.ActiveSessions,
            TotalSpans: stats.TotalSpans,
            TotalInputTokens: stats.TotalInputTokens,
            TotalOutputTokens: stats.TotalOutputTokens,
            TotalCostUsd: stats.TotalCostUsd,
            TotalToolCalls: stats.TotalToolCalls,
            TotalErrors: stats.TotalErrors,
            TopModels: stats.TopModels);
    }
}

// DTOs
public sealed record SessionListResponse(
    IReadOnlyList<SessionDto> Sessions,
    int Total,
    bool HasMore);

public sealed record SessionDto(
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
    bool IsActive);

public sealed record SessionDetailResponse(
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
    int SpanCount2);

public sealed record SessionStatisticsResponse(
    int TotalSessions,
    int ActiveSessions,
    long TotalSpans,
    long TotalInputTokens,
    long TotalOutputTokens,
    decimal TotalCostUsd,
    int TotalToolCalls,
    int TotalErrors,
    IReadOnlyDictionary<string, int> TopModels);
