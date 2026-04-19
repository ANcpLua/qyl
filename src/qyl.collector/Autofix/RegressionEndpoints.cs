namespace Qyl.Collector.Autofix;

/// <summary>
///     REST endpoints for regression detection data.
///     Active regression scanning is owned by qyl.loom.
/// </summary>
public static class RegressionEndpoints
{
    [QylMapEndpoints]
    public static void MapRegressionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/regressions/check/{serviceName}", static async (
            string serviceName, string? version,
            DuckDbStore store, CancellationToken ct) =>
        {
            var regressedIssueIds = await store
                .DetectRegressionsAsync(serviceName, version, ct).ConfigureAwait(false);

            return TypedResults.Ok(new { regressedIssueIds, count = regressedIssueIds.Count });
        });

        app.MapGet("/api/v1/regressions", static async (
            int? limit, DateTime? since,
            DuckDbStore store, CancellationToken ct) =>
        {
            var items = await store
                .GetRegressionEventsAsync(Math.Clamp(limit ?? 50, 1, 1000), since, ct).ConfigureAwait(false);

            return TypedResults.Ok(new
            {
                items = items.Select(static r => new
                {
                    r.EventId, r.IssueId, r.OldValue, r.NewValue, r.Reason, r.CreatedAt
                }),
                total = items.Count
            });
        });

        app.MapGet("/api/v1/issues/{issueId}/regressions", static async (
            string issueId, int? limit,
            DuckDbStore store, CancellationToken ct) =>
        {
            var items = await store
                .GetIssueRegressionEventsAsync(issueId, Math.Clamp(limit ?? 20, 1, 100), ct).ConfigureAwait(false);

            return TypedResults.Ok(new
            {
                items = items.Select(static r => new
                {
                    r.EventId, r.IssueId, r.OldValue, r.NewValue, r.Reason, r.CreatedAt
                }),
                total = items.Count
            });
        });
    }
}
