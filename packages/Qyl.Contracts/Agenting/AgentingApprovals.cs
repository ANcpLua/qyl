namespace Qyl.Contracts.Agenting;

public sealed record AgentRunApprovalRequest
{
    public required string ApprovalId { get; init; }
    public required string RunId { get; init; }
    public required AgentRunPhase Phase { get; init; }
    public required string CapabilityId { get; init; }
    public required string RequestedBy { get; init; }
    public required string RequestedFor { get; init; }
    public required string Justification { get; init; }
    public required string ApprovalReason { get; init; }
    public string? ArtifactId { get; init; }
    public DateTimeOffset RequestedAtUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed record AgentRunApprovalDecision
{
    public required string ApprovalId { get; init; }
    public required string DecidedBy { get; init; }
    public required AgentRunApprovalStatus Status { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset DecidedAtUtc { get; init; }
    public DateTimeOffset? EffectiveUntilUtc { get; init; }
    public string? ConditionsJson { get; init; }
}
