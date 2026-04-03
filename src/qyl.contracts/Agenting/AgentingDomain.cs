using System;
using System.Collections.Generic;

namespace Qyl.Contracts.Agenting;

/// <summary>
/// Canonical lifecycle phases for a run under the agent/control and governance model.
/// </summary>
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

/// <summary>
/// High-level run status emitted by automation and control systems.
/// </summary>
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

/// <summary>
/// Artifact kinds persisted or derived during an agentic run.
/// </summary>
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

/// <summary>
/// Capability scope for actions taken by an agent run.
/// </summary>
public enum AgentCapabilityScope
{
    Read,
    Write,
    Execute,
    Admin
}

/// <summary>
/// Approval decision for a gated run transition.
/// </summary>
public enum AgentRunApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Expired,
    Withdrawn
}

/// <summary>
/// Governance decision after policy evaluation.
/// </summary>
public enum AgentRunPolicyDecision
{
    Allowed,
    Blocked,
    RequiresApproval,
    RequiresEscalation,
    Deferred
}

/// <summary>
/// Confidence/validation outcome for a run checkpoint.
/// </summary>
public enum ValidationOutcome
{
    Unknown,
    Pass,
    Fail,
    Warning,
    NotApplicable
}

/// <summary>
/// Capability declaration used by policy/ledger to gate tool and workflow behavior.
/// </summary>
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
