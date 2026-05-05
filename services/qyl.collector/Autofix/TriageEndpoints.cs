namespace Qyl.Collector.Autofix;

public static class TriageEndpoints
{
    [QylMapEndpoints]
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

        app.MapGet("/api/v1/issues/untriaged", static async (
            int? limit, DuckDbStore store, CancellationToken ct) =>
        {
            var ids = await store.GetUntriagedIssueIdsAsync(
                Math.Clamp(limit ?? 100, 1, 1000), ct);
            return TypedResults.Ok(new { items = ids, total = ids.Count });
        });
    }
}
