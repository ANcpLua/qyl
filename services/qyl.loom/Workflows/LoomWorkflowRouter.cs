// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;

namespace Qyl.Loom.Workflows;

/// <summary>
///     Deterministic, LLM-free router across the four Loom workflow shapes
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
    private static readonly string[] FixIssueTokens =
    [
        "fix sentry", "sentry error", "sentry errors", "sentry exception", "sentry exceptions",
        "production issue", "production bug", "debug production", "investigate exception",
        "resolve bug", "fix bug", "fix the bug", "investigate bug", "stack trace",
        "issue id", "sentry issue", "sentry event", "sentry backlog", "triage",
        "fix qyl issue", "qyl issue", "qyl error",
    ];

    private static readonly string[] BotReviewTokens =
    [
        "sentry[bot]", "seer-by-sentry[bot]", "sentry-io[bot]", "seer review",
        "sentry bot", "seer bot", "bot comment", "pr comment", "pr comments",
        "review comments", "address sentry review", "resolve sentry findings",
        "sentry feedback", "seer feedback", "@sentry review",
    ];

    private static readonly string[] SdkSetupTokens =
    [
        "add sentry", "install sentry", "set up sentry", "setup sentry",
        "configure sentry", "sentry for .net", "sentry for c#", "sentry.aspnetcore",
        "sentry.maui", "sentry.profiling", "sentry.extensions.logging",
        "sentry wizard", "sentry dsn", "sentrysdk.init", "usesentry",
        "error monitoring", "tracing setup", "logging setup", "metrics setup",
        "enable profiling", "cron monitoring", "hangfire sentry", "quartz sentry",
    ];

    private static readonly string[] AiMonitoringTokens =
    [
        "ai monitoring", "ai observability", "agent monitoring", "llm monitoring",
        "monitor llm", "monitor openai", "monitor anthropic", "track openai",
        "track anthropic", "token usage", "ai costs", "gen_ai", "gen ai",
        "tracessampler", "traces_sampler", "ai agent visibility", "openai span",
        "vercel ai sentry", "langchain sentry", "langgraph sentry",
        "microsoft.extensions.ai monitoring", "mcp.extensions.ai monitoring",
    ];

    /// <summary>
    ///     Route a user request. Case-insensitive token match against <paramref name="userRequest" />.
    ///     Structured signals in <paramref name="signals" /> (if provided) override keyword matches.
    /// </summary>
    public static LoomRouteDecision Route(string? userRequest, LoomRouteSignals? signals = null)
    {
        signals ??= LoomRouteSignals.Empty;

        if (signals.PullRequestNumber is int prNum && prNum > 0 && signals.ReviewBotAuthor is { Length: > 0 })
        {
            return new LoomRouteDecision
            {
                Kind = LoomWorkflowKind.ReviewBotPrComments,
                Confidence = 1.0,
                Rationale =
                    $"Signals include PR #{prNum} and bot author '{signals.ReviewBotAuthor}'. " +
                    "Bot-comment workflow is the only shape that consumes both.",
                PromptIds = [PromptIds.ReviewBotPrComments],
                MatchedSignals = ["signals.pull_request_number", "signals.review_bot_author"],
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
                MatchedSignals = ["signals.issue_id"],
            };
        }

        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return Clarify(
                "I need a starting point. Is this about: (1) fixing a live production issue, " +
                "(2) resolving review-bot PR comments, (3) installing the Sentry .NET SDK, " +
                "or (4) configuring AI/LLM monitoring?");
        }

        var normalized = userRequest.ToLowerInvariant();

        var fixMatches = FindMatches(normalized, FixIssueTokens);
        var botMatches = FindMatches(normalized, BotReviewTokens);
        var sdkMatches = FindMatches(normalized, SdkSetupTokens);
        var aiMatches = FindMatches(normalized, AiMonitoringTokens);

        var hits = (fixMatches.Length > 0 ? 1 : 0)
                   + (botMatches.Length > 0 ? 1 : 0)
                   + (sdkMatches.Length > 0 ? 1 : 0)
                   + (aiMatches.Length > 0 ? 1 : 0);

        if (hits == 0)
        {
            return Clarify(
                "I can't tell which Loom workflow you want. Is this about: " +
                "(1) fixing a live production issue, (2) resolving review-bot PR comments, " +
                "(3) installing the Sentry .NET SDK, or (4) configuring AI/LLM monitoring?");
        }

        if (hits == 1)
        {
            return fixMatches.Length > 0
                ? Decision(LoomWorkflowKind.FixProductionIssue, PromptIds.FixProductionIssue, fixMatches)
                : botMatches.Length > 0
                    ? Decision(LoomWorkflowKind.ReviewBotPrComments, PromptIds.ReviewBotPrComments, botMatches)
                    : sdkMatches.Length > 0
                        ? Decision(LoomWorkflowKind.SetupDotnetSdk, PromptIds.SetupDotnetSdk, sdkMatches)
                        : Decision(LoomWorkflowKind.SetupAiMonitoring, PromptIds.SetupAiMonitoring, aiMatches);
        }

        // AI monitoring paired with SDK setup is not ambiguous: SDK is prerequisite, AI is the goal.
        if (hits == 2 && sdkMatches.Length > 0 && aiMatches.Length > 0)
        {
            return new LoomRouteDecision
            {
                Kind = LoomWorkflowKind.SetupAiMonitoring,
                Confidence = 0.85,
                Rationale =
                    "Request mentions both SDK setup and AI monitoring. AI monitoring requires the base SDK, " +
                    "so the AI monitoring prompt is the entry point — it references the SDK setup as prerequisite.",
                PromptIds = [PromptIds.SetupAiMonitoring, PromptIds.SetupDotnetSdk],
                MatchedSignals = [..aiMatches, ..sdkMatches],
            };
        }

        return Clarify(
            "Your request touches multiple Loom workflows at once " +
            $"({DescribeHits(fixMatches, botMatches, sdkMatches, aiMatches)}). " +
            "Which one do you want to start with: fix a production issue, review PR bot comments, " +
            "set up the .NET SDK, or configure AI monitoring?");
    }

    private static ImmutableArray<string> FindMatches(string normalizedText, string[] tokens)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var token in tokens)
        {
            if (normalizedText.Contains(token, StringComparison.Ordinal))
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
            MatchedSignals = matches,
        };

    private static LoomRouteDecision Clarify(string question) =>
        new()
        {
            Kind = LoomWorkflowKind.Clarify,
            Confidence = 0,
            Rationale = "Request is ambiguous across the four Loom workflows. " +
                        "Router refuses to guess — asking a single clarifying question instead.",
            PromptIds = [],
            MatchedSignals = [],
            ClarifyingQuestion = question,
        };

    private static string DescribeHits(
        ImmutableArray<string> fix,
        ImmutableArray<string> bot,
        ImmutableArray<string> sdk,
        ImmutableArray<string> ai)
    {
        var parts = new List<string>(4);
        if (fix.Length > 0) parts.Add("fix-production");
        if (bot.Length > 0) parts.Add("bot-PR-review");
        if (sdk.Length > 0) parts.Add("SDK-setup");
        if (ai.Length > 0) parts.Add("AI-monitoring");
        return string.Join(" + ", parts);
    }

    /// <summary>MCP prompt names this router dispatches to. Stable — tests assert on them.</summary>
    public static class PromptIds
    {
        /// <summary>Fix a production issue workflow.</summary>
        public const string FixProductionIssue = "qyl.loom.fix_issue";

        /// <summary>Resolve review-bot PR comments.</summary>
        public const string ReviewBotPrComments = "qyl.loom.review_bot_pr";

        /// <summary>Install and configure the Sentry .NET SDK.</summary>
        public const string SetupDotnetSdk = "qyl.loom.setup_dotnet";

        /// <summary>Configure AI agent monitoring.</summary>
        public const string SetupAiMonitoring = "qyl.loom.setup_ai_monitoring";

        /// <summary>Router prompt — returns a routing directive the caller can act on.</summary>
        public const string Router = "qyl.loom.route";
    }
}
