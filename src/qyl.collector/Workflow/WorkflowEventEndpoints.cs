namespace Qyl.Collector.Workflow;

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

    private static ServerSentEventsResult<SseItem<LegacyWorkflowEventDto>> HandleEventStream(
        string runId,
        WorkflowRunService service,
        HttpContext context) =>
        TypedResults.ServerSentEvents(
            StreamEventsAsync(runId, service, context.RequestAborted),
            null
        );

    private static async IAsyncEnumerable<SseItem<LegacyWorkflowEventDto>> StreamEventsAsync(
        string runId,
        WorkflowRunService service,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new SseItem<LegacyWorkflowEventDto>(
            new LegacyWorkflowEventDto
            {
                EventId = "connected",
                RunId = runId,
                EventType = "connected",
                Timestamp = TimeConversions.ToUnixNano(TimeProvider.System.GetUtcNow())
            },
            "connected"
        );

        long lastSequence = -1;

        while (!ct.IsCancellationRequested)
        {
            var events = await service.GetEventsAsync(runId, lastSequence, 200, ct).ConfigureAwait(false);

            foreach (var evt in events)
            {
                lastSequence = Math.Max(lastSequence, evt.SequenceNumber);

                yield return new SseItem<LegacyWorkflowEventDto>(
                    new LegacyWorkflowEventDto
                    {
                        EventId = evt.Id,
                        RunId = evt.RunId,
                        EventType = evt.EventType,
                        NodeId = evt.NodeId,
                        Timestamp = TimeConversions.ToUnixNano(new DateTimeOffset(evt.Timestamp, TimeSpan.Zero)),
                        PayloadJson = evt.PayloadJson
                    },
                    evt.EventType
                );
            }

            var run = await service.GetRunByIdAsync(runId, ct).ConfigureAwait(false);
            if (run is { Status: "completed" or "failed" or "cancelled" })
            {
                yield return new SseItem<LegacyWorkflowEventDto>(
                    new LegacyWorkflowEventDto
                    {
                        EventId = "done",
                        RunId = runId,
                        EventType = "done",
                        PayloadJson = JsonSerializer.Serialize(new { finalStatus = run.Status }),
                        Timestamp = TimeConversions.ToUnixNano(TimeProvider.System.GetUtcNow())
                    },
                    "done"
                );
                yield break;
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }
    }
}
