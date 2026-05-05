namespace Qyl.Contracts.Agenting;

public sealed record ValidationCheckpoint
{
    public required string CheckpointId { get; init; }
    public required string RunId { get; init; }
    public required string Name { get; init; }
    public required ValidationOutcome Outcome { get; init; }
    public required double Confidence { get; init; }
    public required string EvidenceJson { get; init; }
    public string? FailureReason { get; init; }
    public DateTimeOffset EvaluatedAtUtc { get; init; }
    public string? EvaluatedBy { get; init; }
}

public sealed record AgentRunPolicyEvaluation
{
    public required string RunId { get; init; }
    public required string CapabilityId { get; init; }
    public required AgentRunPolicyDecision Decision { get; init; }
    public required string PolicyName { get; init; }
    public required string PolicyReason { get; init; }
    public required DateTimeOffset EvaluatedAtUtc { get; init; }
    public string? ConstraintJson { get; init; }
    public string? ReviewPath { get; init; }
}

public sealed record ConfidenceSignal
{
    public required string SignalId { get; init; }
    public required string RunId { get; init; }
    public required double Confidence { get; init; }
    public required ValidationOutcome QualityOutcome { get; init; }
    public string? RationaleJson { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
}
