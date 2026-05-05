#nullable enable

namespace Qyl.Domains.Alerting;

public sealed class AlertRuleEntity
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required Qyl.Domains.Alerting.AlertRuleType RuleType { get; init; }
    public required string ConditionJson { get; init; }
    public string? ThresholdJson { get; init; }
    public required string TargetType { get; init; }
    public string? TargetFilterJson { get; init; }
    public required Qyl.Domains.Alerting.AlertSeverity Severity { get; init; }
    public required int CooldownSeconds { get; init; }
    public string? NotificationChannelsJson { get; init; }
    public required bool Enabled { get; init; }
    public DateTimeOffset? LastTriggeredAt { get; init; }
    public required long TriggerCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class AlertFiringEntity
{
    public required string Id { get; init; }
    public required string RuleId { get; init; }
    public required string Fingerprint { get; init; }
    public required Qyl.Domains.Alerting.AlertSeverity Severity { get; init; }
    public required string Title { get; init; }
    public string? Message { get; init; }
    public double? TriggerValue { get; init; }
    public double? ThresholdValue { get; init; }
    public string? ContextJson { get; init; }
    public required Qyl.Domains.Alerting.AlertFiringStatus Status { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public string? AcknowledgedBy { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public required DateTimeOffset FiredAt { get; init; }
    public string? DedupKey { get; init; }
}

public sealed class FixRunEntity
{
    public required string Id { get; init; }
    public required string IssueId { get; init; }
    public string? AlertFiringId { get; init; }
    public required Qyl.Domains.Alerting.FixTriggerType TriggerType { get; init; }
    public required string Strategy { get; init; }
    public string? ModelName { get; init; }
    public string? ModelProvider { get; init; }
    public required Qyl.Domains.Alerting.FixRunStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public int? TokensUsed { get; init; }
    public int? DurationMs { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

public sealed class FixArtifactEntity
{
    public required string Id { get; init; }
    public required string FixRunId { get; init; }
    public required Qyl.Domains.Alerting.FixArtifactType ArtifactType { get; init; }
    public required string Name { get; init; }
    public required string ContentType { get; init; }
    public string? Content { get; init; }
    public string? ContentHash { get; init; }
    public long? SizeBytes { get; init; }
    public string? MetadataJson { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class FixPolicyGateEntity
{
    public required string Id { get; init; }
    public required string FixRunId { get; init; }
    public required Qyl.Domains.Alerting.PolicyGateType GateType { get; init; }
    public required string GateName { get; init; }
    public required Qyl.Domains.Alerting.GateDecision Decision { get; init; }
    public string? Reason { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public required DateTimeOffset EvaluatedAt { get; init; }
}

public enum AlertRuleType
{
    Threshold,
    ErrorRate,
    NewIssue,
    Regression,
    BurnRate,
    Anomaly,
    Custom
}

public enum AlertSeverity
{
    Critical,
    Warning,
    Info
}

public enum AlertFiringStatus
{
    Firing,
    Acknowledged,
    Resolved,
    Suppressed
}

public enum FixTriggerType
{
    Alert,
    Manual,
    Mcp,
    Scheduled
}

public enum FixRunStatus
{
    Pending,
    Running,
    AwaitingApproval,
    Applied,
    Rejected,
    Failed
}

public enum FixArtifactType
{
    Patch,
    Log,
    Report,
    Prompt,
    Response,
    TestResults
}

public enum PolicyGateType
{
    Scope,
    Safety,
    Test,
    Review,
    Budget
}

public enum GateDecision
{
    Allow,
    Deny,
    Defer
}
