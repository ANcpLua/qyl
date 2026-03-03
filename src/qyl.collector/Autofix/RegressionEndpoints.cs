namespace qyl.collector.Autofix;

/// <summary>
///     REST endpoints for manually triggering regression checks and
///     querying regression events from the <c>issue_events</c> table.
/// </summary>
public static class RegressionEndpoints
{
    public static void MapRegressionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/regressions/check/{serviceName}", static async (
            string serviceName, string? version,
            DuckDbStore store, TriagePipelineService triagePipeline,
            CancellationToken ct) =>
        {
            IReadOnlyList<string> regressedIssueIds = await store
                .DetectRegressionsAsync(serviceName, version, ct).ConfigureAwait(false);

            if (regressedIssueIds.Count > 0)
                await triagePipeline.TriageUntriagedIssuesAsync(ct).ConfigureAwait(false);

            return Results.Ok(new { regressedIssueIds, count = regressedIssueIds.Count });
        });

        app.MapGet("/api/v1/regressions", static async (
            int? limit, DateTime? since,
            DuckDbStore store, CancellationToken ct) =>
        {
            int clampedLimit = Math.Clamp(limit ?? 50, 1, 1000);
            IReadOnlyList<RegressionEventRow> items = await store
                .GetRegressionEventsAsync(clampedLimit, since, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                items = items.Select(static r => new
                {
                    eventId = r.EventId,
                    issueId = r.IssueId,
                    oldValue = r.OldValue,
                    newValue = r.NewValue,
                    reason = r.Reason,
                    createdAt = r.CreatedAt
                }),
                total = items.Count
            });
        });

        app.MapGet("/api/v1/issues/{issueId}/regressions", static async (
            string issueId, int? limit,
            DuckDbStore store, CancellationToken ct) =>
        {
            int clampedLimit = Math.Clamp(limit ?? 20, 1, 100);
            IReadOnlyList<RegressionEventRow> items = await store
                .GetIssueRegressionEventsAsync(issueId, clampedLimit, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                items = items.Select(static r => new
                {
                    eventId = r.EventId,
                    issueId = r.IssueId,
                    oldValue = r.OldValue,
                    newValue = r.NewValue,
                    reason = r.Reason,
                    createdAt = r.CreatedAt
                }),
                total = items.Count
            });
        });
    }
}
