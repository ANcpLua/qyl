namespace Qyl.Contracts.Agenting;

public enum AgentRunPhase
{
    Intake,
    Context,
    Diagnose,
    Plan,
    Patch,
    Validate,
    Score,
    Gate,
    Apply,
    Observe,
    Publish,
    Closed
}

public enum AgentRunStatus
{
    Pending,
    Running,
    WaitingApproval,
    BlockedByPolicy,
    Completed,
    Failed,
    Cancelled,
    Escalated
}

public enum AgentRunArtifactKind
{
    Context,
    Detection,
    RootCause,
    Plan,
    Patch,
    Validation,
    Score,
    Diff,
    EvidencePack,
    Report,
    ApprovalRecord
}

public enum AgentCapabilityScope
{
    Read,
    Write,
    Execute,
    Admin
}

public enum AgentRunApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Expired,
    Withdrawn
}

public enum AgentRunPolicyDecision
{
    Allowed,
    Blocked,
    RequiresApproval,
    RequiresEscalation,
    Deferred
}

public enum ValidationOutcome
{
    Unknown,
    Pass,
    Fail,
    Warning,
    NotApplicable
}

public sealed record AgentCapabilityDescriptor
{
    public required string CapabilityId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public AgentCapabilityScope DefaultScope { get; init; }
    public required IReadOnlyList<string> AllowedSubjects { get; init; }
    public bool RequiresAuditableApproval { get; init; }
    public int? MaxAttemptsOverride { get; init; }
}
