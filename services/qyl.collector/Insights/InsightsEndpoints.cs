namespace Qyl.Collector.Insights;

public sealed record InsightsResponse(
    string Markdown,
    DateTimeOffset? LastUpdated,
    IReadOnlyList<InsightTierStatus> Tiers);

public sealed record InsightTierStatus(
    string Tier,
    string Hash,
    DateTimeOffset? MaterializedAt,
    double DurationMs);

internal static class InsightsEndpoints
{
    [QylMapEndpoints]
    public static void MapInsightsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/insights", async (DuckDbStore store, CancellationToken ct) =>
        {
            var rows = await store.GetAllInsightsAsync(ct).ConfigureAwait(false);
            if (rows.Count is 0)
                return TypedResults.Ok(new InsightsResponse("No insights materialized yet.", null, []));

            var markdown = string.Join("\n\n", rows.Select(r => r.ContentMarkdown));
            var lastUpdated = rows.Max(r => r.MaterializedAt);
            var tiers = rows.Select(r => new InsightTierStatus(
                r.Tier, r.ContentHash, r.MaterializedAt, r.DurationMs)).ToList();

            return TypedResults.Ok(new InsightsResponse(markdown, lastUpdated, tiers));
        });

        app.MapGet("/api/v1/insights/{tier}",
            async Task<IResult> (string tier, DuckDbStore store, CancellationToken ct) =>
            {
                var rows = await store.GetAllInsightsAsync(ct).ConfigureAwait(false);
                if (rows.FirstOrDefault(r => string.Equals(r.Tier, tier, StringComparison.OrdinalIgnoreCase)) is not
                    { } row)
                    return TypedResults.NotFound();

                var status = new InsightTierStatus(row.Tier, row.ContentHash, row.MaterializedAt, row.DurationMs);
                return TypedResults.Ok(new InsightsResponse(row.ContentMarkdown, row.MaterializedAt, [status]));
            });
    }
}
