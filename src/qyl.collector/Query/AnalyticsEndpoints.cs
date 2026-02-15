// =============================================================================
// AnalyticsEndpoints - REST API for AI chat analytics
// =============================================================================

namespace qyl.collector.Query;

/// <summary>
///     REST endpoints for AI chat analytics.
///     All queries run against the existing spans table via AnalyticsQueryService.
/// </summary>
internal static class AnalyticsEndpoints
{
    public static WebApplication MapAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/analytics");

        group.MapGet("/conversations", ListConversationsAsync);
        group.MapGet("/conversations/{conversationId}", GetConversationAsync);
        group.MapGet("/coverage-gaps", GetCoverageGapsAsync);
        group.MapGet("/top-questions", GetTopQuestionsAsync);
        group.MapGet("/source-analytics", GetSourceAnalyticsAsync);
        group.MapGet("/satisfaction", GetSatisfactionAsync);
        group.MapGet("/users", ListUsersAsync);
        group.MapGet("/users/{userId}/journey", GetUserJourneyAsync);

        return app;
    }

    private static async Task<IResult> ListConversationsAsync(
        AnalyticsQueryService analytics,
        string? period,
        int? offset,
        int? page,
        int? pageSize,
        bool? hasErrors,
        string? userId,
        string? model,
        CancellationToken ct)
    {
        var result = await analytics.ListConversationsAsync(
            period ?? "monthly",
            offset ?? 0,
            page ?? 1,
            pageSize ?? 20,
            hasErrors,
            userId,
            model,
            ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetConversationAsync(
        string conversationId,
        AnalyticsQueryService analytics,
        CancellationToken ct)
    {
        var result = await analytics.GetConversationAsync(conversationId, ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetCoverageGapsAsync(
        AnalyticsQueryService analytics,
        string? period,
        int? offset,
        CancellationToken ct)
    {
        var result = await analytics.GetCoverageGapsAsync(
            period ?? "monthly",
            offset ?? 0,
            ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetTopQuestionsAsync(
        AnalyticsQueryService analytics,
        string? period,
        int? offset,
        int? minConversations,
        CancellationToken ct)
    {
        var result = await analytics.GetTopQuestionsAsync(
            period ?? "monthly",
            offset ?? 0,
            minConversations ?? 3,
            ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetSourceAnalyticsAsync(
        AnalyticsQueryService analytics,
        string? period,
        int? offset,
        CancellationToken ct)
    {
        var result = await analytics.GetSourceAnalyticsAsync(
            period ?? "monthly",
            offset ?? 0,
            ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetSatisfactionAsync(
        AnalyticsQueryService analytics,
        string? period,
        int? offset,
        CancellationToken ct)
    {
        var result = await analytics.GetSatisfactionAsync(
            period ?? "monthly",
            offset ?? 0,
            ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> ListUsersAsync(
        AnalyticsQueryService analytics,
        string? period,
        int? offset,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var result = await analytics.ListUsersAsync(
            period ?? "monthly",
            offset ?? 0,
            page ?? 1,
            pageSize ?? 20,
            ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetUserJourneyAsync(
        string userId,
        AnalyticsQueryService analytics,
        CancellationToken ct)
    {
        var result = await analytics.GetUserJourneyAsync(userId, ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}
