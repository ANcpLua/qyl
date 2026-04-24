// Copyright (c) 2025-2026 ancplua

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Qyl.Loom.Workflows.Detection;
using Qyl.Loom.Workflows.ReviewBot;

namespace Qyl.Loom.Workflows;

/// <summary>
///     MCP tools that expose Loom's workflow-router + detection + review-bot primitives to
///     external callers. Every tool is a thin wrapper around the pure static primitives —
///     no LLM, no side effects.
/// </summary>
[McpServerToolType]
public sealed class LoomWorkflowTools
{
    [McpServerTool(Name = "loom_route", Title = "Loom Workflow Router",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Route a user request across the four Loom workflows (fix_issue / review_bot_pr / setup_dotnet / setup_ai_monitoring). Returns clarifying question when ambiguous.")]
    public static LoomRouteDecision Route(
        [Description("User request in natural language.")] string userRequest,
        [Description("Optional PR number when caller is on a specific PR.")] int? pullRequestNumber = null,
        [Description("Optional review-bot author login (e.g. 'sentry[bot]', 'seer-by-sentry[bot]').")] string? reviewBotAuthor = null,
        [Description("Optional issue id when caller is on a specific issue.")] string? issueId = null)
    {
        var signals = new LoomRouteSignals
        {
            PullRequestNumber = pullRequestNumber,
            ReviewBotAuthor = reviewBotAuthor,
            IssueId = issueId,
        };
        return LoomWorkflowRouter.Route(userRequest, signals);
    }

    [McpServerTool(Name = "loom_detect_dotnet", Title = "Detect .NET project shape",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = true)]
    [Description("Scan a repo root, classify the .NET app shape, surface Sentry/logging/scheduler/AI-SDK evidence, and produce the setup recommendations.")]
    public static DotnetProjectEvidence DetectDotnet(
        [Description("Absolute path to the repo or folder root to scan.")] string repoRoot) =>
        DotnetProjectDetector.Detect(repoRoot);

    [McpServerTool(Name = "loom_parse_review_bot_comments", Title = "Parse review-bot PR comments",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Parse a JSON array of GitHub PR review comments. Filters to Sentry/Seer bots; returns structured bug/severity/confidence/analysis/fix/prompt per comment.")]
    public static LoomReviewBotParseResult ParseReviewBotComments(
        [Description("JSON array of GitHub review comments. Each item: { author, file, line, body }. Non-Sentry bots are silently dropped.")]
        string commentsJson)
    {
        ArgumentException.ThrowIfNullOrEmpty(commentsJson);

        var raw = JsonSerializer.Deserialize(
            commentsJson,
            LoomWorkflowToolsJsonContext.Default.ReviewBotRawCommentArray) ?? [];

        var parsed = ReviewBotCommentParser.Parse(raw);
        var summary = ReviewBotCommentParser.BuildSummary(parsed);

        return new LoomReviewBotParseResult
        {
            InputCount = raw.Length,
            ParsedCount = parsed.Length,
            Summary = summary,
            Comments = parsed.ToArray(),
        };
    }

    [McpServerTool(Name = "loom_plan_task", Title = "Plan a Loom workflow task end-to-end",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = true)]
    [Description("One-shot orchestrator: routes the request, runs detection (for setup workflows) or accepts pre-fetched bot comments (for review), and returns the prompt ids the caller should fetch next.")]
    public static LoomTaskPlan PlanTask(
        [Description("User request in natural language.")] string userRequest,
        [Description("Absolute path to the repo root. Used by setup workflows. Optional.")]
        string? repoRoot = null,
        [Description("Optional PR number for review-bot workflow.")] int? pullRequestNumber = null,
        [Description("Optional review-bot author login.")] string? reviewBotAuthor = null,
        [Description("Optional issue id for fix-production workflow.")] string? issueId = null,
        [Description("Optional JSON array of review-bot comments. When provided together with a review-bot route, it is parsed into the plan.")]
        string? reviewBotCommentsJson = null)
    {
        var decision = LoomWorkflowRouter.Route(userRequest, new LoomRouteSignals
        {
            PullRequestNumber = pullRequestNumber,
            ReviewBotAuthor = reviewBotAuthor,
            IssueId = issueId,
        });

        DotnetProjectEvidence? detection = null;
        LoomReviewBotParseResult? reviewBot = null;

        if (decision.Kind is LoomWorkflowKind.SetupDotnetSdk or LoomWorkflowKind.SetupAiMonitoring
            && !string.IsNullOrWhiteSpace(repoRoot))
        {
            detection = DotnetProjectDetector.Detect(repoRoot);
        }

        if (decision.Kind is LoomWorkflowKind.ReviewBotPrComments
            && !string.IsNullOrWhiteSpace(reviewBotCommentsJson))
        {
            reviewBot = ParseReviewBotComments(reviewBotCommentsJson);
        }

        return new LoomTaskPlan
        {
            Decision = decision,
            Detection = detection,
            ReviewBot = reviewBot,
        };
    }
}

/// <summary>Parsed review-bot comments returned by <see cref="LoomWorkflowTools.ParseReviewBotComments" />.</summary>
public sealed record LoomReviewBotParseResult
{
    /// <summary>Number of raw comments deserialised from the JSON input.</summary>
    public required int InputCount { get; init; }

    /// <summary>Number of comments that passed the Sentry/Seer bot filter.</summary>
    public required int ParsedCount { get; init; }

    /// <summary>Human-readable summary, ordered by severity/confidence.</summary>
    public required string Summary { get; init; }

    /// <summary>Structured parsed comments.</summary>
    public required ReviewBotComment[] Comments { get; init; }
}

/// <summary>
///     End-to-end plan from <see cref="LoomWorkflowTools.PlanTask" />. Carries the router
///     decision plus any workflow-specific payload (detection or parsed bot comments). The
///     caller fetches the MCP prompt ids in <see cref="LoomRouteDecision.PromptIds" />.
/// </summary>
public sealed record LoomTaskPlan
{
    /// <summary>Router decision. If <see cref="LoomWorkflowKind.Clarify" />, inspect <c>ClarifyingQuestion</c>.</summary>
    public required LoomRouteDecision Decision { get; init; }

    /// <summary>Detection evidence for setup workflows. Null otherwise.</summary>
    public DotnetProjectEvidence? Detection { get; init; }

    /// <summary>Parsed bot comments for review workflows. Null otherwise.</summary>
    public LoomReviewBotParseResult? ReviewBot { get; init; }
}

/// <summary>
///     Source-gen JSON context for <see cref="LoomWorkflowTools.ParseReviewBotComments" />.
///     Avoids runtime reflection, satisfies trim/AOT analysis.
/// </summary>
[JsonSerializable(typeof(ReviewBotRawComment[]))]
[JsonSerializable(typeof(LoomReviewBotParseResult))]
[JsonSerializable(typeof(LoomTaskPlan))]
internal sealed partial class LoomWorkflowToolsJsonContext : JsonSerializerContext;
