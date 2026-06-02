namespace Qyl.Collector.Services;

internal static class ServiceEndpoints
{
    [QylMapEndpoints]
    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/services");

        group.MapGet("/", GetServicesAsync);
        group.MapGet("/{serviceName}", GetServiceDetailAsync);

        group.MapGet("/map", GetServiceMapAsync);

        return app;
    }

    private static async Task<IResult> GetServicesAsync(
        DuckDbStore store,
        string? type,
        string? status,
        int? limit,
        CancellationToken ct)
    {
        var services = await store.GetServicesAsync(
            type, status, limit ?? 100, ct).ConfigureAwait(false);

        return TypedResults.Ok(new ServicesResponse { Services = services, Total = services.Count });
    }

    private static async Task<IResult> GetServiceDetailAsync(
        string serviceName,
        DuckDbStore store,
        string? type,
        CancellationToken ct) =>
        await store.GetServiceDetailAsync(serviceName, type, ct).ConfigureAwait(false) is not { } detail
            ? TypedResults.NotFound()
            : TypedResults.Ok(detail);

    private static async Task<IResult> GetServiceMapAsync(
        DuckDbStore store,
        CancellationToken ct)
    {
        var edges = await store.ExecuteReadAsync(static con =>
        {
            using var cmd = con.CreateCommand();

            cmd.CommandText = """
                              SELECT
                                  COALESCE(parent.service_name, 'unknown') as source,
                                  COALESCE(child.service_name, 'unknown') as target,
                                  COUNT(*) as call_count,
                                  AVG(child.duration_ns) as avg_duration_ns,
                                  COUNT(*) FILTER (WHERE TRY_CAST(child.status_code AS INTEGER) = 2) as error_count
                              FROM spans child
                              JOIN spans parent ON child.parent_span_id = parent.span_id
                              WHERE child.service_name IS NOT NULL
                                AND parent.service_name IS NOT NULL
                                AND child.service_name != parent.service_name
                              GROUP BY source, target
                              ORDER BY call_count DESC
                              LIMIT 500
                              """;

            var rows = new List<ServiceEdgeDto>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new ServiceEdgeDto
                {
                    Source = reader.GetString(0),
                    Target = reader.GetString(1),
                    CallCount = reader.Col(2).GetInt64(0),
                    AvgDurationMs = reader.Col(3).AsDouble is { } avgDurationNs ? avgDurationNs / 1_000_000d : null,
                    ErrorCount = reader.Col(4).GetInt64(0)
                });
            }

            return rows;
        }, ct).ConfigureAwait(false);

        var nodeSet = new HashSet<string>();
        foreach (var edge in edges)
        {
            nodeSet.Add(edge.Source);
            nodeSet.Add(edge.Target);
        }

        return TypedResults.Ok(new ServiceMapDto { Nodes = [.. nodeSet.Order()], Edges = edges });
    }
}


public sealed record ServiceSummary
{
    public required string ServiceNamespace { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceType { get; init; }
    public string? LatestVersion { get; init; }
    public string? ProviderName { get; init; }
    public string? DefaultModel { get; init; }
    public DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public DateTimeOffset? LastErrorAt { get; init; }
    public int TotalInstances { get; init; }
    public int ActiveInstances { get; init; }
    public long TotalSpans { get; init; }
    public long TotalLogs { get; init; }
    public long TotalErrors { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public double TotalCostUsd { get; init; }
    public double? ErrorRate { get; init; }
}

public sealed record ServiceInstanceDto
{
    public required string ServiceNamespace { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceInstanceId { get; init; }
    public required string ServiceType { get; init; }
    public string? ServiceVersion { get; init; }
    public string? DeploymentEnvironment { get; init; }
    public string? OsType { get; init; }
    public string? HostArch { get; init; }
    public string? AgentName { get; init; }
    public string? ProviderName { get; init; }
    public string? DefaultModel { get; init; }
    public DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public DateTimeOffset? LastErrorAt { get; init; }
    public string Status { get; init; } = "active";
    public long TotalSpans { get; init; }
    public long TotalLogs { get; init; }
    public long TotalErrors { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public double TotalCostUsd { get; init; }
}

public sealed record ServiceDetail
{
    public required string ServiceName { get; init; }
    public required string ServiceType { get; init; }
    public required IReadOnlyList<ServiceInstanceDto> Instances { get; init; }
}


internal sealed record ServicesResponse
{
    public required IReadOnlyList<ServiceSummary> Services { get; init; }
    public int Total { get; init; }
}


[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ServiceSummary))]
[JsonSerializable(typeof(ServiceInstanceDto))]
[JsonSerializable(typeof(ServiceDetail))]
[JsonSerializable(typeof(ServicesResponse))]
internal sealed partial class ServiceSerializerContext : JsonSerializerContext;


internal sealed record ServiceEdgeDto
{
    [JsonPropertyName("source")] public required string Source { get; init; }
    [JsonPropertyName("target")] public required string Target { get; init; }
    [JsonPropertyName("call_count")] public long CallCount { get; init; }
    [JsonPropertyName("avg_duration_ms")] public double? AvgDurationMs { get; init; }
    [JsonPropertyName("error_count")] public long ErrorCount { get; init; }
}

internal sealed record ServiceMapDto
{
    [JsonPropertyName("nodes")] public required IReadOnlyList<string> Nodes { get; init; }
    [JsonPropertyName("edges")] public required IReadOnlyList<ServiceEdgeDto> Edges { get; init; }
}
