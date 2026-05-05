#nullable enable

namespace Qyl.Domains.Ops.Deployment;

public sealed class DeploymentAttributes
{
    public string? EnvironmentName { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    public Qyl.Domains.Ops.Deployment.DeploymentStatus? Status { get; init; }
}

public sealed class DeploymentEntity
{
    public required string DeploymentId { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceVersion { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentStatus Status { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentStrategy Strategy { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public double? DurationS { get; init; }
    public string? DeployedBy { get; init; }
    public string? GitCommit { get; init; }
    public string? GitBranch { get; init; }
    public string? PreviousVersion { get; init; }
    public string? RollbackTarget { get; init; }
    public int? ReplicaCount { get; init; }
    public int? HealthyReplicas { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class DeploymentEvent
{
    public required Qyl.Domains.Ops.Deployment.DeploymentEventName EventName { get; init; }
    public required string DeploymentId { get; init; }
    public required string ServiceName { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentStatus Status { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

public sealed class DeploymentDurationMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }
    public required string ServiceName { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentStatus Status { get; init; }
}

public sealed class DeploymentCountMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }
    public required string ServiceName { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentStatus Status { get; init; }
}

public sealed class DoraDeploymentFrequencyMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }
    public string? ServiceName { get; init; }
}

public sealed class DoraLeadTimeMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }
    public string? ServiceName { get; init; }
}

public sealed class DoraChangeFailureRateMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }
    public string? ServiceName { get; init; }
}

public sealed class DoraMttrMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }
    public string? ServiceName { get; init; }
}

public enum DeploymentStatus
{
    Pending,
    InProgress,
    Success,
    Failed,
    RolledBack,
    Cancelled
}

public enum DeploymentEnvironment
{
    Development,
    Testing,
    Staging,
    Production,
    Preview,
    Canary
}

public enum DeploymentStrategy
{
    Rolling,
    BlueGreen,
    Canary,
    Recreate,
    AbTest,
    Shadow,
    FeatureFlag
}

public enum DeploymentEventName
{
    Started,
    Completed,
    Failed,
    RolledBack,
    HealthCheckPassed,
    HealthCheckFailed
}
