namespace Qyl.Contracts.Agenting;

/// <summary>
/// Canonical, deterministic state object for agentic runs.
/// </summary>
public sealed record AutofixRunState
{
    public required string RunId { get; init; }
    public required string IssueId { get; init; }
    public required AgentRunPhase Phase { get; init; }
    public required AgentRunStatus Status { get; init; }
    public required int Attempt { get; init; }
    public required int MaxAttempts { get; init; }
    public required string CreatedBy { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public string? PlannerAgent { get; init; }
    public string? ExecutorAgent { get; init; }
    public string? LastErrorCode { get; init; }
    public string? LastErrorMessage { get; init; }
    public string? CorrelationId { get; init; }
    public IReadOnlyList<string>? AppliedCapabilities { get; init; }
    public IReadOnlyList<string>? PendingApprovals { get; init; }
    public IReadOnlyList<string>? Artifacts { get; init; }
    public string? ContextJson { get; init; }
    public string? PlanJson { get; init; }
    public string? PatchJson { get; init; }
    public string? ValidationJson { get; init; }
    public double? LatestConfidence { get; init; }
    public string? FinalOutcome { get; init; }
}

/// <summary>
/// Envelope for deterministic run telemetry and governance projection.
/// </summary>
public sealed record RunLedgerEvent
{
    public required string EventId { get; init; }
    public required string RunId { get; init; }
    public required string EventType { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
    public required string RecordedBy { get; init; }
    public required string PayloadJson { get; init; }
    public string? ArtifactId { get; init; }
}
