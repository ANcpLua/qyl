
namespace Qyl.Collector.AgentRuns;

internal static class AgentInsightsEndpoints
{
    [QylMapEndpoints]
    public static WebApplication MapAgentInsightsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/agents");

        group.MapGet("/overview/traffic", GetTrafficAsync);
        group.MapGet("/overview/duration", GetDurationAsync);
        group.MapGet("/overview/issues", GetIssuesAsync);
        group.MapGet("/overview/llm-calls", GetLlmCallsAsync);
        group.MapGet("/overview/tokens", GetTokensAsync);
        group.MapGet("/overview/tool-calls", GetToolCallsAsync);

        group.MapGet("/traces", GetTracesAsync);

        group.MapGet("/traces/{traceId}/spans", GetTraceSpansAsync);

        group.MapGet("/models", GetModelsAsync);

        group.MapGet("/tools", GetToolsAsync);

        return app;
    }


    private static (long fromMs, long toMs) ParseRange(long? from, long? to)
    {
        var now = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds();
        var toMs = to ?? now;
        var fromMs = from ?? toMs - (24 * 3600_000L);
        return (fromMs, toMs);
    }


    private static async Task<IResult> GetTrafficAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return TypedResults.Ok(await svc.GetTrafficAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetDurationAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return TypedResults.Ok(await svc.GetDurationAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetIssuesAsync(
        AgentInsightsService svc, long? from, long? to, int? limit, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return TypedResults.Ok(await svc.GetIssuesAsync(f, t, limit ?? 10, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetLlmCallsAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return TypedResults.Ok(await svc.GetLlmCallsAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetTokensAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return TypedResults.Ok(await svc.GetTokensAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetToolCallsAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return TypedResults.Ok(await svc.GetToolCallsTimeseriesAsync(f, t, bucket, ct: ct).ConfigureAwait(false));
    }


    private static async Task<IResult> GetTracesAsync(
        AgentInsightsService svc, long? from, long? to, int? limit, int? offset, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return TypedResults.Ok(await svc.GetAgentTracesAsync(f, t, limit ?? 50, offset ?? 0, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetTraceSpansAsync(
        string traceId, AgentInsightsService svc, CancellationToken ct)
    {
        var spans = await svc.GetTraceSpansAsync(traceId, ct).ConfigureAwait(false);
        return TypedResults.Ok(new { spans });
    }


    private static async Task<IResult> GetModelsAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return TypedResults.Ok(await svc.GetModelsAsync(f, t, bucket, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GetToolsAsync(
        AgentInsightsService svc, long? from, long? to, string? bucket, CancellationToken ct)
    {
        var (f, t) = ParseRange(from, to);
        return TypedResults.Ok(await svc.GetToolsAsync(f, t, bucket, ct).ConfigureAwait(false));
    }
}
