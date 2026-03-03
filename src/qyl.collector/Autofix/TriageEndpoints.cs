namespace qyl.collector.Autofix;

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
            return Results.Ok(new { items = results, total = results.Count });
        });

        app.MapGet("/api/v1/triage/{triageId}", static async (
            string triageId, DuckDbStore store, CancellationToken ct) =>
        {
            var result = await store.GetTriageResultAsync(triageId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapGet("/api/v1/issues/{issueId}/triage", static async (
            string issueId, DuckDbStore store, CancellationToken ct) =>
        {
            var result = await store.GetLatestTriageForIssueAsync(issueId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapPost("/api/v1/issues/{issueId}/triage", static async (
            string issueId, TriagePipelineService pipeline,
            DuckDbStore store, CancellationToken ct) =>
        {
            if (await store.GetIssueByIdAsync(issueId, ct) is null)
                return Results.NotFound();

            // Triage this single issue immediately (bypasses the polling loop)
            await pipeline.TriageUntriagedIssuesAsync(ct);

            var result = await store.GetLatestTriageForIssueAsync(issueId, ct);
            return result is null
                ? Results.Problem("Triage failed to produce a result")
                : Results.Created($"/api/v1/triage/{result.TriageId}", result);
        });
    }
}
