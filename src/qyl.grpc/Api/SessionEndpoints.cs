using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using qyl.grpc.Aggregation;

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
            serviceName,
            from,
            to,
            minTokens,
            hasErrors,
            limit,
            offset);

        var sessions = aggregator.Query(query);

        return new(
            [
                .. sessions.Select(s => new SessionDto(
                    s.SessionId,
                    s.ServiceName,
                    s.StartTime,
                    s.LastActivity,
                    s.DurationMinutes,
                    s.SpanCount,
                    s.ErrorCount,
                    s.ErrorRate,
                    s.TotalInputTokens,
                    s.TotalOutputTokens,
                    s.TotalTokens,
                    s.TotalCostUsd,
                    s.ToolCallCount,
                    s.PrimaryModel,
                    s.Models,
                    s.IsActive
                ))
            ],
            (int)aggregator.SessionCount,
            offset + limit < aggregator.SessionCount);
    }

    private static Results<Ok<SessionDetailResponse>, NotFound> GetSession(
        ISessionAggregator aggregator,
        string sessionId)
    {
        var session = aggregator.GetSession(sessionId);
        if (session is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new SessionDetailResponse(
            session.SessionId,
            session.ServiceName,
            session.StartTime,
            session.LastActivity,
            session.DurationMinutes,
            session.SpanCount,
            session.ErrorCount,
            session.ErrorRate,
            session.TotalInputTokens,
            session.TotalOutputTokens,
            session.TotalTokens,
            session.TotalCostUsd,
            session.ToolCallCount,
            session.PrimaryModel,
            session.Models,
            session.TraceIds,
            session.Spans.Count));
    }

    private static SessionStatisticsResponse GetStatistics(ISessionAggregator aggregator)
    {
        var stats = aggregator.GetStatistics();

        return new(
            stats.TotalSessions,
            stats.ActiveSessions,
            stats.TotalSpans,
            stats.TotalInputTokens,
            stats.TotalOutputTokens,
            stats.TotalCostUsd,
            stats.TotalToolCalls,
            stats.TotalErrors,
            stats.TopModels);
    }
}

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
