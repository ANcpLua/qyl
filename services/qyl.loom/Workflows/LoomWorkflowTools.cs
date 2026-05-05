using ANcpLua.Roslyn.Utilities;

using System.ComponentModel;
using ModelContextProtocol.Server;
using Qyl.Loom.Workflows.ReviewBot;

namespace Qyl.Loom.Workflows;

[McpServerToolType]
public sealed partial class LoomWorkflowTools
{
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

    [McpServerTool(Name = "loom_parse_review_bot_comments", Title = "Parse review-bot PR comments",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Parse a JSON array of GitHub PR review comments. Filters to qyl review bots by default (exact, case-insensitive login match); pass additionalBotLoginsJson (JSON string[]) to opt in foreign review bots (e.g. loom[bot]). Returns structured bug/severity/confidence/analysis/fix/prompt per comment.")]
    public static LoomReviewBotParseResult ParseReviewBotComments(
        string commentsJson,
        [Description(
            "Optional JSON array of extra bot logins to accept in addition to the qyl defaults (e.g. [\"loom[bot]\"]). Exact, case-insensitive match.")]
        string? additionalBotLoginsJson = null)
    {
        Guard.NotNullOrEmpty(commentsJson);

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

public sealed record LoomReviewBotParseResult
{
    public required int InputCount { get; init; }

    public required int ParsedCount { get; init; }

    public required string Summary { get; init; }

    public required ReviewBotComment[] Comments { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ReviewBotRawComment[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(LoomReviewBotParseResult))]
internal sealed partial class LoomWorkflowToolsJsonContext : JsonSerializerContext;
