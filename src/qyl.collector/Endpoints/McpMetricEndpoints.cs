using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Endpoints;

/// <summary>
///     MCP metric endpoints — /api/v1/mcp/metrics.
///     List available metrics and query time series data from DuckDB spans/logs.
/// </summary>
internal static class McpMetricEndpoints
{
    public static WebApplication MapMcpMetricEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mcp");

        group.MapGet("/metrics", ListMetricsAsync);
        group.MapGet("/metrics/{name}/query", QueryMetricAsync);

        return app;
    }

    /// <summary>
    ///     GET /api/v1/mcp/metrics?project=...
    ///     Lists available metric names derived from span aggregations.
    /// </summary>
    private static async Task<IResult> ListMetricsAsync(
        [FromServices] DuckDbStore store,
        [FromQuery] string? project,
        CancellationToken ct)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var qb = new McpQueryBuilder();
        if (!string.IsNullOrWhiteSpace(project))
            qb.Add("service_name = $N", project);

        // Derive available metrics from span data
        cmd.CommandText = $"""
                           SELECT
                               COALESCE(service_name, 'unknown') as service,
                               COUNT(*) as span_count,
                               COUNT(*) FILTER (WHERE status_code = 2) as error_count,
                               AVG(duration_ns) as avg_duration_ns,
                               COALESCE(SUM(gen_ai_input_tokens), 0) as total_input_tokens,
                               COALESCE(SUM(gen_ai_output_tokens), 0) as total_output_tokens,
                               COALESCE(SUM(gen_ai_cost_usd), 0) as total_cost_usd
                           FROM spans
                           {qb.WhereClause}
                           GROUP BY COALESCE(service_name, 'unknown')
                           ORDER BY span_count DESC
                           LIMIT 100
                           """;
        qb.ApplyTo(cmd);

        var metrics = new List<McpMetricSummaryDto>
        {
            new() { Name = "span.count", Description = "Total number of spans", Unit = "count" },
            new() { Name = "span.duration", Description = "Span duration", Unit = "ns" },
            new() { Name = "span.error_rate", Description = "Error rate (status_code=2)", Unit = "ratio" },
            new() { Name = "genai.input_tokens", Description = "GenAI input token count", Unit = "tokens" },
            new() { Name = "genai.output_tokens", Description = "GenAI output token count", Unit = "tokens" },
            new() { Name = "genai.cost", Description = "GenAI cost", Unit = "usd" },
            new() { Name = "log.count", Description = "Total number of logs", Unit = "count" }
        };

        var services = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            services.Add(reader.GetString(0));

        return TypedResults.Ok(new McpMetricsListDto
        {
            Metrics = metrics,
            Services = services
        });
    }

    /// <summary>
    ///     GET /api/v1/mcp/metrics/{name}/query?filter=...&amp;from=...&amp;to=...&amp;interval=...
    ///     Time series query for a named metric.
    /// </summary>
    private static async Task<IResult> QueryMetricAsync(
        string name,
        [FromServices] DuckDbStore store,
        [FromQuery] string? filter,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? interval,
        CancellationToken ct)
    {
        var normalizedInterval = interval ?? "1h";
        var bucketInterval = normalizedInterval switch
        {
            "1m" => "1 MINUTE",
            "5m" => "5 MINUTES",
            "15m" => "15 MINUTES",
            "6h" => "6 HOURS",
            "1d" => "1 DAY",
            _ => "1 HOUR"
        };

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var qb = new McpQueryBuilder();
        if (!string.IsNullOrWhiteSpace(filter))
            qb.Add("service_name = $N", filter);

        // Parse time bounds
        if (!string.IsNullOrWhiteSpace(from) && DateTimeOffset.TryParse(from, CultureInfo.InvariantCulture, out var fromTime))
        {
            var fromNano = (ulong)(fromTime.ToUnixTimeMilliseconds() * 1_000_000L);
            qb.Add("start_time_unix_nano >= $N", (decimal)fromNano);
        }

        if (!string.IsNullOrWhiteSpace(to) && DateTimeOffset.TryParse(to, CultureInfo.InvariantCulture, out var toTime))
        {
            var toNano = (ulong)(toTime.ToUnixTimeMilliseconds() * 1_000_000L);
            qb.Add("start_time_unix_nano <= $N", (decimal)toNano);
        }

        // Build the aggregation based on metric name
        var (selectExpr, tableName) = name switch
        {
            "span.count" => ("COUNT(*)", "spans"),
            "span.duration" => ("AVG(duration_ns)", "spans"),
            "span.error_rate" => ("COUNT(*) FILTER (WHERE status_code = 2)::DOUBLE / NULLIF(COUNT(*), 0)", "spans"),
            "genai.input_tokens" => ("COALESCE(SUM(gen_ai_input_tokens), 0)", "spans"),
            "genai.output_tokens" => ("COALESCE(SUM(gen_ai_output_tokens), 0)", "spans"),
            "genai.cost" => ("COALESCE(SUM(gen_ai_cost_usd), 0)", "spans"),
            "log.count" => ("COUNT(*)", "logs"),
            _ => (null, null)
        };

        if (selectExpr is null || tableName is null)
            return TypedResults.NotFound(new { error = $"Unknown metric: {name}" });

        var timeColumn = tableName is "logs" ? "time_unix_nano" : "start_time_unix_nano";

        cmd.CommandText = $"""
                           SELECT
                               time_bucket(INTERVAL '{bucketInterval}',
                                   epoch_ms({timeColumn} / 1000000)::TIMESTAMP) as bucket,
                               {selectExpr} as value
                           FROM {tableName}
                           {qb.WhereClause}
                           GROUP BY bucket
                           ORDER BY bucket ASC
                           LIMIT 1000
                           """;
        qb.ApplyTo(cmd);

        var points = new List<McpTimeSeriesPointDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            points.Add(new McpTimeSeriesPointDto
            {
                Timestamp = reader.Col(0).AsDateTimeOffset ?? DateTimeOffset.MinValue,
                Value = reader.Col(1).AsDouble ?? 0
            });
        }

        return TypedResults.Ok(new McpTimeSeriesDto
        {
            Metric = name,
            Interval = normalizedInterval,
            Points = points
        });
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DTOs — Metric Endpoints
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed record McpMetricSummaryDto
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("unit")] public string? Unit { get; init; }
}

internal sealed record McpMetricsListDto
{
    [JsonPropertyName("metrics")] public required IReadOnlyList<McpMetricSummaryDto> Metrics { get; init; }
    [JsonPropertyName("services")] public required IReadOnlyList<string> Services { get; init; }
}

internal sealed record McpTimeSeriesPointDto
{
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; init; }
    [JsonPropertyName("value")] public double Value { get; init; }
}

internal sealed record McpTimeSeriesDto
{
    [JsonPropertyName("metric")] public required string Metric { get; init; }
    [JsonPropertyName("interval")] public required string Interval { get; init; }
    [JsonPropertyName("points")] public required IReadOnlyList<McpTimeSeriesPointDto> Points { get; init; }
}
