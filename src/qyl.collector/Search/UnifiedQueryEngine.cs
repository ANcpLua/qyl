namespace qyl.collector.Search;

/// <summary>
///     Builds cross-entity DuckDB queries using UNION ALL with time-bounded text search.
/// </summary>
internal static class UnifiedQueryEngine
{
    /// <summary>Default time window when no explicit range is specified (24 hours).</summary>
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(24);

    private static readonly FrozenSet<string> ValidEntityTypes = FrozenSet.ToFrozenSet(
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
        var searchText = $"%{EscapeLike(query.Text)}%";

        var parameters = new List<DuckDBParameter>
        {
            new() { Value = searchText },
            new() { Value = (decimal)startNano },
            new() { Value = (decimal)endNano }
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
            return [.. ValidEntityTypes];

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in requested)
        {
            var lower = t.ToLowerInvariant();
            if (ValidEntityTypes.Contains(lower))
                result.Add(lower);
        }

        return result.Count > 0 ? result : [.. ValidEntityTypes];
    }

    private static (ulong Start, ulong End) ResolveTimeWindow(DateTime? start, DateTime? end)
    {
        var now = TimeProvider.System.GetUtcNow();
        var endDto = end.HasValue ? new DateTimeOffset(end.Value, TimeSpan.Zero) : now;
        var startDto = start.HasValue ? new DateTimeOffset(start.Value, TimeSpan.Zero) : endDto - DefaultWindow;

        return (ToUnixNano(startDto), ToUnixNano(endDto));
    }

    private static ulong ToUnixNano(DateTimeOffset dto) =>
        (ulong)(dto.ToUnixTimeMilliseconds() * 1_000_000L);

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    // Each subquery returns: entity_type, entity_id, title, snippet, ts (as UBIGINT unix nano), score
    // Score: 2.0 for exact name match, 1.0 for partial match in secondary columns

    private const string SpanSubquery = """
        SELECT
            'spans' AS entity_type,
            span_id AS entity_id,
            name AS title,
            COALESCE(service_name, '') AS snippet,
            start_time_unix_nano AS ts,
            CASE WHEN name ILIKE $1 ESCAPE '\\' THEN 2.0
                 WHEN COALESCE(service_name, '') ILIKE $1 ESCAPE '\\' THEN 1.5
                 ELSE 1.0 END AS score
        FROM spans
        WHERE (name ILIKE $1 ESCAPE '\\'
            OR COALESCE(service_name, '') ILIKE $1 ESCAPE '\\')
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
            CASE WHEN COALESCE(body, '') ILIKE $1 ESCAPE '\\' THEN 2.0
                 WHEN COALESCE(service_name, '') ILIKE $1 ESCAPE '\\' THEN 1.5
                 ELSE 1.0 END AS score
        FROM logs
        WHERE (COALESCE(body, '') ILIKE $1 ESCAPE '\\'
            OR COALESCE(service_name, '') ILIKE $1 ESCAPE '\\')
          AND time_unix_nano >= $2
          AND time_unix_nano <= $3
        """;

    private const string ErrorSubquery = """
        SELECT
            'errors' AS entity_type,
            error_id AS entity_id,
            COALESCE(error_type, 'Error') AS title,
            COALESCE(message, '') AS snippet,
            CAST(epoch_ns(last_seen) AS UBIGINT) AS ts,
            CASE WHEN COALESCE(message, '') ILIKE $1 ESCAPE '\\' THEN 2.0
                 WHEN COALESCE(error_type, '') ILIKE $1 ESCAPE '\\' THEN 1.5
                 ELSE 1.0 END AS score
        FROM errors
        WHERE (COALESCE(message, '') ILIKE $1 ESCAPE '\\'
            OR COALESCE(error_type, '') ILIKE $1 ESCAPE '\\')
          AND CAST(epoch_ns(last_seen) AS UBIGINT) >= $2
          AND CAST(epoch_ns(last_seen) AS UBIGINT) <= $3
        """;

    private const string AgentRunSubquery = """
        SELECT
            'agent_runs' AS entity_type,
            run_id AS entity_id,
            COALESCE(agent_name, 'Agent') AS title,
            COALESCE(model, '') AS snippet,
            COALESCE(start_time, 0) AS ts,
            CASE WHEN COALESCE(agent_name, '') ILIKE $1 ESCAPE '\\' THEN 2.0
                 WHEN COALESCE(model, '') ILIKE $1 ESCAPE '\\' THEN 1.5
                 ELSE 1.0 END AS score
        FROM agent_runs
        WHERE (COALESCE(agent_name, '') ILIKE $1 ESCAPE '\\'
            OR COALESCE(model, '') ILIKE $1 ESCAPE '\\')
          AND COALESCE(start_time, 0) >= $2
          AND COALESCE(start_time, 0) <= $3
        """;

    private const string WorkflowSubquery = """
        SELECT
            'workflows' AS entity_type,
            execution_id AS entity_id,
            COALESCE(workflow_name, 'Workflow') AS title,
            COALESCE(status, '') AS snippet,
            COALESCE(start_time_unix_nano, 0) AS ts,
            CASE WHEN COALESCE(workflow_name, '') ILIKE $1 ESCAPE '\\' THEN 2.0
                 WHEN COALESCE(status, '') ILIKE $1 ESCAPE '\\' THEN 1.5
                 ELSE 1.0 END AS score
        FROM workflow_executions
        WHERE (COALESCE(workflow_name, '') ILIKE $1 ESCAPE '\\'
            OR COALESCE(status, '') ILIKE $1 ESCAPE '\\')
          AND COALESCE(start_time_unix_nano, 0) >= $2
          AND COALESCE(start_time_unix_nano, 0) <= $3
        """;
}
