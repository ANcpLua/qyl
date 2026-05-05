
namespace Qyl.Contracts.Copilot;

public sealed record AgentEvidenceLink
{
    public required string Label { get; init; }

    public required string Href { get; init; }
}

public sealed record AgentDecision
{
    public required string DecisionId { get; init; }

    public required string RunId { get; init; }

    public string? TraceId { get; init; }

    public required string DecisionType { get; init; }

    public required string Outcome { get; init; }

    public bool RequiresApproval { get; init; }

    public string ApprovalStatus { get; init; } = "not_required";

    public string? Reason { get; init; }

    public IReadOnlyList<AgentEvidenceLink>? EvidenceLinks { get; init; }

    public ulong CreatedAtUnixNano { get; init; }

    public string? MetadataJson { get; init; }
}

public sealed record AgentRunAudit
{
    public required string RunId { get; init; }

    public string? TraceId { get; init; }

    public string TrackMode { get; init; } = "auto";

    public string ApprovalStatus { get; init; } = "not_required";

    public int DecisionCount { get; init; }

    public int EvidenceCount { get; init; }
}
