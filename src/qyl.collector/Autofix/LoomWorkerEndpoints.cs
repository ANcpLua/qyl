namespace Qyl.Collector.Autofix;

/// <summary>
///     Endpoints consumed by standalone Loom background workers.
///     Not user-facing — these support the autofix/triage/regression polling loop.
/// </summary>
public static class LoomWorkerEndpoints
{
    public static void MapLoomWorkerEndpoints(this WebApplication app)
    {
        // Fix runs — global query (not scoped to issue)
        app.MapGet("/api/v1/fix-runs", static async Task<IResult> (
            string? status, int? limit,
            DuckDbStore store, CancellationToken ct) =>
        {
            if (status is "pending")
            {
                var runs = await store.GetPendingFixRunsAsync(
                    Math.Clamp(limit ?? 10, 1, 100), ct);
                return TypedResults.Ok(new { items = runs, total = runs.Count });
            }

            return TypedResults.BadRequest(new { error = "status filter required" });
        });

        app.MapGet("/api/v1/fix-runs/{runId}", static async Task<IResult> (
            string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            return run is not null ? TypedResults.Ok(run) : TypedResults.NotFound();
        });

        // Autofix steps — create and update
        app.MapPost("/api/v1/fix-runs/{runId}/steps", static async (
            string runId, AutofixStepRecord step,
            DuckDbStore store, CancellationToken ct) =>
        {
            await store.InsertAutofixStepAsync(step with { RunId = runId }, ct);
            return TypedResults.Created($"/api/v1/fix-runs/{runId}/steps/{step.StepId}", step);
        });

        app.MapPatch("/api/v1/fix-runs/{runId}/steps/{stepId}", static async (
            string runId, string stepId,
            AutofixStepPatchRequest request,
            DuckDbStore store, CancellationToken ct) =>
        {
            await store.UpdateAutofixStepAsync(
                stepId, request.Status ?? "completed",
                request.OutputJson, errorMessage: request.ErrorMessage, ct: ct);
            return TypedResults.NoContent();
        });

        // Triage — link fix run
        app.MapPatch("/api/v1/triage/{triageId}", static async (
            string triageId, TriagePatchRequest request,
            DuckDbStore store, CancellationToken ct) =>
        {
            if (request.FixRunId is not null)
                await store.UpdateTriageFixRunAsync(triageId, request.FixRunId, ct);
            return TypedResults.NoContent();
        });

        // Untriaged issues
        app.MapGet("/api/v1/issues/untriaged", static async (
            int? limit, DuckDbStore store, CancellationToken ct) =>
        {
            var ids = await store.GetUntriagedIssueIdsAsync(
                Math.Clamp(limit ?? 20, 1, 100), ct);
            return TypedResults.Ok(new { ids });
        });

        // Deployments since timestamp
        app.MapGet("/api/v1/deployments", static async (
            DateTime? since, DuckDbStore store, CancellationToken ct) =>
        {
            var deployments = await store.GetDeploymentsAfterAsync(
                since ?? TimeProvider.System.GetUtcNow().UtcDateTime.AddHours(-1), ct);
            return TypedResults.Ok(new { items = deployments });
        });
    }
}

public sealed record AutofixStepPatchRequest(
    string? Status = null, string? OutputJson = null, string? ErrorMessage = null);

public sealed record TriagePatchRequest(string? FixRunId = null);
