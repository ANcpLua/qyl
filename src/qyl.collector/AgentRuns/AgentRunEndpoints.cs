namespace qyl.collector.AgentRuns;

/// <summary>
///     Endpoints for querying agent runs and tool calls.
/// </summary>
public static class AgentRunEndpoints
{
    public static void MapAgentRunEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/agent-runs", static async (
            DuckDbStore store, int? limit, int? offset, string? agentName, string? status,
            string? trackMode, string? approvalStatus,
            CancellationToken ct) =>
        {
            var runs = await store.GetAgentRunsAsync(
                Math.Clamp(limit ?? 50, 1, 1000),
                Math.Max(offset ?? 0, 0),
                agentName,
                status,
                trackMode,
                approvalStatus,
                ct);
            return Results.Ok(new { items = runs, total = runs.Count });
        });

        app.MapGet("/api/v1/agent-runs/{runId}", static async (
            string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetAgentRunAsync(runId, ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });

        app.MapGet("/api/v1/agent-runs/{runId}/tools", static async (
            string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var calls = await store.GetToolCallsAsync(runId, ct);
            return Results.Ok(new { items = calls, total = calls.Count });
        });

        app.MapGet("/api/v1/agent-runs/{runId}/decisions", static async (
            string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var decisions = await store.GetAgentDecisionsAsync(runId, ct);
            return Results.Ok(new { items = decisions, total = decisions.Count });
        });

        app.MapGet("/api/v1/agent-runs/by-trace/{traceId}", static async (
            string traceId, DuckDbStore store, CancellationToken ct) =>
        {
            var runs = await store.GetAgentRunsByTraceAsync(traceId, ct);
            return Results.Ok(new { items = runs, total = runs.Count });
        });
    }
}
