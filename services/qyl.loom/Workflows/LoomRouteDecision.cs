
using System.Collections.Immutable;

namespace Qyl.Loom.Workflows;

public sealed record LoomRouteDecision
{
    public required LoomWorkflowKind Kind { get; init; }

    public required double Confidence { get; init; }

    public required string Rationale { get; init; }

    public string? ClarifyingQuestion { get; init; }

    public required ImmutableArray<string> PromptIds { get; init; }

    public required ImmutableArray<string> MatchedSignals { get; init; }
}

public sealed record LoomRouteSignals
{
    public static readonly LoomRouteSignals Empty = new();

    public int? PullRequestNumber { get; init; }

    public string? ReviewBotAuthor { get; init; }

    public string? IssueId { get; init; }
}
