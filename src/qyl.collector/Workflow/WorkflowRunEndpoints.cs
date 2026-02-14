namespace qyl.collector.Workflow;

/// <summary>
///     Minimal API endpoints for the v2 workflow engine (migration-based tables).
///     Routes: <c>/api/v1/workflows/*</c>
/// </summary>
public static class WorkflowRunEndpoints
{
    /// <summary>
    ///     Maps workflow run, node, event, and checkpoint query endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapWorkflowRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/workflows")
            .WithTags("Workflows");

        // --- Workflow Runs ---

        group.MapGet("/", static async (
            [Microsoft.AspNetCore.Mvc.FromServices] WorkflowRunService service,
            string? projectId, string? workflowId, string? status,
            string? triggerType, int? limit, int? offset,
            CancellationToken ct) =>
        {
            var runs = await service.ListRunsAsync(
                projectId, workflowId, status, triggerType,
                Math.Clamp(limit ?? 50, 1, 1000),
                Math.Max(offset ?? 0, 0),
                ct).ConfigureAwait(false);
            return Results.Ok(new { items = runs, total = runs.Count });
        })
        .WithName("ListWorkflowRuns")
        .WithSummary("List workflow runs with filtering");

        group.MapGet("/{runId}", static async (
            string runId, [Microsoft.AspNetCore.Mvc.FromServices] WorkflowRunService service, CancellationToken ct) =>
        {
            var run = await service.GetRunByIdAsync(runId, ct).ConfigureAwait(false);
            return run is null ? Results.NotFound() : Results.Ok(run);
        })
        .WithName("GetWorkflowRun")
        .WithSummary("Get a single workflow run by ID");

        // --- Workflow Nodes ---

        group.MapGet("/{runId}/nodes", static async (
            string runId, [Microsoft.AspNetCore.Mvc.FromServices] WorkflowRunService service, CancellationToken ct) =>
        {
            var nodes = await service.GetNodesAsync(runId, ct).ConfigureAwait(false);
            return Results.Ok(new { items = nodes, total = nodes.Count });
        })
        .WithName("GetWorkflowNodes")
        .WithSummary("Get all nodes for a workflow run");

        // --- Workflow Events ---

        group.MapGet("/{runId}/events", static async (
            string runId, [Microsoft.AspNetCore.Mvc.FromServices] WorkflowRunService service,
            long? afterSequence, int? limit, CancellationToken ct) =>
        {
            var events = await service.GetEventsAsync(
                runId, afterSequence, Math.Clamp(limit ?? 200, 1, 5000), ct).ConfigureAwait(false);
            return Results.Ok(new { items = events, total = events.Count });
        })
        .WithName("GetWorkflowRunEvents")
        .WithSummary("Get events for a workflow run with sequence cursor");

        // --- Workflow Checkpoints ---

        group.MapGet("/{runId}/checkpoints", static async (
            string runId, [Microsoft.AspNetCore.Mvc.FromServices] WorkflowRunService service, CancellationToken ct) =>
        {
            var checkpoints = await service.GetCheckpointsAsync(runId, ct).ConfigureAwait(false);
            return Results.Ok(new { items = checkpoints, total = checkpoints.Count });
        })
        .WithName("GetWorkflowRunCheckpoints")
        .WithSummary("Get checkpoints for a workflow run");

        // --- Resume / Approve / Cancel ---

        group.MapPost("/{runId}/resume", static async (
            string runId, [Microsoft.AspNetCore.Mvc.FromServices] WorkflowRunService service, CancellationToken ct) =>
        {
            var resumed = await service.ResumeRunAsync(runId, ct).ConfigureAwait(false);
            return resumed
                ? Results.Ok(new { runId, status = "running" })
                : Results.NotFound();
        })
        .WithName("ResumeWorkflowRun")
        .WithSummary("Resume a paused workflow run");

        group.MapPost("/{runId}/nodes/{nodeId}/approve", static async (
            string runId, string nodeId,
            [Microsoft.AspNetCore.Mvc.FromServices] WorkflowRunService service, CancellationToken ct) =>
        {
            var approved = await service.ApproveNodeAsync(runId, nodeId, ct).ConfigureAwait(false);
            return approved
                ? Results.Ok(new { runId, nodeId, status = "approved" })
                : Results.NotFound();
        })
        .WithName("ApproveWorkflowNode")
        .WithSummary("Approve a workflow node awaiting human approval");

        group.MapPost("/{runId}/cancel", static async (
            string runId, [Microsoft.AspNetCore.Mvc.FromServices] WorkflowRunService service, CancellationToken ct) =>
        {
            var cancelled = await service.CancelRunAsync(runId, ct).ConfigureAwait(false);
            return cancelled
                ? Results.Ok(new { runId, status = "cancelled" })
                : Results.NotFound();
        })
        .WithName("CancelWorkflowRun")
        .WithSummary("Cancel a running or pending workflow run");

        return endpoints;
    }
}
