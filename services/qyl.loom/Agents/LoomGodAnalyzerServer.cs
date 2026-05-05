using ModelContextProtocol.Server;
using Qyl.Loom.Autofix;
using Qyl.Loom.CodeReview;
using Qyl.Loom.Exploration;

namespace Qyl.Loom.Agents;

[McpServerToolType]
public sealed partial class LoomGodAnalyzerServer(
    CollectorClient collector,
    AutofixOrchestrator autofixOrchestrator,
    ExplorationInsightService insightService,
    CodeReviewService codeReviewService)
{
    [McpServerTool(Name = "loom_get_issue_insight", Title = "Loom Issue Insight",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    public async partial Task<ExplorationInsight?> GetIssueInsightAsync(
        string issueId,
        CancellationToken ct = default) =>
        await insightService.GenerateInsightAsync(issueId, ct).ConfigureAwait(false);

    [McpServerTool(Name = "loom_start_fix_run", Title = "Start Autofix Run",
        ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    public async partial Task<FixRunRecord?> StartFixRunAsync(
        string issueId,
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
    public async partial Task<CodeReviewResult> ReviewPullRequestAsync(
        string repoFullName,
        int prNumber,
        CancellationToken ct = default) =>
        await codeReviewService.ReviewPullRequestAsync(repoFullName, prNumber, ct).ConfigureAwait(false);

    [McpServerTool(Name = "loom_autofix_update", Title = "Append Instruction to Fix Run",
        ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    public async partial Task<LoomAutofixUpdateResult> UpdateFixRunAsync(
        string issueId,
        string runId,
        string instruction,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return new LoomAutofixUpdateResult(false, runId, "instruction is required and cannot be empty.");

        try
        {
            await collector.AppendFixRunInstructionAsync(issueId, runId, instruction, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return new LoomAutofixUpdateResult(
                false,
                runId,
                $"Collector rejected the update: {ex.Message}");
        }

        return new LoomAutofixUpdateResult(true, runId, null);
    }

    [McpServerTool(Name = "loom_autofix_setup_check", Title = "Autofix Pre-flight Check",
        ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    public async partial Task<LoomAutofixSetupCheck> AutofixSetupCheckAsync(
        string issueId,
        string? policy = null,
        CancellationToken ct = default)
    {
        var issue = await collector.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        var parsedPolicy = ParseFixPolicy(policy);

        LoomAutofixCheck issueCheck = issue is null
            ? new LoomAutofixCheck("issue_exists", "fail", $"Issue '{issueId}' not found in qyl collector.")
            : new LoomAutofixCheck("issue_exists", "pass", $"Issue '{issueId}' resolved: {issue.ErrorType}.");

        LoomAutofixCheck policyCheck = new(
            "policy",
            "pass",
            $"Policy resolved to {parsedPolicy}.");

        LoomAutofixCheck repoCheck = new(
            "repo_connection",
            "unknown",
            "qyl collector does not currently expose project-integration state. Verify via the LLM-driven `qyl.loom.autofix_setup_check` prompt.");

        LoomAutofixCheck writeCheck = new(
            "write_access",
            "unknown",
            "Integration-scope introspection not available. Verify via the GitHub App settings page.");

        LoomAutofixCheck mappingCheck = new(
            "code_mapping",
            "unknown",
            "Stack-trace-to-repo mapping is not yet exposed as a tool. Stage-4 solution generation will surface mapping failures at the hunk level.");

        LoomAutofixCheck quotaCheck = new(
            "quota",
            "unknown",
            "Per-org run quota introspection not available. If a policy run stalls with no events, check collector rate limits.");

        var passed = issue is not null;
        var decision = passed ? "proceed" : "cannot_proceed";

        return new LoomAutofixSetupCheck(
            issueId,
            parsedPolicy.ToString(),
            [issueCheck, policyCheck, repoCheck, writeCheck, mappingCheck, quotaCheck],
            decision,
            "qyl.loom.autofix_setup_check");
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

public sealed record LoomAutofixSetupCheck(
    string IssueId,
    string Policy,
    IReadOnlyList<LoomAutofixCheck> Checks,
    string Decision,
    string AgentPromptId);

public sealed record LoomAutofixCheck(string Name, string Status, string Detail);

public sealed record LoomAutofixUpdateResult(bool Success, string RunId, string? Error);
