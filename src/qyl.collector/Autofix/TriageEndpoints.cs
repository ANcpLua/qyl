namespace Qyl.Collector.Autofix;

/// <summary>
///     REST endpoints for querying and triggering triage assessments.
/// </summary>
public static class TriageEndpoints
{
    public static void MapTriageEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/triage", static async (
            string? automationLevel, int? limit,
            DuckDbStore store, CancellationToken ct) =>
        {
            var results = await store.GetTriageResultsAsync(
                automationLevel, Math.Clamp(limit ?? 50, 1, 1000), ct);
            return TypedResults.Ok(new { items = results, total = results.Count });
        });

        app.MapGet("/api/v1/triage/{triageId}", static async Task<IResult> (
            string triageId, DuckDbStore store, CancellationToken ct) =>
        {
            var result = await store.GetTriageResultAsync(triageId, ct);
            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        });

        app.MapGet("/api/v1/issues/{issueId}/triage", static async Task<IResult> (
            string issueId, DuckDbStore store, CancellationToken ct) =>
        {
            var result = await store.GetLatestTriageForIssueAsync(issueId, ct);
            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        });

        app.MapPost("/api/v1/issues/{issueId}/triage", static async Task<IResult> (
            string issueId, TriagePipelineService pipeline,
            DuckDbStore store, CancellationToken ct) =>
        {
            if (await store.GetIssueByIdAsync(issueId, ct) is null)
                return TypedResults.NotFound();

            // Triage only this specific issue (no side effects on other issues)
            var result = await pipeline.TriageSingleIssueAsync(issueId, ct);
            return result is null
                ? TypedResults.Problem("Triage failed to produce a result")
                : TypedResults.Created($"/api/v1/triage/{result.TriageId}", result);
        });
    }
}
