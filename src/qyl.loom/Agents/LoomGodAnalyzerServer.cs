using Qyl.Agents;
using Qyl.Loom.CodeReview;
using Qyl.Loom.Exploration;

namespace Qyl.Loom.Agents;

[McpServer("loom-god-analyzer",
    Description = "Compile-time Loom analyzer surface backed by qyl collector services.",
    Version = "0.1.0")]
public sealed partial class LoomGodAnalyzerServer(
    CollectorClient collector,
    AutofixOrchestrator autofixOrchestrator,
    ExplorationInsightService insightService,
    CodeReviewService codeReviewService) : IMcpServer
{
    private const string ServerName = "loom-god-analyzer";
    private const string ServerDescription = "Seer-grade Loom analyzer surface backed by qyl collector services.";
    private const string ServerVersion = "0.1.0";

    private static readonly byte[] InsightSchema = Encoding.UTF8.GetBytes(
        """
        {"type":"object","properties":{"issueId":{"type":"string"}},"required":["issueId"]}
        """);

    private static readonly byte[] FixRunSchema = Encoding.UTF8.GetBytes(
        """
        {"type":"object","properties":{"issueId":{"type":"string"},"policy":{"type":"string"}},"required":["issueId"]}
        """);

    private static readonly byte[] ReviewSchema = Encoding.UTF8.GetBytes(
        """
        {"type":"object","properties":{"repoFullName":{"type":"string"},"prNumber":{"type":"integer"}},"required":["repoFullName","prNumber"]}
        """);

    public static string SkillMd =>
        """
        # Loom God Analyzer

        Seer-grade MCP surface for qyl Loom.

        Tools:
        - `loom_get_issue_insight`
        - `loom_start_fix_run`
        - `loom_review_pull_request`

        Prompt:
        - `loom_god_analyzer`
        """;

    public static string LlmsTxt =>
        """
        # loom-god-analyzer
        Seer-grade Loom MCP surface for issue insight, fix-run launch, pull-request review, and deep analysis prompts.
        """;

    public static McpServerInfo GetServerInfo() => new()
    {
        Name = ServerName,
        Description = ServerDescription,
        Version = ServerVersion
    };

    public static IReadOnlyList<McpToolInfo> GetToolInfos() =>
    [
        new()
        {
            Name = "loom_get_issue_insight",
            Description = "Generate pre-investigation Loom insight for an issue id.",
            InputSchema = InsightSchema,
            ReadOnlyHint = true,
            IdempotentHint = true,
            DestructiveHint = false,
            OpenWorldHint = false
        },
        new()
        {
            Name = "loom_start_fix_run",
            Description = "Create an autofix run for an issue using the selected policy.",
            InputSchema = FixRunSchema,
            ReadOnlyHint = false,
            IdempotentHint = false,
            DestructiveHint = false,
            OpenWorldHint = false
        },
        new()
        {
            Name = "loom_review_pull_request",
            Description = "Run Loom code review against a GitHub owner/repo pull request.",
            InputSchema = ReviewSchema,
            ReadOnlyHint = false,
            IdempotentHint = true,
            DestructiveHint = false,
            OpenWorldHint = false
        }
    ];

    public static IReadOnlyList<McpResourceInfo> GetResourceInfos() => [];

    public static IReadOnlyList<McpPromptInfo> GetPromptInfos() =>
    [
        new()
        {
            Name = "loom_god_analyzer",
            Description = "Reusable system prompt for the Loom god-analyzer workflow.",
            Arguments =
            [
                new McpPromptArgument { Name = "issueId", Description = "Issue identifier to analyze.", Required = true },
                new McpPromptArgument
                {
                    Name = "operatorGoal",
                    Description = "Optional operator goal or analysis angle.",
                    Required = false
                }
            ]
        }
    ];

    public async Task<string> DispatchToolCallAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "loom_get_issue_insight" => await DispatchInsightAsync(arguments, cancellationToken).ConfigureAwait(false),
            "loom_start_fix_run" => await DispatchFixRunAsync(arguments, cancellationToken).ConfigureAwait(false),
            "loom_review_pull_request" => await DispatchReviewAsync(arguments, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown tool '{toolName}'.")
        };
    }

    public Task<ResourceReadResult> DispatchResourceReadAsync(string uri, CancellationToken cancellationToken) =>
        Task.FromException<ResourceReadResult>(
            new InvalidOperationException($"No resources are exposed by {ServerName}."));

    public async Task<PromptResult> DispatchPromptAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(name, "loom_god_analyzer", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unknown prompt '{name}'.");

        var promptArgs = JsonSerializer.Deserialize(
                             arguments,
                             LoomGodAnalyzerJsonContext.Default.LoomGodAnalyzerPromptArgs)
                         ?? throw new InvalidOperationException("Prompt arguments are required.");

        return await Task.FromResult(BuildGodAnalyzerPrompt(promptArgs.IssueId, promptArgs.OperatorGoal))
            .ConfigureAwait(false);
    }

    [Tool("loom_get_issue_insight",
        Description = "Generate pre-investigation Loom insight for an issue id.")]
    public async Task<ExplorationInsight?> GetIssueInsightAsync(string issueId, CancellationToken ct = default) =>
        await insightService.GenerateInsightAsync(issueId, ct).ConfigureAwait(false);

    [Tool("loom_start_fix_run",
        Description = "Create an autofix run for an issue using the selected policy.")]
    public async Task<FixRunRecord?> StartFixRunAsync(
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

    [Tool("loom_review_pull_request",
        Description = "Run Loom code review against a GitHub owner/repo pull request.")]
    public async Task<CodeReviewResult> ReviewPullRequestAsync(
        string repoFullName,
        int prNumber,
        CancellationToken ct = default) =>
        await codeReviewService.ReviewPullRequestAsync(repoFullName, prNumber, ct).ConfigureAwait(false);

    [Prompt("loom_god_analyzer",
        Description = "Reusable system prompt for the Loom god-analyzer workflow.")]
    public static PromptResult BuildGodAnalyzerPrompt(string issueId, string? operatorGoal = null)
    {
        PromptMessage[] messages =
        [
            new("system",
                """
                You are Loom, qyl's Seer-grade god analyzer.
                Work in three passes:
                1. Reconstruct what happened from issue and trace context.
                2. State the most probable root cause with explicit evidence.
                3. Propose the smallest defensible fix and the validation plan.
                Never invent repository facts. Mark assumptions. Prefer concrete file, route, and telemetry evidence.
                """),
            new("user",
                $"""
                 Analyze issue `{issueId}`.
                 Operator goal: {operatorGoal ?? "produce the best available root-cause analysis and next fix action."}
                 Start with the evidence you have, name what is missing, and avoid generic advice.
                 """)
        ];

        return new PromptResult(messages)
        {
            Description = "Seer-style Loom prompt for deep issue analysis and fix planning."
        };
    }

    private async Task<string> DispatchInsightAsync(JsonElement arguments, CancellationToken ct)
    {
        var toolArgs = JsonSerializer.Deserialize(arguments, LoomGodAnalyzerJsonContext.Default.LoomGetIssueInsightArgs)
                       ?? throw new InvalidOperationException("Tool arguments are required.");
        var insight = await GetIssueInsightAsync(toolArgs.IssueId, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(insight, LoomGodAnalyzerJsonContext.Default.ExplorationInsight);
    }

    private async Task<string> DispatchFixRunAsync(JsonElement arguments, CancellationToken ct)
    {
        var toolArgs = JsonSerializer.Deserialize(arguments, LoomGodAnalyzerJsonContext.Default.LoomStartFixRunArgs)
                       ?? throw new InvalidOperationException("Tool arguments are required.");
        var run = await StartFixRunAsync(toolArgs.IssueId, toolArgs.Policy, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(run, LoomGodAnalyzerJsonContext.Default.FixRunRecord);
    }

    private async Task<string> DispatchReviewAsync(JsonElement arguments, CancellationToken ct)
    {
        var toolArgs = JsonSerializer.Deserialize(arguments, LoomGodAnalyzerJsonContext.Default.LoomReviewPullRequestArgs)
                       ?? throw new InvalidOperationException("Tool arguments are required.");
        var review = await ReviewPullRequestAsync(toolArgs.RepoFullName, toolArgs.PrNumber, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(review, LoomGodAnalyzerJsonContext.Default.CodeReviewResult);
    }

    private static FixPolicy ParseFixPolicy(string? policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
            return FixPolicy.RequireReview;

        return policy.Trim().Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant() switch
        {
            "AUTOAPPLY" => FixPolicy.AutoApply,
            "DRYRUN" => FixPolicy.DryRun,
            "REQUIREREVIEW" => FixPolicy.RequireReview,
            _ when Enum.TryParse<FixPolicy>(policy, true, out var parsed) => parsed,
            _ => FixPolicy.RequireReview
        };
    }
}

internal sealed record LoomGetIssueInsightArgs(string IssueId);

internal sealed record LoomStartFixRunArgs(string IssueId, string? Policy);

internal sealed record LoomReviewPullRequestArgs(string RepoFullName, int PrNumber);

internal sealed record LoomGodAnalyzerPromptArgs(string IssueId, string? OperatorGoal);

[JsonSerializable(typeof(LoomGetIssueInsightArgs))]
[JsonSerializable(typeof(LoomStartFixRunArgs))]
[JsonSerializable(typeof(LoomReviewPullRequestArgs))]
[JsonSerializable(typeof(LoomGodAnalyzerPromptArgs))]
[JsonSerializable(typeof(ExplorationInsight))]
[JsonSerializable(typeof(FixRunRecord))]
[JsonSerializable(typeof(CodeReviewResult))]
internal partial class LoomGodAnalyzerJsonContext : JsonSerializerContext;
