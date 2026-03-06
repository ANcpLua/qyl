namespace qyl.collector.Autofix;

/// <summary>
///     REST endpoints for managing coding agent handoff lifecycle:
///     create, accept, submit, fail, and query handoffs.
/// </summary>
public static class AgentHandoffEndpoints
{
    public static void MapAgentHandoffEndpoints(this WebApplication app)
    {
        // Create handoff from fix run
        app.MapPost("/api/v1/fix-runs/{runId}/handoffs", static async (
            string runId, CreateHandoffRequest request,
            AgentHandoffService service, CancellationToken ct) =>
        {
            try
            {
                AgentHandoffRecord record = await service.CreateHandoffAsync(runId, request.AgentType, ct)
                    .ConfigureAwait(false);
                return Results.Created($"/api/v1/handoffs/{record.HandoffId}", record);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // List handoffs for a fix run
        app.MapGet("/api/v1/fix-runs/{runId}/handoffs", static async (
            string runId, int? limit,
            DuckDbStore store, CancellationToken ct) =>
        {
            IReadOnlyList<AgentHandoffRecord> items = await store
                .GetHandoffsForRunAsync(runId, Math.Clamp(limit ?? 50, 1, 1000), ct).ConfigureAwait(false);
            return Results.Ok(new { items, total = items.Count });
        });

        // Get pending handoffs (for agents polling)
        app.MapGet("/api/v1/handoffs/pending", static async (
            int? limit,
            DuckDbStore store, CancellationToken ct) =>
        {
            try
            {
                IReadOnlyList<AgentHandoffRecord> items = await store
                    .GetPendingHandoffsAsync(Math.Clamp(limit ?? 50, 1, 1000), ct).ConfigureAwait(false);
                return Results.Ok(new { items, total = items.Count });
            }
            catch
            {
                // Keep Loom dashboard resilient when handoff tables are not initialized yet.
                return Results.Ok(new { items = Array.Empty<AgentHandoffRecord>(), total = 0 });
            }
        });

        // Get handoff by ID
        app.MapGet("/api/v1/handoffs/{handoffId}", static async (
            string handoffId,
            DuckDbStore store, CancellationToken ct) =>
        {
            AgentHandoffRecord? record = await store.GetHandoffAsync(handoffId, ct).ConfigureAwait(false);
            return record is null ? Results.NotFound() : Results.Ok(record);
        });

        // Get handoff context (the full RCA + plan for the agent to work with)
        app.MapGet("/api/v1/handoffs/{handoffId}/context", static async (
            string handoffId,
            AgentHandoffService service, CancellationToken ct) =>
        {
            string? context = await service.GetHandoffContextAsync(handoffId, ct).ConfigureAwait(false);
            return context is null ? Results.NotFound() : Results.Content(context, "application/json");
        });

        // Accept handoff (agent claims it)
        app.MapPost("/api/v1/handoffs/{handoffId}/accept", static async (
            string handoffId,
            AgentHandoffService service, CancellationToken ct) =>
        {
            AgentHandoffRecord? record = await service.AcceptHandoffAsync(handoffId, ct).ConfigureAwait(false);
            return record is null
                ? Results.Conflict(new { error = "Handoff not found or not in 'pending' status" })
                : Results.Ok(record);
        });

        // Submit result (agent completed the fix)
        app.MapPost("/api/v1/handoffs/{handoffId}/submit", static async (
            string handoffId, SubmitHandoffRequest request,
            AgentHandoffService service, CancellationToken ct) =>
        {
            AgentHandoffRecord? record = await service
                .SubmitHandoffResultAsync(handoffId, request.ResultJson, ct).ConfigureAwait(false);
            return record is null
                ? Results.Conflict(new { error = "Handoff not found or not in 'accepted' status" })
                : Results.Ok(record);
        });

        // Fail handoff (agent couldn't complete)
        app.MapPost("/api/v1/handoffs/{handoffId}/fail", static async (
            string handoffId, FailHandoffRequest request,
            AgentHandoffService service, CancellationToken ct) =>
        {
            AgentHandoffRecord? record = await service
                .FailHandoffAsync(handoffId, request.ErrorMessage, ct).ConfigureAwait(false);
            return record is null
                ? Results.Conflict(new { error = "Handoff not found or not in 'pending'/'accepted' status" })
                : Results.Ok(record);
        });
    }
}

/// <summary>Request body for POST /api/v1/fix-runs/{runId}/handoffs.</summary>
public sealed record CreateHandoffRequest(string AgentType);

/// <summary>Request body for POST /api/v1/handoffs/{handoffId}/submit.</summary>
public sealed record SubmitHandoffRequest(string ResultJson);

/// <summary>Request body for POST /api/v1/handoffs/{handoffId}/fail.</summary>
public sealed record FailHandoffRequest(string ErrorMessage);
