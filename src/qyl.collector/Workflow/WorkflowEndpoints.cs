namespace qyl.collector.Workflow;

/// <summary>
///     REST endpoints for querying workflow executions, checkpoints, and events.
/// </summary>
public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/workflows/runs", static async (
            DuckDbStore store, int? limit, int? offset, string? workflowName, string? status,
            CancellationToken ct) =>
        {
            var runs = await store.GetWorkflowExecutionsAsync(
                Math.Clamp(limit ?? 50, 1, 1000),
                Math.Max(offset ?? 0, 0),
                workflowName,
                status,
                ct);
            return Results.Ok(new { items = runs, total = runs.Count });
        });

        app.MapGet("/api/v1/workflows/runs/{runId}", static async (
            string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetWorkflowExecutionAsync(runId, ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });

        app.MapGet("/api/v1/workflows/runs/{runId}/events", static async (
            string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var events = await store.GetWorkflowEventsAsync(runId, ct: ct);
            return Results.Ok(new { items = events, total = events.Count });
        });

        app.MapGet("/api/v1/workflows/runs/{runId}/checkpoints", static async (
            string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var checkpoints = await store.GetCheckpointsAsync(runId, ct);
            return Results.Ok(new { items = checkpoints, total = checkpoints.Count });
        });

        app.MapPost("/api/v1/workflows/runs/{runId}/cancel", static async (
            string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var cancelled = await store.CancelWorkflowExecutionAsync(runId, ct);
            return cancelled
                ? Results.Ok(new { executionId = runId, status = "cancelled" })
                : Results.NotFound();
        });

        app.MapWorkflowEventEndpoints();
    }
}
