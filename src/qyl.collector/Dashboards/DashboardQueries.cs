namespace qyl.collector.Dashboards;

/// <summary>
///     Pre-built DuckDB queries for each dashboard type.
///     Returns structured widget data for frontend rendering.
/// </summary>
public static class DashboardQueries
{
    public static async Task<IReadOnlyList<DashboardWidget>> GetWidgetsAsync(
        string dashboardId, DuckDBConnection con, CancellationToken ct) =>
        dashboardId switch
        {
            "api-performance" => await ApiPerformanceAsync(con, ct).ConfigureAwait(false),
            "external-apis" => await ExternalApisAsync(con, ct).ConfigureAwait(false),
            "genai" => await GenAiAsync(con, ct).ConfigureAwait(false),
            "database" => await DatabaseAsync(con, ct).ConfigureAwait(false),
            "error-tracker" => await ErrorTrackerAsync(con, ct).ConfigureAwait(false),
            "messaging" => await MessagingAsync(con, ct).ConfigureAwait(false),
            _ => []
        };

    // =========================================================================
    // API Performance
    // =========================================================================

    private static async Task<IReadOnlyList<DashboardWidget>> ApiPerformanceAsync(
        DuckDBConnection con, CancellationToken ct)
    {
        var widgets = new List<DashboardWidget>();

        // Stat: total requests, avg latency, error rate
        var stats = await QueryStatsAsync(con, """
            SELECT
                COUNT(*) AS total_requests,
                ROUND(AVG(duration_ns / 1e6), 1) AS avg_latency_ms,
                ROUND(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS error_rate
            FROM spans
            WHERE attributes_json LIKE '%http.request.method%'
            """, ct).ConfigureAwait(false);

        if (stats.Count > 0)
        {
            widgets.Add(new DashboardWidget("api-total-requests", "Total Requests", "stat",
                new StatCardData("Requests", stats.GetValueOrDefault("total_requests", "0"))));
            widgets.Add(new DashboardWidget("api-avg-latency", "Avg Latency", "stat",
                new StatCardData("Latency", stats.GetValueOrDefault("avg_latency_ms", "0"), "ms")));
            widgets.Add(new DashboardWidget("api-error-rate", "Error Rate", "stat",
                new StatCardData("Errors", stats.GetValueOrDefault("error_rate", "0"), "%")));
        }

        // Top routes by p95 latency
        var topRoutes = await QueryTopNAsync(con, """
            SELECT
                COALESCE(
                    json_extract_string(attributes_json, '$.http.route'),
                    json_extract_string(attributes_json, '$.url.path'),
                    name
                ) AS route_name,
                ROUND(PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns / 1e6), 1) AS p95_ms,
                COUNT(*) AS call_count,
                ROUND(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS err_rate
            FROM spans
            WHERE attributes_json LIKE '%http.request.method%'
            GROUP BY route_name
            ORDER BY p95_ms DESC
            LIMIT 10
            """, "route_name", "p95_ms", "ms", "call_count", "err_rate", ct).ConfigureAwait(false);

        if (topRoutes.Count > 0)
            widgets.Add(new DashboardWidget("api-top-routes", "Top Routes by P95 Latency", "table", topRoutes));

        // Throughput over time (last 24h, 1h buckets)
        var throughput = await QueryTimeSeriesAsync(con, """
            SELECT
                strftime(time_bucket(INTERVAL '1 hour', to_timestamp(start_time_unix_nano / 1e9)), '%H:%M') AS bucket,
                COUNT(*) AS req_count
            FROM spans
            WHERE attributes_json LIKE '%http.request.method%'
              AND start_time_unix_nano >= (epoch_ns(now()) - 86400000000000)
            GROUP BY bucket
            ORDER BY bucket
            """, "bucket", "req_count", ct).ConfigureAwait(false);

        if (throughput.Count > 0)
            widgets.Add(new DashboardWidget("api-throughput", "Request Throughput (24h)", "chart", throughput));

        return widgets;
    }

    // =========================================================================
    // External APIs
    // =========================================================================

    private static async Task<IReadOnlyList<DashboardWidget>> ExternalApisAsync(
        DuckDBConnection con, CancellationToken ct)
    {
        var widgets = new List<DashboardWidget>();

        var stats = await QueryStatsAsync(con, """
            SELECT
                COUNT(*) AS total_calls,
                ROUND(AVG(duration_ns / 1e6), 1) AS avg_latency_ms,
                ROUND(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS error_rate
            FROM spans
            WHERE (attributes_json LIKE '%http.client%' OR kind = 3)
              AND attributes_json LIKE '%http.%'
            """, ct).ConfigureAwait(false);

        if (stats.Count > 0)
        {
            widgets.Add(new DashboardWidget("ext-total-calls", "Total Outbound Calls", "stat",
                new StatCardData("Calls", stats.GetValueOrDefault("total_calls", "0"))));
            widgets.Add(new DashboardWidget("ext-avg-latency", "Avg Latency", "stat",
                new StatCardData("Latency", stats.GetValueOrDefault("avg_latency_ms", "0"), "ms")));
            widgets.Add(new DashboardWidget("ext-error-rate", "Error Rate", "stat",
                new StatCardData("Errors", stats.GetValueOrDefault("error_rate", "0"), "%")));
        }

        var topHosts = await QueryTopNAsync(con, """
            SELECT
                COALESCE(
                    json_extract_string(attributes_json, '$.server.address'),
                    json_extract_string(attributes_json, '$.http.host'),
                    json_extract_string(attributes_json, '$.url.full'),
                    name
                ) AS host_name,
                ROUND(PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns / 1e6), 1) AS p95_ms,
                COUNT(*) AS call_count,
                ROUND(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS err_rate
            FROM spans
            WHERE (attributes_json LIKE '%http.client%' OR kind = 3)
              AND attributes_json LIKE '%http.%'
            GROUP BY host_name
            ORDER BY call_count DESC
            LIMIT 10
            """, "host_name", "p95_ms", "ms", "call_count", "err_rate", ct).ConfigureAwait(false);

        if (topHosts.Count > 0)
            widgets.Add(new DashboardWidget("ext-top-hosts", "Top External Hosts", "table", topHosts));

        return widgets;
    }

    // =========================================================================
    // GenAI
    // =========================================================================

    private static async Task<IReadOnlyList<DashboardWidget>> GenAiAsync(
        DuckDBConnection con, CancellationToken ct)
    {
        var widgets = new List<DashboardWidget>();

        var stats = await QueryStatsAsync(con, """
            SELECT
                COUNT(*) AS total_requests,
                COALESCE(SUM(gen_ai_input_tokens), 0) AS total_input_tokens,
                COALESCE(SUM(gen_ai_output_tokens), 0) AS total_output_tokens,
                ROUND(COALESCE(SUM(gen_ai_cost_usd), 0), 4) AS total_cost_usd,
                ROUND(AVG(duration_ns / 1e6), 1) AS avg_latency_ms
            FROM spans
            WHERE gen_ai_provider_name IS NOT NULL OR gen_ai_request_model IS NOT NULL
            """, ct).ConfigureAwait(false);

        if (stats.Count > 0)
        {
            widgets.Add(new DashboardWidget("genai-requests", "Total Requests", "stat",
                new StatCardData("Requests", stats.GetValueOrDefault("total_requests", "0"))));
            widgets.Add(new DashboardWidget("genai-tokens", "Total Tokens", "stat",
                new StatCardData("Tokens",
                    (long.Parse(stats.GetValueOrDefault("total_input_tokens", "0")) +
                     long.Parse(stats.GetValueOrDefault("total_output_tokens", "0"))).ToString())));
            widgets.Add(new DashboardWidget("genai-cost", "Total Cost", "stat",
                new StatCardData("Cost", "$" + stats.GetValueOrDefault("total_cost_usd", "0"))));
            widgets.Add(new DashboardWidget("genai-latency", "Avg Latency", "stat",
                new StatCardData("Latency", stats.GetValueOrDefault("avg_latency_ms", "0"), "ms")));
        }

        var topModels = await QueryTopNAsync(con, """
            SELECT
                COALESCE(gen_ai_request_model, gen_ai_response_model, 'unknown') AS model_name,
                ROUND(AVG(duration_ns / 1e6), 1) AS avg_ms,
                COUNT(*) AS call_count,
                ROUND(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS err_rate
            FROM spans
            WHERE gen_ai_provider_name IS NOT NULL OR gen_ai_request_model IS NOT NULL
            GROUP BY model_name
            ORDER BY call_count DESC
            LIMIT 10
            """, "model_name", "avg_ms", "ms", "call_count", "err_rate", ct).ConfigureAwait(false);

        if (topModels.Count > 0)
            widgets.Add(new DashboardWidget("genai-top-models", "Usage by Model", "table", topModels));

        var tokenSeries = await QueryTimeSeriesAsync(con, """
            SELECT
                strftime(time_bucket(INTERVAL '1 hour', to_timestamp(start_time_unix_nano / 1e9)), '%H:%M') AS bucket,
                COALESCE(SUM(gen_ai_input_tokens + gen_ai_output_tokens), 0) AS total_tokens
            FROM spans
            WHERE (gen_ai_provider_name IS NOT NULL OR gen_ai_request_model IS NOT NULL)
              AND start_time_unix_nano >= (epoch_ns(now()) - 86400000000000)
            GROUP BY bucket
            ORDER BY bucket
            """, "bucket", "total_tokens", ct).ConfigureAwait(false);

        if (tokenSeries.Count > 0)
            widgets.Add(new DashboardWidget("genai-token-usage", "Token Usage (24h)", "chart", tokenSeries));

        return widgets;
    }

    // =========================================================================
    // Database
    // =========================================================================

    private static async Task<IReadOnlyList<DashboardWidget>> DatabaseAsync(
        DuckDBConnection con, CancellationToken ct)
    {
        var widgets = new List<DashboardWidget>();

        var stats = await QueryStatsAsync(con, """
            SELECT
                COUNT(*) AS total_queries,
                ROUND(AVG(duration_ns / 1e6), 1) AS avg_latency_ms,
                ROUND(PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns / 1e6), 1) AS p95_latency_ms,
                ROUND(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS error_rate
            FROM spans
            WHERE attributes_json LIKE '%db.system%'
            """, ct).ConfigureAwait(false);

        if (stats.Count > 0)
        {
            widgets.Add(new DashboardWidget("db-total-queries", "Total Queries", "stat",
                new StatCardData("Queries", stats.GetValueOrDefault("total_queries", "0"))));
            widgets.Add(new DashboardWidget("db-avg-latency", "Avg Latency", "stat",
                new StatCardData("Latency", stats.GetValueOrDefault("avg_latency_ms", "0"), "ms")));
            widgets.Add(new DashboardWidget("db-p95-latency", "P95 Latency", "stat",
                new StatCardData("P95", stats.GetValueOrDefault("p95_latency_ms", "0"), "ms")));
            widgets.Add(new DashboardWidget("db-error-rate", "Error Rate", "stat",
                new StatCardData("Errors", stats.GetValueOrDefault("error_rate", "0"), "%")));
        }

        var topOps = await QueryTopNAsync(con, """
            SELECT
                COALESCE(
                    json_extract_string(attributes_json, '$.db.operation.name'),
                    json_extract_string(attributes_json, '$.db.operation'),
                    name
                ) AS op_name,
                ROUND(PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns / 1e6), 1) AS p95_ms,
                COUNT(*) AS call_count,
                ROUND(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS err_rate
            FROM spans
            WHERE attributes_json LIKE '%db.system%'
            GROUP BY op_name
            ORDER BY p95_ms DESC
            LIMIT 10
            """, "op_name", "p95_ms", "ms", "call_count", "err_rate", ct).ConfigureAwait(false);

        if (topOps.Count > 0)
            widgets.Add(new DashboardWidget("db-top-ops", "Slowest Operations", "table", topOps));

        return widgets;
    }

    // =========================================================================
    // Error Tracker
    // =========================================================================

    private static async Task<IReadOnlyList<DashboardWidget>> ErrorTrackerAsync(
        DuckDBConnection con, CancellationToken ct)
    {
        var widgets = new List<DashboardWidget>();

        var stats = await QueryStatsAsync(con, """
            SELECT
                COUNT(*) AS total_errors,
                COUNT(DISTINCT service_name) AS affected_services
            FROM logs
            WHERE severity_number >= 17
            """, ct).ConfigureAwait(false);

        if (stats.Count > 0)
        {
            widgets.Add(new DashboardWidget("err-total", "Total Errors", "stat",
                new StatCardData("Errors", stats.GetValueOrDefault("total_errors", "0"))));
            widgets.Add(new DashboardWidget("err-services", "Affected Services", "stat",
                new StatCardData("Services", stats.GetValueOrDefault("affected_services", "0"))));
        }

        var topErrors = await QueryTopNAsync(con, """
            SELECT
                COALESCE(SUBSTRING(body, 1, 120), 'unknown') AS error_name,
                ROUND(0, 1) AS placeholder_val,
                COUNT(*) AS occurrence_count,
                ROUND(0, 1) AS no_rate
            FROM logs
            WHERE severity_number >= 17
            GROUP BY error_name
            ORDER BY occurrence_count DESC
            LIMIT 10
            """, "error_name", "placeholder_val", null, "occurrence_count", null, ct).ConfigureAwait(false);

        if (topErrors.Count > 0)
            widgets.Add(new DashboardWidget("err-top", "Top Errors", "table", topErrors));

        var errorSeries = await QueryTimeSeriesAsync(con, """
            SELECT
                strftime(time_bucket(INTERVAL '1 hour', to_timestamp(time_unix_nano / 1e9)), '%H:%M') AS bucket,
                COUNT(*) AS error_count
            FROM logs
            WHERE severity_number >= 17
              AND time_unix_nano >= (epoch_ns(now()) - 86400000000000)
            GROUP BY bucket
            ORDER BY bucket
            """, "bucket", "error_count", ct).ConfigureAwait(false);

        if (errorSeries.Count > 0)
            widgets.Add(new DashboardWidget("err-timeline", "Errors Over Time (24h)", "chart", errorSeries));

        return widgets;
    }

    // =========================================================================
    // Messaging
    // =========================================================================

    private static async Task<IReadOnlyList<DashboardWidget>> MessagingAsync(
        DuckDBConnection con, CancellationToken ct)
    {
        var widgets = new List<DashboardWidget>();

        var stats = await QueryStatsAsync(con, """
            SELECT
                COUNT(*) AS total_messages,
                ROUND(AVG(duration_ns / 1e6), 1) AS avg_latency_ms,
                ROUND(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS error_rate
            FROM spans
            WHERE attributes_json LIKE '%messaging.system%'
            """, ct).ConfigureAwait(false);

        if (stats.Count > 0)
        {
            widgets.Add(new DashboardWidget("msg-total", "Total Messages", "stat",
                new StatCardData("Messages", stats.GetValueOrDefault("total_messages", "0"))));
            widgets.Add(new DashboardWidget("msg-latency", "Avg Latency", "stat",
                new StatCardData("Latency", stats.GetValueOrDefault("avg_latency_ms", "0"), "ms")));
            widgets.Add(new DashboardWidget("msg-error-rate", "Error Rate", "stat",
                new StatCardData("Errors", stats.GetValueOrDefault("error_rate", "0"), "%")));
        }

        var topSystems = await QueryTopNAsync(con, """
            SELECT
                COALESCE(json_extract_string(attributes_json, '$.messaging.system'), name) AS system_name,
                ROUND(AVG(duration_ns / 1e6), 1) AS avg_ms,
                COUNT(*) AS msg_count,
                ROUND(SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0), 1) AS err_rate
            FROM spans
            WHERE attributes_json LIKE '%messaging.system%'
            GROUP BY system_name
            ORDER BY msg_count DESC
            LIMIT 10
            """, "system_name", "avg_ms", "ms", "msg_count", "err_rate", ct).ConfigureAwait(false);

        if (topSystems.Count > 0)
            widgets.Add(new DashboardWidget("msg-systems", "Messaging Systems", "table", topSystems));

        return widgets;
    }

    // =========================================================================
    // Query Helpers
    // =========================================================================

    private static async Task<Dictionary<string, string>> QueryStatsAsync(
        DuckDBConnection con, string sql, CancellationToken ct)
    {
        var result = new Dictionary<string, string>();
        try
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    result[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct).ConfigureAwait(false)
                        ? "0"
                        : reader.GetValue(i).ToString() ?? "0";
                }
            }
        }
        catch
        {
            // Graceful fallback — table/column might not exist yet
        }

        return result;
    }

    private static async Task<IReadOnlyList<TopNRow>> QueryTopNAsync(
        DuckDBConnection con, string sql,
        string nameCol, string valueCol, string? unit,
        string? countCol, string? errorRateCol,
        CancellationToken ct)
    {
        var rows = new List<TopNRow>();
        try
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var name = reader[nameCol].ToString() ?? "unknown";
                var value = Convert.ToDouble(reader[valueCol]);
                var count = countCol is not null ? Convert.ToInt32(reader[countCol]) : (int?)null;
                var errorRate = errorRateCol is not null ? Convert.ToDouble(reader[errorRateCol]) : (double?)null;

                rows.Add(new TopNRow(name, value, unit, count, errorRate));
            }
        }
        catch
        {
            // Graceful fallback
        }

        return rows;
    }

    private static async Task<IReadOnlyList<TimeSeriesPoint>> QueryTimeSeriesAsync(
        DuckDBConnection con, string sql, string timeCol, string valueCol, CancellationToken ct)
    {
        var points = new List<TimeSeriesPoint>();
        try
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var time = reader[timeCol].ToString() ?? "";
                var value = Convert.ToDouble(reader[valueCol]);
                points.Add(new TimeSeriesPoint(time, value));
            }
        }
        catch
        {
            // Graceful fallback
        }

        return points;
    }
}
