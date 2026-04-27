// Copyright (c) 2025-2026 ancplua

using System.ComponentModel;
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
public sealed partial class LoomWorkflowTools
{
    /// <summary>
    ///     Route a user request across the Loom workflow shapes. Returns a
    ///     <see cref="LoomRouteDecision" /> — inspect <c>Kind</c> for the workflow and
    ///     <c>PromptIds</c> for the MCP prompts the caller should fetch next.
    /// </summary>
    [McpServerTool(Name = "loom_route", Title = "Loom Workflow Router",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    public static partial LoomRouteDecision Route(
        string userRequest,
        int? pullRequestNumber = null,
        [Description("Optional review-bot author login (e.g. 'qyl[bot]', 'qyl-review[bot]').")]
        string? reviewBotAuthor = null,
        string? issueId = null)
    {
        var signals = new LoomRouteSignals
        {
            PullRequestNumber = pullRequestNumber, ReviewBotAuthor = reviewBotAuthor, IssueId = issueId
        };
        return LoomWorkflowRouter.Route(userRequest, signals);
    }

    /// <summary>
    ///     Scan <paramref name="repoRoot" /> and return structured detection evidence used
    ///     by the setup-dotnet / setup-ai-monitoring prompts. Never guesses — empty fields
    ///     mean "no evidence found".
    /// </summary>
    [McpServerTool(Name = "loom_detect_dotnet", Title = "Detect .NET project shape",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = true)]
    public static partial DotnetProjectEvidence DetectDotnet(
        string repoRoot) =>
        DotnetProjectDetector.Detect(repoRoot);

    /// <summary>
    ///     Parse a JSON array of GitHub PR review comments through the deterministic
    ///     <see cref="ReviewBotCommentParser" />. Non-bot authors are dropped; extra bot
    ///     logins may be opted in via <paramref name="additionalBotLoginsJson" />.
    /// </summary>
    [McpServerTool(Name = "loom_parse_review_bot_comments", Title = "Parse review-bot PR comments",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Parse a JSON array of GitHub PR review comments. Filters to qyl review bots by default (exact, case-insensitive login match); pass additionalBotLoginsJson (JSON string[]) to opt in foreign review bots (Sentry, Seer, etc.). Returns structured bug/severity/confidence/analysis/fix/prompt per comment.")]
    public static LoomReviewBotParseResult ParseReviewBotComments(
        string commentsJson,
        [Description(
            "Optional JSON array of extra bot logins to accept in addition to the qyl defaults (e.g. [\"sentry[bot]\", \"seer-by-sentry[bot]\"]). Exact, case-insensitive match.")]
        string? additionalBotLoginsJson = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(commentsJson);

        var raw = JsonSerializer.Deserialize(
            commentsJson,
            LoomWorkflowToolsJsonContext.Default.ReviewBotRawCommentArray) ?? [];

        IReadOnlyCollection<string>? additionalBotLogins = null;
        if (!string.IsNullOrWhiteSpace(additionalBotLoginsJson))
        {
            additionalBotLogins = JsonSerializer.Deserialize(
                additionalBotLoginsJson,
                LoomWorkflowToolsJsonContext.Default.StringArray);
        }

        var parsed = ReviewBotCommentParser.Parse(raw, additionalBotLogins);
        var summary = ReviewBotCommentParser.BuildSummary(parsed);

        return new LoomReviewBotParseResult
        {
            InputCount = raw.Length, ParsedCount = parsed.Length, Summary = summary, Comments = [.. parsed]
        };
    }
}

/// <summary>Parsed review-bot comments returned by <see cref="LoomWorkflowTools.ParseReviewBotComments" />.</summary>
public sealed record LoomReviewBotParseResult
{
    /// <summary>Number of raw comments deserialised from the JSON input.</summary>
    public required int InputCount { get; init; }

    /// <summary>Number of comments that passed the bot-login filter.</summary>
    public required int ParsedCount { get; init; }

    /// <summary>Human-readable summary, ordered by severity/confidence.</summary>
    public required string Summary { get; init; }

    /// <summary>Structured parsed comments.</summary>
    public required ReviewBotComment[] Comments { get; init; }
}

/// <summary>
///     Source-gen JSON context for <see cref="LoomWorkflowTools" />. Emits camelCase
///     property names so outputs align with the <c>loom-sdk-onboarding</c> skill and
///     prompt contracts; avoids runtime reflection, satisfies trim / AOT analysis.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ReviewBotRawComment[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(LoomReviewBotParseResult))]
[JsonSerializable(typeof(DotnetProjectEvidence))]
[JsonSerializable(typeof(DotnetFeatureRecommendations))]
internal sealed partial class LoomWorkflowToolsJsonContext : JsonSerializerContext;
