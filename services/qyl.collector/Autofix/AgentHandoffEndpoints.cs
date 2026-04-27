namespace Qyl.Collector.Autofix;

/// <summary>
///     REST endpoints for managing coding agent handoff lifecycle:
///     create, accept, submit, fail, and query handoffs.
/// </summary>
public static class AgentHandoffEndpoints
{
    [QylMapEndpoints]
    public static void MapAgentHandoffEndpoints(this WebApplication app)
    {
        // Create handoff from fix run
        app.MapPost("/api/v1/fix-runs/{runId}/handoffs", static async Task<IResult> (
            string runId, CreateHandoffRequest request,
            AgentHandoffService service, CancellationToken ct) =>
        {
            try
            {
                var record = await service.CreateHandoffAsync(runId, request.AgentType, ct)
                    .ConfigureAwait(false);
                return TypedResults.Created($"/api/v1/handoffs/{record.HandoffId}", record);
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.NotFound(new { error = ex.Message });
            }
        });

        // List handoffs for a fix run
        app.MapGet("/api/v1/fix-runs/{runId}/handoffs", static async (
            string runId, int? limit,
            DuckDbStore store, CancellationToken ct) =>
        {
            var items = await store
                .GetHandoffsForRunAsync(runId, Math.Clamp(limit ?? 50, 1, 1000), ct).ConfigureAwait(false);
            return TypedResults.Ok(new HandoffListResponse(items, items.Count));
        });

        // Get pending handoffs (for agents polling)
        app.MapGet("/api/v1/handoffs/pending", static async Task<IResult> (
            int? limit,
            DuckDbStore store, CancellationToken ct) =>
        {
            try
            {
                var items = await store
                    .GetPendingHandoffsAsync(Math.Clamp(limit ?? 50, 1, 1000), ct).ConfigureAwait(false);
                return TypedResults.Ok(new HandoffListResponse(items, items.Count));
            }
            catch
            {
                // Keep Loom dashboard resilient when handoff tables are not initialized yet.
                return TypedResults.Ok(new HandoffListResponse(Array.Empty<AgentHandoffRecord>(), 0));
            }
        });

        // Get handoff by ID
        app.MapGet("/api/v1/handoffs/{handoffId}", static async Task<IResult> (
            string handoffId,
            DuckDbStore store, CancellationToken ct) =>
        {
            var record = await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
            return record is null ? TypedResults.NotFound() : TypedResults.Ok(record);
        });

        // Get handoff context (the full RCA + plan for the agent to work with)
        app.MapGet("/api/v1/handoffs/{handoffId}/context", static async Task<IResult> (
            string handoffId,
            AgentHandoffService service, CancellationToken ct) =>
        {
            var context = await service.GetHandoffContextAsync(handoffId, ct).ConfigureAwait(false);
            return context is null ? TypedResults.NotFound() : TypedResults.Content(context, "application/json");
        });

        // Accept handoff (agent claims it)
        app.MapPost("/api/v1/handoffs/{handoffId}/accept", static async Task<IResult> (
            string handoffId,
            AgentHandoffService service, CancellationToken ct) =>
        {
            var record = await service.AcceptHandoffAsync(handoffId, ct).ConfigureAwait(false);
            return record is null
                ? TypedResults.Conflict(new { error = "Handoff not found or not in 'pending' status" })
                : TypedResults.Ok(record);
        });

        // Submit result (agent completed the fix)
        app.MapPost("/api/v1/handoffs/{handoffId}/submit", static async Task<IResult> (
            string handoffId, SubmitHandoffRequest request,
            AgentHandoffService service, CancellationToken ct) =>
        {
            var record = await service
                .SubmitHandoffResultAsync(handoffId, request.ResultJson, ct).ConfigureAwait(false);
            return record is null
                ? TypedResults.Conflict(new { error = "Handoff not found or not in 'accepted' status" })
                : TypedResults.Ok(record);
        });

        // Fail handoff (agent couldn't complete)
        app.MapPost("/api/v1/handoffs/{handoffId}/fail", static async Task<IResult> (
            string handoffId, FailHandoffRequest request,
            AgentHandoffService service, CancellationToken ct) =>
        {
            var record = await service
                .FailHandoffAsync(handoffId, request.ErrorMessage, ct).ConfigureAwait(false);
            return record is null
                ? TypedResults.Conflict(new { error = "Handoff not found or not in 'pending'/'accepted' status" })
                : TypedResults.Ok(record);
        });
    }
}

/// <summary>Request body for POST /api/v1/fix-runs/{runId}/handoffs.</summary>
public sealed record CreateHandoffRequest(string AgentType);

/// <summary>Request body for POST /api/v1/handoffs/{handoffId}/submit.</summary>
public sealed record SubmitHandoffRequest(string ResultJson);

/// <summary>Request body for POST /api/v1/handoffs/{handoffId}/fail.</summary>
public sealed record FailHandoffRequest(string ErrorMessage);

/// <summary>List response shape for handoff queries — items + total count.</summary>
public sealed record HandoffListResponse(IReadOnlyList<AgentHandoffRecord> Items, int Total);
