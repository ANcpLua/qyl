namespace qyl.collector.Health;

/// <summary>
///     Detailed health status for the UI dashboard.
/// </summary>
public sealed record HealthUiResponse
{
    /// <summary>Overall health status: healthy, degraded, or unhealthy.</summary>
    public required string Status { get; init; }

    /// <summary>Individual component health checks.</summary>
    public required IReadOnlyList<ComponentHealth> Components { get; init; }

    /// <summary>Server uptime in seconds.</summary>
    public required long UptimeSeconds { get; init; }

    /// <summary>Server version.</summary>
    public required string Version { get; init; }

    /// <summary>Last span ingestion time (UTC ISO 8601).</summary>
    public string? LastIngestionTime { get; init; }

    /// <summary>Timestamp of this health check (UTC ISO 8601).</summary>
    public required string CheckedAt { get; init; }
}

/// <summary>
///     Health status for an individual component.
/// </summary>
public sealed record ComponentHealth
{
    /// <summary>Component name (e.g., "duckdb", "disk", "memory").</summary>
    public required string Name { get; init; }

    /// <summary>Component status: healthy, degraded, or unhealthy.</summary>
    public required string Status { get; init; }

    /// <summary>Human-readable status message.</summary>
    public string? Message { get; init; }

    /// <summary>Additional component-specific data.</summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }
}
