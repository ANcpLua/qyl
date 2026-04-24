// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;

namespace Qyl.Loom.Workflows;

/// <summary>
///     Output of <see cref="LoomWorkflowRouter.Route" />. Carries the chosen workflow, the
///     deterministic reasoning behind it, and (when <see cref="Kind" /> is
///     <see cref="LoomWorkflowKind.Clarify" />) the one question the caller must answer
///     before Loom continues.
/// </summary>
public sealed record LoomRouteDecision
{
    /// <summary>The routed workflow, or <see cref="LoomWorkflowKind.Clarify" /> when ambiguous.</summary>
    public required LoomWorkflowKind Kind { get; init; }

    /// <summary>Score in [0, 1]. Exactly 1.0 for deterministic-signal matches; 0 for Clarify.</summary>
    public required double Confidence { get; init; }

    /// <summary>Short rationale — plain text, safe for a system message or a log entry.</summary>
    public required string Rationale { get; init; }

    /// <summary>
    ///     When <see cref="Kind" /> is <see cref="LoomWorkflowKind.Clarify" />, the single
    ///     disambiguating question to ask the caller. Null otherwise.
    /// </summary>
    public string? ClarifyingQuestion { get; init; }

    /// <summary>
    ///     Enumerated MCP prompt names the caller should fetch to execute the routed
    ///     workflow. Empty for Clarify. Names match <c>[McpServerPrompt(Name = ...)]</c>
    ///     registrations under <c>qyl.loom.*</c>.
    /// </summary>
    public required ImmutableArray<string> PromptIds { get; init; }

    /// <summary>Lowercased keyword tokens that drove the match. Empty for Clarify.</summary>
    public required ImmutableArray<string> MatchedSignals { get; init; }
}

/// <summary>
///     Optional structured signals accompanying a user request. Any non-null field is
///     treated as a deterministic override of keyword matching in
///     <see cref="LoomWorkflowRouter.Route" />.
/// </summary>
public sealed record LoomRouteSignals
{
    /// <summary>Singleton "no signals" instance.</summary>
    public static readonly LoomRouteSignals Empty = new();

    /// <summary>Pull request number, when the caller is already on a PR.</summary>
    public int? PullRequestNumber { get; init; }

    /// <summary>Review-bot author login (e.g. <c>sentry[bot]</c>, <c>seer-by-sentry[bot]</c>).</summary>
    public string? ReviewBotAuthor { get; init; }

    /// <summary>Issue id when the caller is already looking at an issue in qyl.</summary>
    public string? IssueId { get; init; }
}
