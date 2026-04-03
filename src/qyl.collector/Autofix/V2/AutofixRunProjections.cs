using Qyl.Contracts.Agenting;

namespace Qyl.Collector.Autofix.V2;

public sealed record AutofixRunProjection(
    string RunId,
    string IssueId,
    AgentRunPhase Phase,
    AgentRunStatus Status,
    int Attempt,
    int MaxAttempts,
    double? LatestConfidence,
    int PendingApprovalCount,
    int ArtifactCount,
    int ValidationCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record AutofixArtifactProjection(
    string ArtifactId,
    string RunId,
    AgentRunArtifactKind Kind,
    string Name,
    string Locator,
    string ContentType,
    string MimeType,
    DateTimeOffset ProducedAtUtc,
    string? SourceTool,
    string? SourceStep);

public sealed record AutofixApprovalProjection(
    string ApprovalId,
    string RunId,
    AgentRunPhase Phase,
    string CapabilityId,
    AgentRunApprovalStatus Status,
    string RequestedBy,
    string RequestedFor,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string? ArtifactId);

public sealed record AutofixValidationProjection(
    string CheckpointId,
    string RunId,
    string Name,
    ValidationOutcome Outcome,
    double Confidence,
    DateTimeOffset EvaluatedAtUtc,
    string? FailureReason);
