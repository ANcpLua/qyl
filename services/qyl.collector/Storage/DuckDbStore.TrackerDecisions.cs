namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{
    // OTLP-to-JSON in the existing collector pipeline corrupts numeric span attributes
    // (sent int N comes back as zigzag-decoded form). Until that is fixed upstream, the
    // tracker-decision queries treat each span as one observation (COUNT(*)) and only
    // use the blocked attribute as a non-zero / zero indicator, which survives the
    // corruption: blocked=0 stays "0", blocked>0 becomes any non-"0" string.
    private const string TrackerDecisionSpanFilter = """
                                                     attributes_json IS NOT NULL
                                                     AND json_extract_string(attributes_json, '$."qyl.tracker.host"') IS NOT NULL
                                                     AND start_time_unix_nano >= $1
                                                     """;

    private const string TrackerDecisionInnerProjection = """
                                                          json_extract_string(attributes_json, '$."qyl.tracker.host"')   AS host,
                                                          CASE
                                                              WHEN json_extract_string(attributes_json, '$."qyl.tracker.blocked"') IS NULL
                                                                   OR json_extract_string(attributes_json, '$."qyl.tracker.blocked"') IN ('0', 'false', 'False')
                                                                  THEN 0
                                                              ELSE 1
                                                          END                                                              AS blocked,
                                                          COALESCE(
                                                              json_extract_string(attributes_json, '$."qyl.tracker.reason"'),
                                                              json_extract_string(attributes_json, '$."qyl.tracker.last_error"')
                                                          )                                                                AS last_reason,
                                                          json_extract_string(attributes_json, '$."qyl.tracker.source"')   AS source
                                                          """;

    public async Task<IReadOnlyList<TrackerHostAggregate>> GetTopTrackerDecisionsAsync(
        string? source,
        long sinceUnixNanos,
        int top,
        CancellationToken ct = default)
    {
        var clampedTop = Math.Clamp(top, 1, 200);
        var sql = $"""
                   SELECT host,
                          COUNT(*)         AS total,
                          SUM(blocked)     AS blocked,
                          ANY_VALUE(last_reason) AS last_reason
                   FROM (
                       SELECT {TrackerDecisionInnerProjection}
                       FROM spans
                       WHERE {TrackerDecisionSpanFilter}
                   ) AS decisions
                   WHERE ($2 IS NULL OR source = $2)
                   GROUP BY host
                   ORDER BY blocked DESC, total DESC
                   LIMIT {clampedTop}
                   """;

        return await ReadManyAsync(
            sql,
            cmd =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)sinceUnixNanos });
                cmd.Parameters.Add(new DuckDBParameter
                {
                    Value = string.IsNullOrWhiteSpace(source) ? DBNull.Value : source
                });
            },
            static reader => new TrackerHostAggregate(
                Host: reader.GetString(0),
                Total: reader.Col(1).GetInt64(0),
                Blocked: reader.Col(2).GetInt64(0),
                LastReason: reader.Col(3).AsString),
            ct).ConfigureAwait(false);
    }

    public async Task<TrackerDecisionTotals> GetTrackerDecisionTotalsAsync(
        string? source,
        long sinceUnixNanos,
        CancellationToken ct = default)
    {
        var sql = $"""
                   SELECT
                       COUNT(*)          AS total_events,
                       SUM(blocked)      AS total_blocked
                   FROM (
                       SELECT {TrackerDecisionInnerProjection}
                       FROM spans
                       WHERE {TrackerDecisionSpanFilter}
                   ) AS decisions
                   WHERE ($2 IS NULL OR source = $2)
                   """;

        var result = await ReadOneAsync(
            sql,
            cmd =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)sinceUnixNanos });
                cmd.Parameters.Add(new DuckDBParameter
                {
                    Value = string.IsNullOrWhiteSpace(source) ? DBNull.Value : source
                });
            },
            static reader => new TrackerDecisionTotals(
                TotalEvents: reader.Col(0).GetInt64(0),
                TotalBlocked: reader.Col(1).GetInt64(0)),
            ct).ConfigureAwait(false);

        return result ?? new TrackerDecisionTotals(0, 0);
    }

    public async Task<IReadOnlyList<TrackerSiteDecisionRow>> GetTrackerDecisionsForHostAsync(
        string host,
        long sinceUnixNanos,
        int limit,
        CancellationToken ct = default)
    {
        var clampedLimit = Math.Clamp(limit, 1, 500);
        var sql = $"""
                   SELECT
                       json_extract_string(attributes_json, '$."qyl.tracker.host"')      AS host,
                       CASE
                           WHEN json_extract_string(attributes_json, '$."qyl.tracker.blocked"') IS NULL
                                OR json_extract_string(attributes_json, '$."qyl.tracker.blocked"') IN ('0', 'false', 'False')
                               THEN 'observed'
                           ELSE 'blocked'
                       END                                                                AS decision,
                       COALESCE(
                           json_extract_string(attributes_json, '$."qyl.tracker.reason"'),
                           json_extract_string(attributes_json, '$."qyl.tracker.last_error"')
                       )                                                                  AS reason,
                       start_time_unix_nano                                                AS occurred_at
                   FROM spans
                   WHERE {TrackerDecisionSpanFilter}
                     AND json_extract_string(attributes_json, '$."qyl.tracker.host"') = $2
                   ORDER BY start_time_unix_nano DESC
                   LIMIT {clampedLimit}
                   """;

        return await ReadManyAsync(
            sql,
            cmd =>
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)sinceUnixNanos });
                cmd.Parameters.Add(new DuckDBParameter { Value = host });
            },
            static reader => new TrackerSiteDecisionRow(
                Host: reader.GetString(0),
                Decision: reader.GetString(1),
                Reason: reader.Col(2).AsString,
                OccurredAtUnixNanos: reader.Col(3).AsUInt64 ?? 0UL),
            ct).ConfigureAwait(false);
    }
}

public sealed record TrackerHostAggregate(string Host, long Total, long Blocked, string? LastReason);

public sealed record TrackerDecisionTotals(long TotalEvents, long TotalBlocked);

public sealed record TrackerSiteDecisionRow(string Host, string Decision, string? Reason, ulong OccurredAtUnixNanos);
