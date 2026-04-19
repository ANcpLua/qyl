namespace Qyl.Collector.Autofix;

/// <summary>
///     REST endpoints for fix runs. Collector stores fix run data —
///     orchestration (RCA, diff generation, confidence scoring) is owned by qyl.loom.
/// </summary>
public static class AutofixEndpoints
{
    [QylMapEndpoints]
    public static void MapAutofixEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/issues/{issueId}/fix-runs", static async Task<IResult> (
            string issueId, FixRunRequest request,
            DuckDbStore store, CancellationToken ct) =>
        {
            if (await store.GetIssueByIdAsync(issueId, ct) is null)
                return TypedResults.NotFound();

            if (!Enum.TryParse<FixPolicy>(request.Policy, true, out var policy))
                policy = FixPolicy.RequireReview;

            var run = new FixRunRecord
            {
                RunId = Guid.NewGuid().ToString("N"),
                IssueId = issueId,
                Status = "pending",
                Policy = policy.ToString(),
                Instruction = request.Instruction,
                StoppingPoint = request.StoppingPoint
            };
            await store.InsertFixRunAsync(run, ct);
            return TypedResults.Created($"/api/v1/issues/{issueId}/fix-runs/{run.RunId}", run);
        });

        app.MapGet("/api/v1/issues/{issueId}/fix-runs", static async (
            string issueId, DuckDbStore store, int? limit, CancellationToken ct) =>
        {
            var runs = await store.GetFixRunsAsync(issueId, Math.Clamp(limit ?? 50, 1, 1000), ct);
            return TypedResults.Ok(new { items = runs, total = runs.Count });
        });

        app.MapGet("/api/v1/issues/{issueId}/fix-runs/{runId}", static async Task<IResult> (
            string issueId, string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return TypedResults.NotFound();

            return TypedResults.Ok(run);
        });

        app.MapPost("/api/v1/issues/{issueId}/fix-runs/{runId}/pr", static async Task<IResult> (
            string issueId, string runId,
            PrCreationRequest request,
            PrCreationService prService, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return TypedResults.NotFound();

            if (string.IsNullOrWhiteSpace(request.Repo))
                return TypedResults.BadRequest(new { error = "repo is required (e.g. 'owner/my-repo')" });

            var result = await prService.CreatePrAsync(runId, request.Repo, request.BaseBranch, ct);
            return result.Success
                ? TypedResults.Ok(new { prUrl = result.PrUrl })
                : TypedResults.UnprocessableEntity(new { error = result.Error });
        });

        app.MapPatch("/api/v1/issues/{issueId}/fix-runs/{runId}", static async Task<IResult> (
            string issueId, string runId,
            FixRunPatchRequest request,
            DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return TypedResults.NotFound();

            await store.UpdateFixRunAsync(
                runId,
                request.Status ?? run.Status,
                request.Description,
                request.Confidence,
                request.ChangesJson,
                ct);

            return TypedResults.NoContent();
        });

        app.MapGet("/api/v1/issues/{issueId}/fix-runs/{runId}/steps", static async Task<IResult> (
            string issueId, string runId, DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return TypedResults.NotFound();

            var steps = await store.GetAutofixStepsAsync(runId, ct);
            return TypedResults.Ok(new { items = steps, total = steps.Count });
        });

        app.MapPost("/api/v1/issues/{issueId}/fix-runs/{runId}/approve", static async Task<IResult> (
            string issueId, string runId,
            DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return TypedResults.NotFound();

            if (run.Status is not "review")
                return TypedResults.BadRequest(new { error = $"Cannot approve fix run in status '{run.Status}'. Must be 'review'." });

            await store.UpdateFixRunAsync(runId, "applied", ct: ct);
            return TypedResults.Ok(new { status = "applied", runId });
        });

        app.MapPost("/api/v1/issues/{issueId}/fix-runs/{runId}/reject", static async Task<IResult> (
            string issueId, string runId,
            FixRunRejectRequest? request,
            DuckDbStore store, CancellationToken ct) =>
        {
            var run = await store.GetFixRunAsync(runId, ct);
            if (run is null || run.IssueId != issueId)
                return TypedResults.NotFound();

            if (run.Status is not "review")
                return TypedResults.BadRequest(new { error = $"Cannot reject fix run in status '{run.Status}'. Must be 'review'." });

            await store.UpdateFixRunAsync(runId, "rejected", request?.Reason, ct: ct);
            return TypedResults.Ok(new { status = "rejected", runId });
        });

    }
}

public sealed record FixRunRequest(
    string? Policy = null,
    string? Instruction = null,
    string? StoppingPoint = null);

public sealed record PrCreationRequest(string Repo, string? BaseBranch = null);

public sealed record FixRunPatchRequest(
    string? Status = null, string? Description = null,
    double? Confidence = null, string? ChangesJson = null);

public sealed record FixRunRejectRequest(string? Reason = null);
