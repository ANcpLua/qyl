// =============================================================================
// AgentInsightsEndpoints - REST API for agent observability dashboard
// 6 overview panels + trace list + models + tools + trace detail
// =============================================================================

namespace qyl.collector.AgentRuns;

internal static class AgentInsightsEndpoints
{
    public static WebApplication MapAgentInsightsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/agents");

        // Overview panels (6 separate endpoints for parallel fetching)
        group.MapGet("/overview/traffic", GetTrafficAsync);
        group.MapGet("/overview/duration", GetDurationAsync);
        group.MapGet("/overview/issues", GetIssuesAsync);
        group.MapGet("/overview/llm-calls", GetLlmCallsAsync);
        group.MapGet("/overview/tokens", GetTokensAsync);
        group.MapGet("/overview/tool-calls", GetToolCallsAsync);

        // Trace list
        group.MapGet("/traces", GetTracesAsync);

        // Trace detail (abbreviated trace view)
        group.MapGet("/traces/{traceId}/spans", GetTraceSpansAsync);

        // Models tab
        group.MapGet("/models", GetModelsAsync);

        // Tools tab
        group.MapGet("/tools", GetToolsAsync);

        return app;
    }

    // =========================================================================
    // Shared: parse time range from query params
    // =========================================================================

    private static (long fromMs, long toMs) ParseRange(long? from, long? to)
    {
        var now = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds();
        var toMs = to ?? now;
        var fromMs = from ?? toMs - (24 * 3600_000L); // default 24h
        return (fromMs, toMs);
    }

    // =========================================================================
    // Overview panels
    // =========================================================================

    private static async Task<IResult> GetTrafficAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return Results.Ok(await svc.GetTrafficAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetDurationAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return Results.Ok(await svc.GetDurationAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetIssuesAsync(
        AgentInsightsService svc, long? from, long? to, int? limit, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return Results.Ok(await svc.GetIssuesAsync(f, t, limit ?? 10, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetLlmCallsAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return Results.Ok(await svc.GetLlmCallsAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetTokensAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return Results.Ok(await svc.GetTokensAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetToolCallsAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return Results.Ok(await svc.GetToolCallsTimeseriesAsync(f, t, bucket, ct: ct).ConfigureAwait(false));
    }

    // =========================================================================
    // Trace list + detail
    // =========================================================================

    private static async Task<IResult> GetTracesAsync(
        AgentInsightsService svc, long? from, long? to, int? limit, int? offset, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return Results.Ok(await svc.GetAgentTracesAsync(f, t, limit ?? 50, offset ?? 0, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetTraceSpansAsync(
        string traceId, AgentInsightsService svc, CancellationToken ct)
    {
        var spans = await svc.GetTraceSpansAsync(traceId, ct).ConfigureAwait(false);
        return Results.Ok(new { spans });
    }

    // =========================================================================
    // Models + Tools tabs
    // =========================================================================

    private static async Task<IResult> GetModelsAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return Results.Ok(await svc.GetModelsAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetToolsAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return Results.Ok(await svc.GetToolsAsync(f, t, bucket, ct).ConfigureAwait(false));
    }
}
