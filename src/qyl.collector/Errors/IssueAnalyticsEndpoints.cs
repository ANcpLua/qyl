using Microsoft.AspNetCore.Mvc;

namespace qyl.collector.Errors;

/// <summary>
///     Minimal API endpoints for error issue timeline and similarity search.
///     Routes: <c>/api/v1/issues/*</c> (under the Issue Analytics tag).
/// </summary>
public static class IssueAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapIssueAnalyticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/issues")
            .WithTags("Issue Analytics");

        group.MapGet("/{issueId}/timeline", GetTimelineAsync)
            .WithName("GetIssueTimeline")
            .WithSummary("Get error occurrence timeline for an issue");

        group.MapGet("/similar", FindSimilarAsync)
            .WithName("FindSimilarSpans")
            .WithSummary("Find spans similar to a given span via cluster proximity");

        return endpoints;
    }

    private static async Task<IResult> GetTimelineAsync(
        string issueId,
        [FromServices] DuckDbStore store,
        int? bucketMinutes,
        int? hours,
        CancellationToken ct)
    {
        int clampedBucket = Math.Clamp(bucketMinutes ?? 60, 1, 1440);
        int clampedHours = Math.Clamp(hours ?? 24, 1, 720);

        DateTimeOffset cutoff = TimeProvider.System.GetUtcNow().AddHours(-clampedHours);

        await using DuckDbStore.ReadLease lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using DuckDBCommand cmd = lease.Connection.CreateCommand();

        cmd.CommandText = $"""
                           SELECT time_bucket(INTERVAL '{clampedBucket} minutes', timestamp) AS bucket,
                                  COUNT(*) AS count
                           FROM error_issue_events
                           WHERE issue_id = $1
                             AND timestamp >= $2
                           GROUP BY bucket
                           ORDER BY bucket ASC
                           """;

        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
        cmd.Parameters.Add(new DuckDBParameter { Value = cutoff.UtcDateTime });

        List<TimelineBucket> buckets = [];
        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            buckets.Add(new TimelineBucket(reader.GetDateTime(0), (int)reader.GetInt64(1)));
        }

        return Results.Ok(new { items = buckets, total = buckets.Count });
    }

    private static async Task<IResult> FindSimilarAsync(
        [FromServices] DuckDbStore store,
        string spanId,
        int? limit,
        CancellationToken ct)
    {
        int clampedLimit = Math.Clamp(limit ?? 10, 1, 100);

        await using DuckDbStore.ReadLease lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using DuckDBCommand cmd = lease.Connection.CreateCommand();

        cmd.CommandText = """
                          SELECT sc2.span_id, sc2.cluster_label, sc2.distance,
                                 ABS(sc1.distance - sc2.distance) AS similarity_score
                          FROM span_clusters sc1
                          JOIN span_clusters sc2 ON sc1.cluster_id = sc2.cluster_id AND sc1.span_id != sc2.span_id
                          WHERE sc1.span_id = $1
                          ORDER BY similarity_score ASC
                          LIMIT $2
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = spanId });
        cmd.Parameters.Add(new DuckDBParameter { Value = clampedLimit });

        List<SimilarSpan> results = [];
        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new SimilarSpan(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDouble(2),
                reader.GetDouble(3)));
        }

        return Results.Ok(new { items = results, total = results.Count });
    }
}

// =============================================================================
// Response Types
// =============================================================================

public sealed record TimelineBucket(DateTime Bucket, int Count);

public sealed record SimilarSpan(string SpanId, string ClusterLabel, double Distance, double SimilarityScore);
