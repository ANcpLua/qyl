#nullable enable

namespace Qyl.Domains.Issues;

public sealed class ErrorIssueEntity
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string Fingerprint { get; init; }
    public required string Title { get; init; }
    public string? Culprit { get; init; }
    public required string ErrorType { get; init; }
    public required string Category { get; init; }
    public required Qyl.Domains.Issues.IssueLevel Level { get; init; }
    public string? Platform { get; init; }
    public required DateTimeOffset FirstSeenAt { get; init; }
    public required DateTimeOffset LastSeenAt { get; init; }
    public required long OccurrenceCount { get; init; }
    public required int AffectedUsersCount { get; init; }
    public required Qyl.Domains.Issues.IssueStatus Status { get; init; }
    public string? Substatus { get; init; }
    public required Qyl.Domains.Issues.IssuePriority Priority { get; init; }
    public string? AssignedTo { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
    public required int RegressionCount { get; init; }
    public string? LastRelease { get; init; }
    public string? TagsJson { get; init; }
    public string? MetadataJson { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class ErrorIssueEventEntity
{
    public required string Id { get; init; }
    public required string IssueId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? Message { get; init; }
    public string? StackTrace { get; init; }
    public string? StackFramesJson { get; init; }
    public string? Environment { get; init; }
    public string? ReleaseVersion { get; init; }
    public string? UserId { get; init; }
    public string? UserIp { get; init; }
    public string? RequestUrl { get; init; }
    public string? RequestMethod { get; init; }
    public string? Browser { get; init; }
    public string? Os { get; init; }
    public string? Device { get; init; }
    public string? Runtime { get; init; }
    public string? RuntimeVersion { get; init; }
    public string? ContextJson { get; init; }
    public string? TagsJson { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

public sealed class ErrorBreadcrumbEntity
{
    public required string Id { get; init; }
    public required string EventId { get; init; }
    public required Qyl.Domains.Issues.BreadcrumbType BreadcrumbType { get; init; }
    public string? Category { get; init; }
    public string? Message { get; init; }
    public required string Level { get; init; }
    public string? DataJson { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

public sealed class ErrorRegressionEntity
{
    public required string Id { get; init; }
    public required string IssueId { get; init; }
    public required string ResolvedInRelease { get; init; }
    public required string RegressedInRelease { get; init; }
    public required DateTimeOffset ResolvedAt { get; init; }
    public required DateTimeOffset RegressedAt { get; init; }
    public required int OccurrenceCountBefore { get; init; }
    public required int OccurrenceCountAfter { get; init; }
    public required bool AutoDetected { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class ErrorOwnershipEntity
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string RuleType { get; init; }
    public required string Pattern { get; init; }
    public required string OwnerType { get; init; }
    public required string OwnerId { get; init; }
    public required int Priority { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class ErrorReleaseMarkerEntity
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string ReleaseVersion { get; init; }
    public required string Environment { get; init; }
    public string? CommitSha { get; init; }
    public string? CommitMessage { get; init; }
    public string? DeployedBy { get; init; }
    public required int IssuesResolvedCount { get; init; }
    public required int IssuesRegressedCount { get; init; }
    public required DateTimeOffset DeployedAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public enum IssueLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

public enum IssueStatus
{
    Unresolved,
    Acknowledged,
    Investigating,
    InProgress,
    Resolved,
    Ignored,
    Regressed
}

public enum IssuePriority
{
    Critical,
    High,
    Medium,
    Low
}

public enum BreadcrumbType
{
    Navigation,
    Http,
    Query,
    User,
    Log,
    Error,
    Debug,
    Default
}
