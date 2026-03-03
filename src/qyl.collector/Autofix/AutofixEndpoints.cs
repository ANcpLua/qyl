namespace qyl.collector.Autofix;

/// <summary>
///     REST endpoints for triggering and querying autofix runs against grouped issues.
/// </summary>
public static class AutofixEndpoints
{
    public static void MapAutofixEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/issues/{issueId}/fix-runs", static async (
            string issueId, FixRunRequest request,
            AutofixOrchestrator orchestrator, CancellationToken ct) =>
        {
            if (await orchestrator.Store.GetIssueByIdAsync(issueId, ct) is not { } issue)
                return Results.NotFound();

            if (!Enum.TryParse<FixPolicy>(request.Policy, true, out var policy))
                policy = FixPolicy.RequireReview;

            var run = await orchestrator.CreateFixRunAsync(issueId, issue, policy, ct);
            return Results.Created($"/api/v1/issues/{issueId}/fix-runs/{run.RunId}", run);
        });

        app.MapGet("/api/v1/issues/{issueId}/fix-runs", static async (
            string issueId, DuckDbStore store, int? limit, CancellationToken ct) =>
        {
            var runs = await store.GetFixRunsAsync(issueId, Math.Clamp(limit ?? 50, 1, 1000), ct);
            return Results.Ok(new { items = runs, total = runs.Count });
        });

        app.MapGet("/api/v1/issues/{issueId}/fix-runs/{runId}", static async (
            string issueId, string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return Results.NotFound();

            return Results.Ok(run);
        });

        app.MapPost("/api/v1/issues/{issueId}/fix-runs/{runId}/pr", static async (
            string issueId, string runId,
            PrCreationRequest request,
            PrCreationService prService, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(request.Repo))
                return Results.BadRequest(new { error = "repo is required (e.g. 'owner/my-repo')" });

            var result = await prService.CreatePrAsync(runId, request.Repo, request.BaseBranch, ct);
            return result.Success
                ? Results.Ok(new { prUrl = result.PrUrl })
                : Results.UnprocessableEntity(new { error = result.Error });
        });

        app.MapPatch("/api/v1/issues/{issueId}/fix-runs/{runId}", static async (
            string issueId, string runId,
            FixRunPatchRequest request,
            AutofixOrchestrator orchestrator, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return Results.NotFound();

            await orchestrator.UpdateFixRunStatusAsync(
                runId,
                request.Status ?? run.Status,
                request.Description,
                request.Confidence,
                request.ChangesJson,
                ct);

            return Results.NoContent();
        });

        // ── Steps ────────────────────────────────────────────────────────────

        app.MapGet("/api/v1/issues/{issueId}/fix-runs/{runId}/steps", static async (
            string issueId, string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return Results.NotFound();

            var steps = await store.GetAutofixStepsAsync(runId, ct);
            return Results.Ok(new { items = steps, total = steps.Count });
        });

        // ── Approve / Reject ─────────────────────────────────────────────────

        app.MapPost("/api/v1/issues/{issueId}/fix-runs/{runId}/approve", static async (
            string issueId, string runId,
            AutofixOrchestrator orchestrator, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return Results.NotFound();

            if (run.Status is not "review")
                return Results.BadRequest(new { error = $"Cannot approve fix run in status '{run.Status}'. Must be 'review'." });

            await orchestrator.UpdateFixRunStatusAsync(runId, "applied", ct: ct);
            return Results.Ok(new { status = "applied", runId });
        });

        app.MapPost("/api/v1/issues/{issueId}/fix-runs/{runId}/reject", static async (
            string issueId, string runId,
            FixRunRejectRequest? request,
            AutofixOrchestrator orchestrator, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return Results.NotFound();

            if (run.Status is not "review")
                return Results.BadRequest(new { error = $"Cannot reject fix run in status '{run.Status}'. Must be 'review'." });

            await orchestrator.UpdateFixRunStatusAsync(
                runId, "rejected", description: request?.Reason, ct: ct);
            return Results.Ok(new { status = "rejected", runId });
        });
    }
}

/// <summary>Request body for POST /api/v1/issues/{issueId}/fix-runs.</summary>
public sealed record FixRunRequest(string? Policy = null);

/// <summary>Request body for POST /api/v1/issues/{issueId}/fix-runs/{runId}/pr.</summary>
public sealed record PrCreationRequest(
    string Repo,
    string? BaseBranch = null);

/// <summary>Request body for PATCH /api/v1/issues/{issueId}/fix-runs/{runId}.</summary>
public sealed record FixRunPatchRequest(
    string? Status = null,
    string? Description = null,
    double? Confidence = null,
    string? ChangesJson = null);

/// <summary>Request body for POST /api/v1/issues/{issueId}/fix-runs/{runId}/reject.</summary>
public sealed record FixRunRejectRequest(string? Reason = null);
