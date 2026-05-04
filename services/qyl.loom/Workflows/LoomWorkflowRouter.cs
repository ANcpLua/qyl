// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;

namespace Qyl.Loom.Workflows;

/// <summary>
///     Deterministic, LLM-free router across the three Loom workflow shapes
///     (<see cref="LoomWorkflowKind" />). Matches on keyword tokens in the caller's request
///     plus optional structured signals (PR number, bot author, issue id). Returns
///     <see cref="LoomWorkflowKind.Clarify" /> with one focused question when signals
///     conflict — the router never silently picks between two plausible workflows.
/// </summary>
/// <remarks>
///     Pure static surface. No DI, no IO, no LLM. Callable from MCP tools, tests, and
///     future workflow executors without any wiring.
/// </remarks>
public static class LoomWorkflowRouter
{
    private static readonly string[] s_fixIssueTokens =
    [
        "fix qyl", "qyl error", "qyl errors", "qyl exception", "qyl exceptions",
        "production issue", "production bug", "debug production", "investigate exception",
        "resolve bug", "fix bug", "fix the bug", "investigate bug", "stack trace",
        "issue id", "qyl issue", "qyl event", "qyl backlog", "triage",
        "fix qyl issue"
    ];

    private static readonly string[] s_botReviewTokens =
    [
        "qyl[bot]", "qyl-review[bot]", "qyl review bot", "qyl bot comment",
        "bot comment", "pr comment", "pr comments", "review comments",
        "address qyl review", "resolve qyl findings", "qyl feedback", "@qyl review"
    ];

    private static readonly string[] s_autofixTokens =
    [
        "autofix", "auto-fix", "run loom on", "run the loom pipeline",
        "generate a pr for", "open a pr for the fix", "headless fix",
        "fixability score", "autofix run", "autofix pipeline",
        "autofix this issue", "autofix issue", "loom autofix"
    ];

    /// <summary>
    ///     Route a user request. Case-insensitive token match against <paramref name="userRequest" />.
    ///     Structured signals in <paramref name="signals" /> (if provided) override keyword matches.
    /// </summary>
    public static LoomRouteDecision Route(string? userRequest, LoomRouteSignals? signals = null)
    {
        signals ??= LoomRouteSignals.Empty;

        if (signals.PullRequestNumber is > 0 && signals.ReviewBotAuthor is { Length: > 0 })
        {
            return new LoomRouteDecision
            {
                Kind = LoomWorkflowKind.ReviewBotPrComments,
                Confidence = 1.0,
                Rationale =
                    $"Signals include PR #{signals.PullRequestNumber} and bot author '{signals.ReviewBotAuthor}'. " +
                    "Bot-comment workflow is the only shape that consumes both.",
                PromptIds = [PromptIds.ReviewBotPrComments],
                MatchedSignals = ["signals.pull_request_number", "signals.review_bot_author"]
            };
        }

        if (signals.IssueId is { Length: > 0 } issueId)
        {
            return new LoomRouteDecision
            {
                Kind = LoomWorkflowKind.FixProductionIssue,
                Confidence = 1.0,
                Rationale =
                    $"Signals include issue id '{issueId}'. Fix-production workflow is the only shape that consumes a qyl issue id.",
                PromptIds = [PromptIds.FixProductionIssue],
                MatchedSignals = ["signals.issue_id"]
            };
        }

        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return Clarify(
                "I need a starting point. Is this about: (1) fixing a live production issue, " +
                "(2) resolving qyl review-bot PR comments, " +
                "or (3) running the headless autofix pipeline?");
        }

        var normalized = userRequest.ToLowerInvariant();

        var fixMatches = FindMatches(normalized, s_fixIssueTokens);
        var botMatches = FindMatches(normalized, s_botReviewTokens);
        var autofixMatches = FindMatches(normalized, s_autofixTokens);

        // Autofix is more specific than FixProductionIssue — a request mentioning both
        // "autofix" and generic fix tokens ("debug", "fix bug") routes to Autofix.
        if (autofixMatches.Length > 0 && fixMatches.Length > 0)
        {
            return new LoomRouteDecision
            {
                Kind = LoomWorkflowKind.Autofix,
                Confidence = 0.9,
                Rationale =
                    "Request mentions autofix-specific tokens alongside generic fix tokens. " +
                    "Autofix is the specific shape — headless pipeline with structured artifact.",
                PromptIds = [PromptIds.Autofix, PromptIds.FixProductionIssue],
                MatchedSignals = [..autofixMatches, ..fixMatches]
            };
        }

        var hits = (fixMatches.Length > 0 ? 1 : 0)
                   + (botMatches.Length > 0 ? 1 : 0)
                   + (autofixMatches.Length > 0 ? 1 : 0);

        if (hits == 0)
        {
            return Clarify(
                "I can't tell which Loom workflow you want. Is this about: " +
                "(1) fixing a live production issue, (2) resolving qyl review-bot PR comments, " +
                "or (3) running the headless autofix pipeline?");
        }

        if (hits == 1)
        {
            return autofixMatches.Length > 0
                ? Decision(LoomWorkflowKind.Autofix, PromptIds.Autofix, autofixMatches)
                : fixMatches.Length > 0
                    ? Decision(LoomWorkflowKind.FixProductionIssue, PromptIds.FixProductionIssue, fixMatches)
                    : Decision(LoomWorkflowKind.ReviewBotPrComments, PromptIds.ReviewBotPrComments, botMatches);
        }

        return Clarify(
            "Your request touches multiple Loom workflows at once " +
            $"({DescribeHits(fixMatches, botMatches, autofixMatches)}). " +
            "Which one do you want to start with: fix a production issue, review PR bot comments, " +
            "or run the headless autofix pipeline?");
    }

    private static ImmutableArray<string> FindMatches(string normalizedText, string[] tokens)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var token in tokens)
        {
            if (normalizedText.ContainsOrdinal(token))
                builder.Add(token);
        }

        return builder.ToImmutable();
    }

    private static LoomRouteDecision Decision(
        LoomWorkflowKind kind,
        string primaryPromptId,
        ImmutableArray<string> matches) =>
        new()
        {
            Kind = kind,
            Confidence = 1.0,
            Rationale =
                $"Matched {matches.Length} signal(s) for {kind}: " + string.Join(", ", matches) +
                ". No other workflow signals detected.",
            PromptIds = [primaryPromptId],
            MatchedSignals = matches
        };

    private static LoomRouteDecision Clarify(string question) =>
        new()
        {
            Kind = LoomWorkflowKind.Clarify,
            Confidence = 0,
            Rationale = "Request is ambiguous across the three Loom workflows. " +
                        "Router refuses to guess — asking a single clarifying question instead.",
            PromptIds = [],
            MatchedSignals = [],
            ClarifyingQuestion = question
        };

    private static string DescribeHits(
        ImmutableArray<string> fix,
        ImmutableArray<string> bot,
        ImmutableArray<string> autofix)
    {
        var parts = new List<string>(3);
        if (fix.Length > 0) parts.Add("fix-production");
        if (bot.Length > 0) parts.Add("bot-PR-review");
        if (autofix.Length > 0) parts.Add("autofix");
        return string.Join(" + ", parts);
    }

    /// <summary>MCP prompt names this router dispatches to. Stable — tests assert on them.</summary>
    public static class PromptIds
    {
        /// <summary>Fix a production issue workflow.</summary>
        public const string FixProductionIssue = "qyl.loom.fix_issue";

        /// <summary>Resolve review-bot PR comments.</summary>
        public const string ReviewBotPrComments = "qyl.loom.review_bot_pr";

        /// <summary>Headless autofix pipeline — fixability gate + structured artifact.</summary>
        public const string Autofix = "qyl.loom.autofix_system";

        /// <summary>
        ///     Deprecated — the deterministic router is the source of truth; the LLM-side
        ///     routing prompt was removed. Constant retained so callers that still reference
        ///     <c>PromptIds.Router</c> keep compiling; nothing serves this id any more.
        /// </summary>
        public const string Router = "qyl.loom.route";
    }
}
