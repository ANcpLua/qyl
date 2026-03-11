using Qyl.Contracts.Primitives;

namespace Qyl.Collector.Workflow;

/// <summary>
///     REST endpoints for querying workflow executions, checkpoints, and events.
/// </summary>
public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/workflows/runs", static async (
            WorkflowRunService service, int? limit, int? offset, string? workflowName, string? workflowId, string? status,
            CancellationToken ct) =>
        {
            var runs = await service.ListRunsAsync(
                workflowId: workflowId ?? workflowName,
                status: status,
                limit: Math.Clamp(limit ?? 50, 1, 1000),
                offset: Math.Max(offset ?? 0, 0),
                ct: ct);
            return Results.Ok(new { items = runs.Select(MapLegacyRun).ToArray(), total = runs.Count });
        });

        app.MapGet("/api/v1/workflows/runs/{runId}", static async (
            string runId, WorkflowRunService service, CancellationToken ct) =>
        {
            var run = await service.GetRunByIdAsync(runId, ct);
            return run is null ? Results.NotFound() : Results.Ok(MapLegacyRun(run));
        });

        app.MapGet("/api/v1/workflows/runs/{runId}/events", static async (
            string runId, WorkflowRunService service, CancellationToken ct) =>
        {
            var events = await service.GetEventsAsync(runId, null, 200, ct);
            return Results.Ok(new { items = events.Select(MapLegacyEvent).ToArray(), total = events.Count });
        });

        app.MapGet("/api/v1/workflows/runs/{runId}/checkpoints", static async (
            string runId, WorkflowRunService service, CancellationToken ct) =>
        {
            var checkpoints = await service.GetCheckpointsAsync(runId, ct);
            return Results.Ok(new { items = checkpoints.Select(MapLegacyCheckpoint).ToArray(), total = checkpoints.Count });
        });

        app.MapPost("/api/v1/workflows/runs/{runId}/cancel", static async (
            string runId, WorkflowRunService service, CancellationToken ct) =>
        {
            var cancelled = await service.CancelRunAsync(runId, ct);
            return cancelled
                ? Results.Ok(new { executionId = runId, status = "cancelled" })
                : Results.NotFound();
        });
    }

    private static LegacyWorkflowRunDto MapLegacyRun(WorkflowRunRow run) =>
        new()
        {
            RunId = run.Id,
            TraceId = null,
            WorkflowName = run.WorkflowId,
            WorkflowType = run.ProjectId,
            Status = run.Status,
            Trigger = run.TriggerType,
            NodeCount = run.NodeCount,
            CompletedNodes = run.CompletedNodes,
            InputTokens = 0,
            OutputTokens = 0,
            TotalCost = 0,
            StartTime = ToUnixNano(run.StartedAt ?? run.CreatedAt),
            EndTime = run.CompletedAt is { } completedAt ? ToUnixNano(completedAt) : null,
            DurationNs = run.DurationMs is { } durationMs ? durationMs * 1_000_000L : null,
            ErrorMessage = run.ErrorMessage,
            MetadataJson = run.InputJson
        };

    private static LegacyWorkflowEventDto MapLegacyEvent(WorkflowEventRow evt) =>
        new()
        {
            EventId = evt.Id,
            RunId = evt.RunId,
            EventType = evt.EventType,
            NodeId = evt.NodeId,
            Timestamp = ToUnixNano(evt.Timestamp),
            PayloadJson = evt.PayloadJson
        };

    private static LegacyWorkflowCheckpointDto MapLegacyCheckpoint(WorkflowCheckpointRow checkpoint) =>
        new()
        {
            CheckpointId = checkpoint.Id,
            RunId = checkpoint.RunId,
            NodeId = checkpoint.NodeId,
            Timestamp = ToUnixNano(checkpoint.CreatedAt),
            StateJson = checkpoint.StateJson
        };

    private static long ToUnixNano(DateTime timestamp) =>
        TimeConversions.ToUnixNano(new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc), TimeSpan.Zero));
}

internal sealed record LegacyWorkflowRunDto
{
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    [JsonPropertyName("trace_id")]
    public string? TraceId { get; init; }

    [JsonPropertyName("workflow_name")]
    public string? WorkflowName { get; init; }

    [JsonPropertyName("workflow_type")]
    public string? WorkflowType { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("trigger")]
    public string? Trigger { get; init; }

    [JsonPropertyName("node_count")]
    public required int NodeCount { get; init; }

    [JsonPropertyName("completed_nodes")]
    public required int CompletedNodes { get; init; }

    [JsonPropertyName("input_tokens")]
    public required long InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public required long OutputTokens { get; init; }

    [JsonPropertyName("total_cost")]
    public required double TotalCost { get; init; }

    [JsonPropertyName("start_time")]
    public long? StartTime { get; init; }

    [JsonPropertyName("end_time")]
    public long? EndTime { get; init; }

    [JsonPropertyName("duration_ns")]
    public long? DurationNs { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("metadata_json")]
    public string? MetadataJson { get; init; }
}

internal sealed record LegacyWorkflowEventDto
{
    [JsonPropertyName("event_id")]
    public required string EventId { get; init; }

    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    [JsonPropertyName("event_type")]
    public required string EventType { get; init; }

    [JsonPropertyName("node_id")]
    public string? NodeId { get; init; }

    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; init; }

    [JsonPropertyName("payload_json")]
    public string? PayloadJson { get; init; }
}

internal sealed record LegacyWorkflowCheckpointDto
{
    [JsonPropertyName("checkpoint_id")]
    public required string CheckpointId { get; init; }

    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    [JsonPropertyName("node_id")]
    public required string NodeId { get; init; }

    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; init; }

    [JsonPropertyName("state_json")]
    public string? StateJson { get; init; }
}
