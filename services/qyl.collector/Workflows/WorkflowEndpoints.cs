namespace Qyl.Collector.Workflows;

public static class WorkflowEndpoints
{
    [QylMapEndpoints]
    public static WebApplication MapWorkflowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/workflows/runs")
            .WithTags("Workflows");

        group.MapGet("/", static async (
                WorkflowRunService service,
                string? projectId,
                string? workflowId,
                string? status,
                DateTimeOffset? startTime,
                DateTimeOffset? endTime,
                int? limit,
                string? cursor,
                CancellationToken ct) =>
            TypedResults.Ok(await service.ListRunsAsync(
                projectId, workflowId, status, startTime, endTime, limit, cursor, ct).ConfigureAwait(false)));

        group.MapGet("/{runId}", static async Task<IResult> (
            string runId,
            WorkflowRunService service,
            CancellationToken ct) =>
        {
            var run = await service.GetRunAsync(runId, ct).ConfigureAwait(false);
            return run is null ? TypedResults.NotFound() : TypedResults.Ok(run);
        });

        group.MapGet("/{runId}/nodes", static async (
                string runId,
                WorkflowRunService service,
                int? limit,
                string? cursor,
                CancellationToken ct) =>
            TypedResults.Ok(await service.GetRunNodesAsync(runId, limit, cursor, ct).ConfigureAwait(false)));

        group.MapGet("/{runId}/events", static async (
                string runId,
                WorkflowRunService service,
                long? afterSequence,
                int? limit,
                CancellationToken ct) =>
            TypedResults.Ok(await service.GetRunEventsAsync(runId, afterSequence, limit, ct).ConfigureAwait(false)));

        group.MapGet("/{runId}/checkpoints", static async (
                string runId,
                WorkflowRunService service,
                CancellationToken ct) =>
            TypedResults.Ok(await service.GetRunCheckpointsAsync(runId, ct).ConfigureAwait(false)));

        group.MapPost("/{runId}/resume", static async Task<IResult> (
            string runId,
            WorkflowRunService service,
            CancellationToken ct) =>
        {
            var run = await service.ResumeRunAsync(runId, ct).ConfigureAwait(false);
            return run is null ? TypedResults.NotFound() : TypedResults.Ok(run);
        });

        group.MapPost("/{runId}/nodes/{nodeId}/approve", static async Task<IResult> (
            string runId,
            string nodeId,
            WorkflowRunService service,
            CancellationToken ct) =>
        {
            var node = await service.ApproveNodeAsync(runId, nodeId, ct).ConfigureAwait(false);
            return node is null ? TypedResults.NotFound() : TypedResults.Ok(node);
        });

        group.MapPost("/{runId}/cancel", static async Task<IResult> (
            string runId,
            WorkflowRunService service,
            CancellationToken ct) =>
        {
            var run = await service.CancelRunAsync(runId, ct).ConfigureAwait(false);
            return run is null ? TypedResults.NotFound() : TypedResults.Ok(run);
        });

        return app;
    }
}
