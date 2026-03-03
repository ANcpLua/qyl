namespace qyl.collector.Autofix;

/// <summary>
///     Storage record for a deployment event. Maps to the deployments DuckDB table.
///     Used by the regression detection service to correlate error spikes with deploys.
/// </summary>
public sealed record DeploymentRecord
{
    public required string DeploymentId { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceVersion { get; init; }
    public required string Environment { get; init; }
    public required string Status { get; init; }
    public required string Strategy { get; init; }
    public required DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public double? DurationS { get; init; }
    public string? DeployedBy { get; init; }
    public string? GitCommit { get; init; }
    public string? GitBranch { get; init; }
    public string? PreviousVersion { get; init; }
    public string? RollbackTarget { get; init; }
    public int? ReplicaCount { get; init; }
    public int? HealthyReplicas { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? CreatedAt { get; init; }
}
