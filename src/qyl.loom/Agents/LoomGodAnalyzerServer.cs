namespace Qyl.Loom.Agents;

using System.ComponentModel;
using ANcpLua.Roslyn.Utilities;
using CodeReview;
using Exploration;
using ModelContextProtocol.Server;

/// <summary>
///     Seer-grade MCP surface for qyl Loom. Exposes three tools — issue insight,
///     autofix launch, pull-request review — backed by qyl collector services.
///     Tool dispatch, schema generation, and protocol handling come from the
///     official MCP C# SDK; this type only carries attributes and business logic.
/// </summary>
[McpServerToolType]
public sealed class LoomGodAnalyzerServer(
    CollectorClient collector,
    AutofixOrchestrator autofixOrchestrator,
    ExplorationInsightService insightService,
    CodeReviewService codeReviewService)
{
    [McpServerTool(Name = "loom_get_issue_insight", Title = "Loom Issue Insight",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Generate pre-investigation Loom insight for an issue id.")]
    public async Task<ExplorationInsight?> GetIssueInsightAsync(
        [Description("Issue identifier to analyze.")] string issueId,
        CancellationToken ct = default) =>
        await insightService.GenerateInsightAsync(issueId, ct).ConfigureAwait(false);

    [McpServerTool(Name = "loom_start_fix_run", Title = "Start Autofix Run",
        ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Create an autofix run for an issue using the selected policy.")]
    public async Task<FixRunRecord?> StartFixRunAsync(
        [Description("Issue identifier.")] string issueId,
        [Description("Fix policy: auto_apply, dry_run, or require_review. Defaults to require_review.")]
        string? policy = null,
        CancellationToken ct = default)
    {
        var issue = await collector.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        if (issue is null)
            return null;

        return await autofixOrchestrator
            .CreateFixRunAsync(issueId, ParseFixPolicy(policy), ct: ct)
            .ConfigureAwait(false);
    }

    [McpServerTool(Name = "loom_review_pull_request", Title = "Review Pull Request",
        ReadOnly = false, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Run Loom code review against a GitHub owner/repo pull request.")]
    public async Task<CodeReviewResult> ReviewPullRequestAsync(
        [Description("GitHub repo in `owner/repo` format.")] string repoFullName,
        [Description("Pull request number.")] int prNumber,
        CancellationToken ct = default) =>
        await codeReviewService.ReviewPullRequestAsync(repoFullName, prNumber, ct).ConfigureAwait(false);

    private static FixPolicy ParseFixPolicy(string? policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
            return FixPolicy.RequireReview;

        var normalized = policy
            .Trim()
            .ReplaceOrdinal("-", string.Empty)
            ?.ReplaceOrdinal("_", string.Empty)
            ?.ToUpperInvariant() ?? string.Empty;

        return normalized switch
        {
            "AUTOAPPLY" => FixPolicy.AutoApply,
            "DRYRUN" => FixPolicy.DryRun,
            "REQUIREREVIEW" => FixPolicy.RequireReview,
            _ when Enum.TryParse<FixPolicy>(policy, true, out var parsed) => parsed,
            _ => FixPolicy.RequireReview
        };
    }
}
