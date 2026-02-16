namespace qyl.collector.Workflow;

/// <summary>
///     SSE streaming endpoint for live workflow events.
/// </summary>
public static class WorkflowEventEndpoints
{
    /// <summary>
    ///     Polling interval for checking new workflow events.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    public static IEndpointRouteBuilder MapWorkflowEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/workflows/runs/{runId}/events/stream", HandleEventStream);
        return endpoints;
    }

    private static ServerSentEventsResult<SseItem<WorkflowEventRecord>> HandleEventStream(
        string runId,
        DuckDbStore store,
        HttpContext context) =>
        TypedResults.ServerSentEvents(
            StreamEventsAsync(runId, store, context.RequestAborted),
            null
        );

    private static async IAsyncEnumerable<SseItem<WorkflowEventRecord>> StreamEventsAsync(
        string runId,
        DuckDbStore store,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Send initial connected event with null data typed as WorkflowEventRecord
        yield return new SseItem<WorkflowEventRecord>(
            new WorkflowEventRecord
            {
                EventId = "connected",
                ExecutionId = runId,
                EventType = "connected",
                CreatedAtUnixNano = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000L
            },
            "connected"
        );

        var lastSequence = -1;

        while (!ct.IsCancellationRequested)
        {
            var events = await store.GetWorkflowEventsAsync(runId, lastSequence, ct).ConfigureAwait(false);

            foreach (var evt in events)
            {
                lastSequence = Math.Max(lastSequence, evt.SequenceNumber);

                yield return new SseItem<WorkflowEventRecord>(evt, evt.EventType ?? "workflow_event");
            }

            // Check if the workflow has ended
            var run = await store.GetWorkflowExecutionAsync(runId, ct).ConfigureAwait(false);
            if (run is { Status: "completed" or "failed" or "cancelled" })
            {
                yield return new SseItem<WorkflowEventRecord>(
                    new WorkflowEventRecord
                    {
                        EventId = "done",
                        ExecutionId = runId,
                        EventType = "done",
                        PayloadJson = $"{{\"finalStatus\":\"{run.Status}\"}}",
                        CreatedAtUnixNano = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000L
                    },
                    "done"
                );
                yield break;
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }
    }
}
