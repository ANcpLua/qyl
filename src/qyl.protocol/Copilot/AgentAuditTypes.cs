// =============================================================================
// qyl.protocol - Agent Audit Types
// Structured agent run and decision contracts for tri-track auditing.
// =============================================================================

namespace qyl.protocol.Copilot;

/// <summary>
///     Evidence link attached to an agent decision.
/// </summary>
public sealed record AgentEvidenceLink
{
    /// <summary>Human-readable label for the evidence link.</summary>
    public required string Label { get; init; }

    /// <summary>Relative or absolute URL to evidence.</summary>
    public required string Href { get; init; }
}

/// <summary>
///     Structured decision event emitted during an agent run.
/// </summary>
public sealed record AgentDecision
{
    /// <summary>Unique decision ID.</summary>
    public required string DecisionId { get; init; }

    /// <summary>Owning run ID.</summary>
    public required string RunId { get; init; }

    /// <summary>Associated trace ID if available.</summary>
    public string? TraceId { get; init; }

    /// <summary>Decision category (routing, tool, approval, policy, evidence).</summary>
    public required string DecisionType { get; init; }

    /// <summary>Decision outcome (selected, approved, denied, failed, etc.).</summary>
    public required string Outcome { get; init; }

    /// <summary>Whether this decision required explicit approval.</summary>
    public bool RequiresApproval { get; init; }

    /// <summary>
    ///     Approval status for this decision:
    ///     not_required, awaiting_approval, approved, denied.
    /// </summary>
    public string ApprovalStatus { get; init; } = "not_required";

    /// <summary>Model-readable reasoning text.</summary>
    public string? Reason { get; init; }

    /// <summary>Structured evidence links backing this decision.</summary>
    public IReadOnlyList<AgentEvidenceLink>? EvidenceLinks { get; init; }

    /// <summary>Decision timestamp in unix nanoseconds.</summary>
    public ulong CreatedAtUnixNano { get; init; }

    /// <summary>Optional metadata payload as JSON text.</summary>
    public string? MetadataJson { get; init; }
}

/// <summary>
///     Structured run-level audit summary.
/// </summary>
public sealed record AgentRunAudit
{
    /// <summary>Unique run ID.</summary>
    public required string RunId { get; init; }

    /// <summary>Associated trace ID if available.</summary>
    public string? TraceId { get; init; }

    /// <summary>
    ///     Effective track mode:
    ///     auto, creative, reasoning, enterprise.
    /// </summary>
    public string TrackMode { get; init; } = "auto";

    /// <summary>
    ///     Aggregate approval status:
    ///     not_required, awaiting_approval, approved, denied.
    /// </summary>
    public string ApprovalStatus { get; init; } = "not_required";

    /// <summary>Total number of decision events captured for this run.</summary>
    public int DecisionCount { get; init; }

    /// <summary>Total number of evidence links captured for this run.</summary>
    public int EvidenceCount { get; init; }
}
