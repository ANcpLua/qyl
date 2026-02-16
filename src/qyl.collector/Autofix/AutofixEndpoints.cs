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
            var issue = await orchestrator.Store.GetIssueByIdAsync(issueId, ct);
            if (issue is null)
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
    }
}

/// <summary>Request body for POST /api/v1/issues/{issueId}/fix-runs.</summary>
public sealed record FixRunRequest(string? Policy = null);
