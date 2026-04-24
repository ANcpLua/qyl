using System.ComponentModel;
using ANcpLua.Roslyn.Utilities;
using Qyl.Loom.CodeReview;
using Qyl.Loom.Exploration;
using ModelContextProtocol.Server;

namespace Qyl.Loom.Agents;

/// <summary>
///     Primary MCP surface for qyl Loom. Exposes three tools — issue insight,
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

    [McpServerTool(Name = "loom_generate_pr_review", Title = "Generate Pull Request Review",
        ReadOnly = false, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Produces new PR review comments via qyl's LLM reviewer (distinct from `loom_parse_review_bot_comments` which consumes existing bot comments).")]
    public async Task<CodeReviewResult> ReviewPullRequestAsync(
        [Description("GitHub repo in `owner/repo` format.")] string repoFullName,
        [Description("Pull request number.")] int prNumber,
        CancellationToken ct = default) =>
        await codeReviewService.ReviewPullRequestAsync(repoFullName, prNumber, ct).ConfigureAwait(false);

    [McpServerTool(Name = "loom_autofix_setup_check", Title = "Autofix Pre-flight Check",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Pre-flight check for an autofix run: verifies the issue exists and the fix policy parses. Returns partial status for prerequisites that need external state (repo connection, integration scopes, code mapping, quota) — fetch MCP prompt `qyl.loom.autofix_setup_check` for the full agent directive to resolve those.")]
    public async Task<LoomAutofixSetupCheck> AutofixSetupCheckAsync(
        [Description("Issue identifier.")] string issueId,
        [Description("Fix policy: auto_apply, dry_run, or require_review. Defaults to require_review.")]
        string? policy = null,
        CancellationToken ct = default)
    {
        var issue = await collector.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        var parsedPolicy = ParseFixPolicy(policy);

        LoomAutofixCheck issueCheck = issue is null
            ? new(Name: "issue_exists", Status: "fail", Detail: $"Issue '{issueId}' not found in qyl collector.")
            : new(Name: "issue_exists", Status: "pass", Detail: $"Issue '{issueId}' resolved: {issue.ErrorType}.");

        LoomAutofixCheck policyCheck = new(
            Name: "policy",
            Status: "pass",
            Detail: $"Policy resolved to {parsedPolicy}.");

        LoomAutofixCheck repoCheck = new(
            Name: "repo_connection",
            Status: "unknown",
            Detail: "qyl collector does not currently expose project-integration state. Verify via the LLM-driven `qyl.loom.autofix_setup_check` prompt.");

        LoomAutofixCheck writeCheck = new(
            Name: "write_access",
            Status: "unknown",
            Detail: "Integration-scope introspection not available. Verify via the GitHub App settings page.");

        LoomAutofixCheck mappingCheck = new(
            Name: "code_mapping",
            Status: "unknown",
            Detail: "Stack-trace-to-repo mapping is not yet exposed as a tool. Stage-4 solution generation will surface mapping failures at the hunk level.");

        LoomAutofixCheck quotaCheck = new(
            Name: "quota",
            Status: "unknown",
            Detail: "Per-org run quota introspection not available. If a policy run stalls with no events, check collector rate limits.");

        var passed = issue is not null;
        var decision = passed ? "proceed" : "cannot_proceed";

        return new LoomAutofixSetupCheck(
            IssueId: issueId,
            Policy: parsedPolicy.ToString(),
            Checks: [issueCheck, policyCheck, repoCheck, writeCheck, mappingCheck, quotaCheck],
            Decision: decision,
            AgentPromptId: "qyl.loom.autofix_setup_check");
    }

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

/// <summary>
///     Structured result of <see cref="LoomGodAnalyzerServer.AutofixSetupCheckAsync" />.
///     Combines tool-checkable prerequisites (issue existence, policy parse) with
///     <c>unknown</c> placeholders for checks that require external state the collector
///     does not currently expose.
/// </summary>
/// <param name="IssueId">The issue id the caller supplied.</param>
/// <param name="Policy">Resolved fix policy string (result of <see cref="FixPolicy" /> parse).</param>
/// <param name="Checks">Named checks: issue_exists, policy, repo_connection, write_access, code_mapping, quota.</param>
/// <param name="Decision"><c>proceed</c> when all programmatic checks pass; <c>cannot_proceed</c> otherwise.</param>
/// <param name="AgentPromptId">MCP prompt id carrying the full agent directive for resolving <c>unknown</c> checks.</param>
public sealed record LoomAutofixSetupCheck(
    string IssueId,
    string Policy,
    IReadOnlyList<LoomAutofixCheck> Checks,
    string Decision,
    string AgentPromptId);

/// <summary>A single named check inside <see cref="LoomAutofixSetupCheck" />.</summary>
/// <param name="Name">Check identifier (e.g. <c>issue_exists</c>, <c>policy</c>, <c>repo_connection</c>).</param>
/// <param name="Status"><c>pass</c>, <c>fail</c>, or <c>unknown</c>.</param>
/// <param name="Detail">Human-readable explanation of the status.</param>
public sealed record LoomAutofixCheck(string Name, string Status, string Detail);
