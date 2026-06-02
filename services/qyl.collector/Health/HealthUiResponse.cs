namespace Qyl.Collector.Health;

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

public sealed record HealthUiResponse
{
    public required HealthStatus Status { get; init; }

    public required IReadOnlyList<ComponentHealth> Components { get; init; }

    public required long UptimeSeconds { get; init; }

    public required string Version { get; init; }

    public string? LastIngestionTime { get; init; }

    public required string CheckedAt { get; init; }
}

public sealed record ComponentHealth
{
    public required string Name { get; init; }

    public required HealthStatus Status { get; init; }

    public string? Message { get; init; }

    public IReadOnlyDictionary<string, object>? Data { get; init; }
}
