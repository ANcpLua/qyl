using System.Text.Json.Serialization;

namespace qyl.collector.Services;

internal static class ServiceEndpoints
{
    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/services");

        group.MapGet("/", GetServicesAsync);
        group.MapGet("/{serviceName}", GetServiceDetailAsync);

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

        return Results.Ok(new ServicesResponse { Services = services, Total = services.Count });
    }

    private static async Task<IResult> GetServiceDetailAsync(
        string serviceName,
        DuckDbStore store,
        string? type,
        CancellationToken ct)
    {
        return await store.GetServiceDetailAsync(serviceName, type, ct).ConfigureAwait(false) is not { } detail
            ? Results.NotFound()
            : Results.Ok(detail);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// DTOs
// ═════════════════════════════════════════════════════════════════════════════

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

// ═════════════════════════════════════════════════════════════════════════════
// Response wrappers
// ═════════════════════════════════════════════════════════════════════════════

internal sealed record ServicesResponse
{
    public required IReadOnlyList<ServiceSummary> Services { get; init; }
    public int Total { get; init; }
}

// ═════════════════════════════════════════════════════════════════════════════
// JSON serializer context (AOT-compatible)
// ═════════════════════════════════════════════════════════════════════════════

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ServiceSummary))]
[JsonSerializable(typeof(ServiceInstanceDto))]
[JsonSerializable(typeof(ServiceDetail))]
[JsonSerializable(typeof(ServicesResponse))]
internal sealed partial class ServiceSerializerContext : JsonSerializerContext;
