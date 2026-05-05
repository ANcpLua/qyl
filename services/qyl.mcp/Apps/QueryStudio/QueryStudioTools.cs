using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Tools;

namespace qyl.mcp.Apps.QueryStudio;

[McpServerToolType]
[QylSkill(QylSkillKind.Apps)]
internal sealed partial class QueryStudioTools(HttpClient client)
{
    [QylCapability("mcp_apps")]
    [McpServerTool(Name = "qyl.app.query_studio", Title = "Query Studio",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> OpenQueryStudioAsync() =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var schema = await FetchSchemaAsync().ConfigureAwait(false);

            var result = new QueryStudioOpenResult(
                schema,
                QueryStudioPresets.All,
                new QueryStudioUiMeta("ui://qyl/query-studio", "Query Studio",
                    "Interactive DuckDB query console for qyl observability data"));

            return JsonSerializer.Serialize(result, QueryStudioJsonContext.Default.QueryStudioOpenResult);
        });

    [QylCapability("mcp_apps", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.app.execute_query", Title = "Execute Query",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> ExecuteQueryAsync(
        string sql,
        int limit = 100,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var take = Math.Clamp(limit, 1, 10_000);
            var stopwatch = Stopwatch.StartNew();

            using var resp = await client.PostAsJsonAsync(
                "/api/v1/query",
                new QueryStudioRequest(sql, take),
                QueryStudioJsonContext.Default.QueryStudioRequest,
                ct).ConfigureAwait(false);

            stopwatch.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return JsonSerializer.Serialize(
                    new QueryStudioErrorResult(error, sql),
                    QueryStudioJsonContext.Default.QueryStudioErrorResult);
            }

            var raw = await resp.Content.ReadFromJsonAsync(
                QueryStudioJsonContext.Default.QueryStudioRawResponse, ct).ConfigureAwait(false);

            if (raw is null)
            {
                return JsonSerializer.Serialize(
                    new QueryStudioErrorResult("Empty response from collector.", sql),
                    QueryStudioJsonContext.Default.QueryStudioErrorResult);
            }

            var firstRow = raw.Rows.Count > 0 ? raw.Rows[0] : null;
            var columns = raw.Columns
                .Select(name => new QueryStudioColumn(name,
                    firstRow is not null && firstRow.TryGetValue(name, out var v) && v is not null
                        ? InferType(v)
                        : "varchar"))
                .ToList();

            var rows = raw.Rows
                .Select(row => raw.Columns
                    .Select(col => row.TryGetValue(col, out var val) ? NormalizeJsonElement(val) : null)
                    .ToList())
                .ToList();

            var result = new QueryStudioQueryResult(
                columns,
                rows,
                raw.RowCount,
                stopwatch.Elapsed.TotalMilliseconds,
                sql);

            return JsonSerializer.Serialize(result, QueryStudioJsonContext.Default.QueryStudioQueryResult);
        });

    [QylCapability("mcp_apps", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.app.query_schema", Title = "Query Schema",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> QuerySchemaAsync(CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var schema = await FetchSchemaAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(schema, QueryStudioJsonContext.Default.QueryStudioSchemaResponse);
        });

    private async Task<QueryStudioSchemaResponse> FetchSchemaAsync(CancellationToken ct = default)
    {
        const string schemaSql = """
                                 SELECT table_name, column_name, data_type
                                 FROM information_schema.columns
                                 WHERE table_schema = 'main'
                                 ORDER BY table_name, ordinal_position
                                 """;

        try
        {
            using var resp = await client.PostAsJsonAsync(
                "/api/v1/query",
                new QueryStudioRequest(schemaSql, 5000),
                QueryStudioJsonContext.Default.QueryStudioRequest,
                ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return QueryStudioPresets.FallbackSchema;

            var raw = await resp.Content.ReadFromJsonAsync(
                QueryStudioJsonContext.Default.QueryStudioRawResponse, ct).ConfigureAwait(false);

            if (raw?.Rows is not { Count: > 0 })
                return QueryStudioPresets.FallbackSchema;

            var tables = raw.Rows
                .GroupBy(r => GetStringValue(r, "table_name"))
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g => new QueryStudioTable(
                    g.Key!,
                    g.Select(r => new QueryStudioColumn(
                        GetStringValue(r, "column_name") ?? "unknown",
                        GetStringValue(r, "data_type") ?? "unknown"
                    )).ToList()))
                .ToList();

            return new QueryStudioSchemaResponse(tables);
        }
        catch (HttpRequestException)
        {
            return QueryStudioPresets.FallbackSchema;
        }
    }

    private static string InferType(object? value) => value switch
    {
        JsonElement el => el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt64(out _) ? "bigint" : "double",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.String when DateTime.TryParse(el.GetString(), out _) => "timestamp",
            _ => "varchar"
        },
        int or long => "bigint",
        float or double or decimal => "double",
        bool => "boolean",
        DateTime or DateTimeOffset => "timestamp",
        _ => "varchar"
    };

    private static object? NormalizeJsonElement(object? value) => value switch
    {
        JsonElement el => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number when el.TryGetInt64(out var l) => l,
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => el.ToString()
        },
        _ => value
    };

    private static string? GetStringValue(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var val) && val is not null
            ? val switch
            {
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } el => el.GetString(),
                _ => val.ToString()
            }
            : null;
}

#region Data Models

internal sealed record QueryStudioOpenResult(
    QueryStudioSchemaResponse Schema,
    List<QueryStudioPreset> Presets,
    QueryStudioUiMeta Meta);

internal sealed record QueryStudioUiMeta(
    string ResourceUri,
    string Title,
    string Description);

internal sealed record QueryStudioQueryResult(
    List<QueryStudioColumn> Columns,
    List<List<object?>> Rows,
    int RowCount,
    double ExecutionTimeMs,
    string Query);

internal sealed record QueryStudioErrorResult(
    string Error,
    string Query);

internal sealed record QueryStudioSchemaResponse(
    List<QueryStudioTable> Tables);

internal sealed record QueryStudioTable(
    string Name,
    List<QueryStudioColumn> Columns);

internal sealed record QueryStudioColumn(
    string Name,
    string Type);

internal sealed record QueryStudioPreset(
    string Name,
    string Sql);

internal sealed record QueryStudioRequest(
    [property: JsonPropertyName("sql")] string Sql,
    [property: JsonPropertyName("limit")] int? Limit);

internal sealed record QueryStudioRawResponse(
    [property: JsonPropertyName("columns")]
    List<string> Columns,
    [property: JsonPropertyName("rows")] List<Dictionary<string, object?>> Rows,
    [property: JsonPropertyName("rowCount")]
    int RowCount);

#endregion

#region Presets

internal static class QueryStudioPresets
{
    public static readonly List<QueryStudioPreset> All =
    [
        new("Recent traces",
            "SELECT * FROM spans ORDER BY start_time DESC LIMIT 100"),
        new("Error rate by service",
            "SELECT service_name, COUNT(*) FILTER (WHERE status = 'ERROR') * 100.0 / COUNT(*) as error_rate FROM spans GROUP BY service_name"),
        new("Slow operations",
            "SELECT operation_name, AVG(duration_ms) as avg_ms, MAX(duration_ms) as max_ms FROM spans GROUP BY operation_name ORDER BY avg_ms DESC LIMIT 20"),
        new("Log volume",
            "SELECT date_trunc('hour', timestamp) as hour, COUNT(*) as count FROM logs GROUP BY hour ORDER BY hour"),
        new("Error distribution",
            "SELECT error_type, COUNT(*) as count, MIN(first_seen) as first_seen, MAX(last_seen) as last_seen FROM errors GROUP BY error_type ORDER BY count DESC"),
        new("Service health",
            "SELECT service_name, COUNT(*) as total_spans, COUNT(*) FILTER (WHERE status = 'ERROR') as errors, AVG(duration_ms) as avg_latency_ms FROM spans GROUP BY service_name ORDER BY errors DESC")
    ];

    public static readonly QueryStudioSchemaResponse FallbackSchema = new(
    [
        new QueryStudioTable("spans",
        [
            new QueryStudioColumn("trace_id", "varchar"),
            new QueryStudioColumn("span_id", "varchar"),
            new QueryStudioColumn("parent_span_id", "varchar"),
            new QueryStudioColumn("operation_name", "varchar"),
            new QueryStudioColumn("service_name", "varchar"),
            new QueryStudioColumn("duration_ms", "double"),
            new QueryStudioColumn("start_time", "timestamp"),
            new QueryStudioColumn("end_time", "timestamp"),
            new QueryStudioColumn("status", "varchar"),
            new QueryStudioColumn("status_message", "varchar"),
            new QueryStudioColumn("kind", "varchar")
        ]),
        new QueryStudioTable("logs",
        [
            new QueryStudioColumn("log_id", "varchar"),
            new QueryStudioColumn("timestamp", "timestamp"),
            new QueryStudioColumn("severity", "varchar"),
            new QueryStudioColumn("body", "varchar"),
            new QueryStudioColumn("service_name", "varchar"),
            new QueryStudioColumn("trace_id", "varchar"),
            new QueryStudioColumn("span_id", "varchar")
        ]),
        new QueryStudioTable("errors",
        [
            new QueryStudioColumn("error_id", "varchar"),
            new QueryStudioColumn("error_type", "varchar"),
            new QueryStudioColumn("error_message", "varchar"),
            new QueryStudioColumn("status", "varchar"),
            new QueryStudioColumn("fingerprint", "varchar"),
            new QueryStudioColumn("first_seen", "timestamp"),
            new QueryStudioColumn("last_seen", "timestamp"),
            new QueryStudioColumn("event_count", "bigint"),
            new QueryStudioColumn("affected_services", "varchar")
        ])
    ]);
}

#endregion

#region JSON Context

[JsonSerializable(typeof(QueryStudioOpenResult))]
[JsonSerializable(typeof(QueryStudioQueryResult))]
[JsonSerializable(typeof(QueryStudioErrorResult))]
[JsonSerializable(typeof(QueryStudioSchemaResponse))]
[JsonSerializable(typeof(QueryStudioRequest))]
[JsonSerializable(typeof(QueryStudioRawResponse))]
[JsonSerializable(typeof(List<QueryStudioPreset>))]
[JsonSerializable(typeof(List<QueryStudioTable>))]
[JsonSerializable(typeof(List<QueryStudioColumn>))]
[JsonSerializable(typeof(List<List<object?>>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class QueryStudioJsonContext : JsonSerializerContext;

#endregion
