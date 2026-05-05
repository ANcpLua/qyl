namespace Qyl.Contracts.Agenting;

public sealed record LoomContinuationToken
{
    public required string SessionId { get; init; }
    public required string IssueId { get; init; }
    public required LoomSpecialistRole LastSpecialist { get; init; }
    public required AgentRunPhase ResumePhase { get; init; }
    public required DateTimeOffset IssuedAtUtc { get; init; }
    public int TurnCount { get; init; }
    public string? CheckpointId { get; init; }
    public string? CorrelationId { get; init; }
}

public enum LoomSpecialistRole
{
    User,
    Diagnostician,
    Strategist,
    Coder,
    Reviewer,
    Orchestrator
}

public sealed record LoomSessionEntry
{
    public required LoomSpecialistRole Role { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public AgentRunPhase? Phase { get; init; }
    public string? ArtifactId { get; init; }
}

public sealed record LoomSessionSnapshot
{
    public required string SessionId { get; init; }
    public required string IssueId { get; init; }
    public required AgentRunPhase CurrentPhase { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public string? UserContext { get; init; }
    public string? ContextBlock { get; init; }
    public string? DiagnosticTranscript { get; init; }
    public string? RootCauseJson { get; init; }
    public string? SolutionJson { get; init; }
    public int TurnCount { get; init; }
    public required IReadOnlyList<LoomSessionEntry> Transcript { get; init; }

    public LoomContinuationToken IssueContinuation(LoomSpecialistRole lastSpecialist, AgentRunPhase resumePhase) =>
        new()
        {
            SessionId = SessionId,
            IssueId = IssueId,
            LastSpecialist = lastSpecialist,
            ResumePhase = resumePhase,
            IssuedAtUtc = UpdatedAtUtc,
            TurnCount = TurnCount,
            CorrelationId = SessionId
        };
}
