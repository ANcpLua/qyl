using Qyl.Contracts.Primitives;

namespace Qyl.Collector.Search;

/// <summary>
///     Builds cross-entity DuckDB queries using UNION ALL with time-bounded text search.
/// </summary>
internal static class UnifiedQueryEngine
{
    // Each subquery returns: entity_type, entity_id, title, snippet, ts (as UBIGINT unix nano), score
    // Score: 2.0 for exact name match, 1.0 for partial match in secondary columns

    private const string SpanSubquery = """
                                        SELECT
                                            'spans' AS entity_type,
                                            span_id AS entity_id,
                                            name AS title,
                                            COALESCE(service_name, '') AS snippet,
                                            start_time_unix_nano AS ts,
                                            CASE WHEN name ILIKE $1 ESCAPE '\' THEN 2.0
                                                 WHEN COALESCE(service_name, '') ILIKE $1 ESCAPE '\' THEN 1.5
                                                 ELSE 1.0 END AS score
                                        FROM spans
                                        WHERE (name ILIKE $1 ESCAPE '\'
                                            OR COALESCE(service_name, '') ILIKE $1 ESCAPE '\')
                                          AND start_time_unix_nano >= $2
                                          AND start_time_unix_nano <= $3
                                        """;

    private const string LogSubquery = """
                                       SELECT
                                           'logs' AS entity_type,
                                           log_id AS entity_id,
                                           COALESCE(severity_text, 'LOG') AS title,
                                           COALESCE(body, '') AS snippet,
                                           time_unix_nano AS ts,
                                           CASE WHEN COALESCE(body, '') ILIKE $1 ESCAPE '\' THEN 2.0
                                                WHEN COALESCE(service_name, '') ILIKE $1 ESCAPE '\' THEN 1.5
                                                ELSE 1.0 END AS score
                                       FROM logs
                                       WHERE (COALESCE(body, '') ILIKE $1 ESCAPE '\'
                                           OR COALESCE(service_name, '') ILIKE $1 ESCAPE '\')
                                         AND time_unix_nano >= $2
                                         AND time_unix_nano <= $3
                                       """;

    private const string ErrorSubquery = """
                                         SELECT
                                             'errors' AS entity_type,
                                             id AS entity_id,
                                             COALESCE(title, COALESCE(error_type, 'Issue')) AS title,
                                             COALESCE(culprit, '') AS snippet,
                                             CAST(epoch_ns(last_seen_at) AS UBIGINT) AS ts,
                                             CASE WHEN COALESCE(title, '') ILIKE $1 ESCAPE '\' THEN 2.0
                                                  WHEN COALESCE(error_type, '') ILIKE $1 ESCAPE '\' THEN 1.5
                                                  WHEN COALESCE(culprit, '') ILIKE $1 ESCAPE '\' THEN 1.25
                                                  ELSE 1.0 END AS score
                                         FROM error_issues
                                         WHERE (COALESCE(title, '') ILIKE $1 ESCAPE '\'
                                             OR COALESCE(error_type, '') ILIKE $1 ESCAPE '\'
                                             OR COALESCE(culprit, '') ILIKE $1 ESCAPE '\')
                                           AND CAST(epoch_ns(last_seen_at) AS UBIGINT) >= $2
                                           AND CAST(epoch_ns(last_seen_at) AS UBIGINT) <= $3
                                         """;

    private const string AgentRunSubquery = """
                                            SELECT
                                                'agent_runs' AS entity_type,
                                                run_id AS entity_id,
                                                COALESCE(agent_name, 'Agent') AS title,
                                                COALESCE(model, '') AS snippet,
                                                COALESCE(start_time, 0) AS ts,
                                                CASE WHEN COALESCE(agent_name, '') ILIKE $1 ESCAPE '\' THEN 2.0
                                                     WHEN COALESCE(model, '') ILIKE $1 ESCAPE '\' THEN 1.5
                                                     ELSE 1.0 END AS score
                                            FROM agent_runs
                                            WHERE (COALESCE(agent_name, '') ILIKE $1 ESCAPE '\'
                                                OR COALESCE(model, '') ILIKE $1 ESCAPE '\')
                                              AND COALESCE(start_time, 0) >= $2
                                              AND COALESCE(start_time, 0) <= $3
                                            """;

    private const string WorkflowSubquery = """
                                            SELECT
                                                'workflows' AS entity_type,
                                                workflow_result.entity_id,
                                                workflow_result.title,
                                                workflow_result.snippet,
                                                workflow_result.ts,
                                                workflow_result.score
                                            FROM (
                                                SELECT
                                                    execution_id AS entity_id,
                                                    COALESCE(workflow_name, 'Workflow') AS title,
                                                    COALESCE(status, '') AS snippet,
                                                    COALESCE(CAST(epoch_ns(started_at) AS UBIGINT), 0) AS ts,
                                                    CASE WHEN COALESCE(workflow_name, '') ILIKE $1 ESCAPE '\' THEN 2.0
                                                         WHEN COALESCE(status, '') ILIKE $1 ESCAPE '\' THEN 1.5
                                                         ELSE 1.0 END AS score
                                                FROM workflow_executions
                                                WHERE (COALESCE(workflow_name, '') ILIKE $1 ESCAPE '\'
                                                    OR COALESCE(status, '') ILIKE $1 ESCAPE '\')
                                                  AND COALESCE(CAST(epoch_ns(started_at) AS UBIGINT), 0) >= $2
                                                  AND COALESCE(CAST(epoch_ns(started_at) AS UBIGINT), 0) <= $3

                                                UNION ALL

                                                SELECT
                                                    id AS entity_id,
                                                    COALESCE(workflow_id, 'Workflow') AS title,
                                                    COALESCE(status, '') AS snippet,
                                                    COALESCE(CAST(epoch_ns(COALESCE(started_at, created_at)) AS UBIGINT), 0) AS ts,
                                                    CASE WHEN COALESCE(workflow_id, '') ILIKE $1 ESCAPE '\' THEN 2.0
                                                         WHEN COALESCE(status, '') ILIKE $1 ESCAPE '\' THEN 1.5
                                                         WHEN COALESCE(trigger_type, '') ILIKE $1 ESCAPE '\' THEN 1.25
                                                         ELSE 1.0 END AS score
                                                FROM workflow_runs
                                                WHERE (COALESCE(workflow_id, '') ILIKE $1 ESCAPE '\'
                                                    OR COALESCE(status, '') ILIKE $1 ESCAPE '\'
                                                    OR COALESCE(trigger_type, '') ILIKE $1 ESCAPE '\')
                                                  AND COALESCE(CAST(epoch_ns(COALESCE(started_at, created_at)) AS UBIGINT), 0) >= $2
                                                  AND COALESCE(CAST(epoch_ns(COALESCE(started_at, created_at)) AS UBIGINT), 0) <= $3
                                            ) AS workflow_result
                                            """;

    /// <summary>Default time window when no explicit range is specified (24 hours).</summary>
    private static readonly TimeSpan s_defaultWindow = TimeSpan.FromHours(24);

    private static readonly FrozenSet<string> s_validEntityTypes = FrozenSet.ToFrozenSet(
        ["spans", "logs", "errors", "agent_runs", "workflows"]);

    /// <summary>
    ///     Builds a UNION ALL query across requested entity types, applying text and time filters.
    /// </summary>
    /// <returns>The SQL text and ordered list of parameters.</returns>
    public static (string Sql, List<DuckDBParameter> Parameters) BuildQuery(SearchQuery query)
    {
        var entityTypes = ResolveEntityTypes(query.EntityTypes);
        var (startNano, endNano) = ResolveTimeWindow(query.StartTime, query.EndTime);
        var clampedLimit = Math.Clamp(query.Limit, 1, 200);
        var searchText = $"%{SqlLikeEscape.Escape(query.Text)}%";

        var parameters = new List<DuckDBParameter>
        {
            new() { Value = searchText }, new() { Value = (decimal)startNano }, new() { Value = (decimal)endNano }
        };

        // $1 = search text, $2 = start nano, $3 = end nano
        var unions = new List<string>();

        if (entityTypes.Contains("spans"))
            unions.Add(SpanSubquery);

        if (entityTypes.Contains("logs"))
            unions.Add(LogSubquery);

        if (entityTypes.Contains("errors"))
            unions.Add(ErrorSubquery);

        if (entityTypes.Contains("agent_runs"))
            unions.Add(AgentRunSubquery);

        if (entityTypes.Contains("workflows"))
            unions.Add(WorkflowSubquery);

        var unionSql = string.Join("\nUNION ALL\n", unions);

        var sql = $"""
                   SELECT entity_type, entity_id, title, snippet, ts, score
                   FROM (
                   {unionSql}
                   ) AS combined
                   ORDER BY score DESC, ts DESC
                   LIMIT {clampedLimit}
                   """;

        return (sql, parameters);
    }

    private static HashSet<string> ResolveEntityTypes(string[]? requested)
    {
        if (requested is null or { Length: 0 })
            return [.. s_validEntityTypes];

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in requested)
        {
            var lower = t.ToLowerInvariant();
            if (s_validEntityTypes.Contains(lower))
                result.Add(lower);
        }

        return result.Count > 0 ? result : [.. s_validEntityTypes];
    }

    private static (ulong Start, ulong End) ResolveTimeWindow(DateTime? start, DateTime? end)
    {
        var now = TimeProvider.System.GetUtcNow();
        var endDto = end.HasValue ? new DateTimeOffset(end.Value, TimeSpan.Zero) : now;
        var startDto = start.HasValue ? new DateTimeOffset(start.Value, TimeSpan.Zero) : endDto - s_defaultWindow;

        return (TimeConversions.ToUnixNanoUnsigned(startDto), TimeConversions.ToUnixNanoUnsigned(endDto));
    }
}
