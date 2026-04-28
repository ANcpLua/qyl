# MAF primitives × qyl autofix patterns — design sketch

Idea map, not a spec. MAF gives the primitives (`Executor`, `WorkflowBuilder`,
`RequestPort`, `CheckpointManager`, `WorkflowEvent`, `IWorkflowContext`,
`InProcessExecution.RunStreamingAsync`, `StreamingRun`). qyl glues them with the
patterns already in the repo: factories, attribute-driven registration, strategy
handlers, executors, extensions, options. Each block below is a code sketch —
types and paths are suggestions, composition is the point.

---

## 1. HITL mid-stream feedback

```csharp
// packages/Qyl.Contracts/Loom/ExplorationFeedback.cs
public sealed record ExplorationPause(string SessionId, string PartialMonologue);
public sealed record UserFeedback(string SessionId, string Text, bool Continue);

// services/qyl.loom/Exploration/Feedback/IExplorationFeedbackChannel.cs
public interface IExplorationFeedbackChannel
{
    ValueTask<UserFeedback> AwaitAsync(string sessionId, CancellationToken ct);
    void Publish(string sessionId, UserFeedback feedback);
}

// services/qyl.loom/Exploration/Workflow/ExplorationWorkflowFactory.cs
new WorkflowBuilder(buildContext)
    .AddEdge(buildContext, diagnose)
    .AddExternalCall<ExplorationPause, UserFeedback>(diagnose, "exploration.feedback")
    .ForwardMessage<UserFeedback>("exploration.feedback", [resumeDiagnose])
    .AddEdge(resumeDiagnose, planSolution)
    .AddEdge(planSolution, finalize)
    .WithOutputFrom(finalize)
    .Build();

// Endpoint: POST /api/v1/loom/{sessionId}/feedback  { text, continue }
// Orchestrator watches for RequestInfoEvent, calls channel.AwaitAsync, then
// run.SendResponseAsync(req.CreateResponse(userFeedback)).
```

---

## 2. Code It Up approval gate

```csharp
// packages/Qyl.Contracts/Loom/AutofixTypes.cs
public enum FixPolicy { AutoApply, RequireReview, DryRun, ReviewBeforeDiff }
public sealed record ApprovalVerdict(
    string RunId, bool Approved, string? EditedPlanJson, string? Reason);

// services/qyl.loom/Autofix/Approval/IFixRunApprovalStore.cs
public interface IFixRunApprovalStore
{
    void Stash(string runId, StreamingRun run, RequestInfo req);
    (StreamingRun Run, RequestInfo Req)? Retrieve(string runId);
    void Release(string runId);
}

// services/qyl.loom/Autofix/Workflow/AutofixWorkflowFactory.cs
.AddEdge(solutionPlan, approvalBridge)
.AddExternalCall<AutofixRunState, ApprovalVerdict>(approvalBridge, "autofix.approval")
.ForwardMessage<ApprovalVerdict>("autofix.approval", [diffGen],   v => v.Approved)
.ForwardMessage<ApprovalVerdict>("autofix.approval", [earlyStop], v => !v.Approved)

// Endpoint
app.MapPost("/api/v1/loom/fix-runs/{runId}/approve", async (
    string runId, ApprovalBody body,
    IFixRunApprovalStore store, CancellationToken ct) =>
{
    if (store.Retrieve(runId) is not { } pending) return Results.NotFound();
    await pending.Run.SendResponseAsync(
        pending.Req.CreateResponse(new ApprovalVerdict(
            runId, body.Approved, body.EditedPlanJson, body.Reason)), ct);
    store.Release(runId);
    return Results.Accepted();
});
```

---

## 3. Supersession / cancel-on-new-commit

```csharp
// services/qyl.loom/Autofix/Cancellation/IFixRunCancellationRegistry.cs
public interface IFixRunCancellationRegistry
{
    CancellationToken Register(string runId, string branchKey);
    void CancelByBranchKey(string branchKey, string reason);
    void Release(string runId);
}

// services/qyl.loom/Autofix/AutofixAgentService.cs — wrap dispatch
var branchKey = $"{run.RepoFullName}#{run.BranchBase ?? "main"}";
using var linked = CancellationTokenSource.CreateLinkedTokenSource(
    ct, registry.Register(run.RunId, branchKey));
try
{
    await using var stream = await InProcessExecution.RunStreamingAsync(
        workflow, new StartAutofix(run.RunId),
        CheckpointManager.Default, sessionId: run.RunId,
        cancellationToken: linked.Token);
    // existing watch loop
}
catch (OperationCanceledException) when (linked.IsCancellationRequested && !ct.IsCancellationRequested)
{
    await orchestrator.UpdateFixRunStatusAsync(run.IssueId, run.RunId,
        status: "cancelled", description: "superseded", ct: ct);
}
finally { registry.Release(run.RunId); }

// services/qyl.collector/Autofix/SupersessionHandler.cs
internal sealed class SupersessionHandler(IFixRunCancellationRegistry registry) : IGitHubEventHandler
{
    public string EventType => "push";
    public ValueTask HandleAsync(JsonElement p, CancellationToken _)
    {
        if (p.GetProperty("sender").GetProperty("login").GetString() is "qyl-bot") return default;
        var repo = p.GetProperty("repository").GetProperty("full_name").GetString()!;
        var @ref = p.GetProperty("ref").GetString()!.Replace("refs/heads/", "");
        registry.CancelByBranchKey($"{repo}#{@ref}", "superseded by push");
        return default;
    }
}
```

---

## 4. Webhook → code review auto-trigger

```csharp
// services/qyl.collector/Autofix/IGitHubEventHandler.cs
public interface IGitHubEventHandler
{
    string EventType { get; }
    ValueTask HandleAsync(JsonElement payload, CancellationToken ct);
}

// services/qyl.loom/CodeReview/PullRequestReviewAdapter.cs
internal sealed class PullRequestReviewAdapter(CodeReviewService review) : IGitHubEventHandler
{
    public string EventType => "pull_request";
    public async ValueTask HandleAsync(JsonElement p, CancellationToken ct)
    {
        if (p.GetProperty("action").GetString() is not ("opened" or "ready_for_review" or "synchronize")) return;
        if (p.GetProperty("pull_request").GetProperty("draft").GetBoolean()) return;
        var repo = p.GetProperty("repository").GetProperty("full_name").GetString()!;
        var prNumber = p.GetProperty("pull_request").GetProperty("number").GetInt32();
        await review.ReviewPullRequestAsync(repo, prNumber, ct);
    }
}

// services/qyl.collector/Autofix/GitHubWebhookEndpoints.cs — fanout at end
foreach (var h in services.GetServices<IGitHubEventHandler>().Where(h => h.EventType == eventType))
    await h.HandleAsync(root, ct);

// Program.cs
services.AddSingleton<IGitHubEventHandler, PullRequestReviewAdapter>();
services.AddSingleton<IGitHubEventHandler, IssueCommentReviewAdapter>();
services.AddSingleton<IGitHubEventHandler, SupersessionHandler>();
```

---

## 5. `@qyl review` PR comment trigger

```csharp
// services/qyl.loom/CodeReview/IssueCommentReviewAdapter.cs
internal sealed class IssueCommentReviewAdapter(CodeReviewService review) : IGitHubEventHandler
{
    public string EventType => "issue_comment";
    public async ValueTask HandleAsync(JsonElement p, CancellationToken ct)
    {
        if (p.GetProperty("action").GetString() != "created") return;
        var body = p.GetProperty("comment").GetProperty("body").GetString() ?? "";
        if (!body.Contains("@qyl review", StringComparison.OrdinalIgnoreCase)) return;
        var repo = p.GetProperty("repository").GetProperty("full_name").GetString()!;
        var prNumber = p.GetProperty("issue").GetProperty("number").GetInt32();
        await review.ReviewPullRequestAsync(repo, prNumber, ct);
    }
}
```

---

## 6. GitHub status checks

```csharp
// services/qyl.loom/CodeReview/GitHubCheckRunExtensions.cs
public enum CheckConclusion { Success, Neutral, Failure, Cancelled, ActionRequired }

public static class GitHubCheckRunExtensions
{
    public static async ValueTask<string> StartCheckAsync(
        this HttpClient gh, string repo, string headSha, string name, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            name, head_sha = headSha, status = "in_progress",
            started_at = DateTimeOffset.UtcNow
        });
        using var r = await gh.PostAsync($"repos/{repo}/check-runs",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        r.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(
            await r.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return json.RootElement.GetProperty("id").GetInt64().ToString();
    }

    public static async ValueTask CompleteCheckAsync(
        this HttpClient gh, string repo, string checkId,
        CheckConclusion conclusion, string title, string summary, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            status = "completed",
            conclusion = conclusion.ToString().ToLowerInvariant(),
            completed_at = DateTimeOffset.UtcNow,
            output = new { title, summary }
        });
        using var _ = await gh.PatchAsync($"repos/{repo}/check-runs/{checkId}",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
    }
}

// CodeReviewService.ReviewPullRequestAsync — wrap existing body
var checkId = await gh.StartCheckAsync(repo, prData.HeadSha, "qyl Code Review", ct);
try
{
    var comments = await RunReview(/* existing */);
    await gh.CompleteCheckAsync(repo, checkId,
        comments.Count == 0 ? CheckConclusion.Success : CheckConclusion.Neutral,
        title: $"{comments.Count} issue(s)",
        summary: RenderSummary(comments), ct);
}
catch (Exception ex)
{
    await gh.CompleteCheckAsync(repo, checkId, CheckConclusion.Failure,
        title: "qyl review failed", summary: ex.Message, ct);
    throw;
}
```

---

## 7. PR creation from fix run

```csharp
// services/qyl.loom/Autofix/Git/IFixRunGitClient.cs
public interface IFixRunGitClient
{
    ValueTask<string> CreateBranchAsync(string repo, string baseBranch, string name, CancellationToken ct);
    ValueTask ApplyHunksAsync(string repo, string branch, string changesJson, CancellationToken ct);
    ValueTask<string /* prUrl */> OpenPullRequestAsync(
        string repo, string head, string @base, string title, string body, CancellationToken ct);
}

// services/qyl.loom/Autofix/Workflow/Executors/CreatePrExecutor.cs
internal sealed class CreatePrExecutor(
    CollectorClient collector, IFixRunGitClient git, IOptions<AutofixPolicyOptions> policy)
    : AutofixPipelineExecutor("autofix.create_pr", stepNumber: 8, stepName: "pr_creation", collector)
{
    protected override async ValueTask<(AutofixRunState, string)> DoWorkAsync(
        AutofixRunState s, CancellationToken ct)
    {
        if (!policy.Value.EnablePrCreation)              return (s, """{"skipped":"policy_disabled"}""");
        if (s.Policy is not FixPolicy.AutoApply)         return (s, """{"skipped":"policy"}""");
        if (s.Confidence?.Recommendation is not "apply") return (s, """{"skipped":"low_confidence"}""");

        // Idempotent on crash-resume: the MAF checkpoint re-enters this executor,
        // so skip if the step ledger already recorded a completed pr_creation
        // for this RunId (keyed by the MAF sessionId = run.RunId).
        var prior = await collector
            .GetCompletedStepAsync(s.RunId, stepName: "pr_creation", ct)
            .ConfigureAwait(false);
        if (prior?.OutputJson is { } existingJson) return (s, existingJson);

        var repo = policy.Value.ProjectToRepo(s.IssueId)
            ?? throw new InvalidOperationException("no repo mapping");
        var branch = $"qyl/autofix/{s.RunId[..8]}";

        await git.CreateBranchAsync(repo, baseBranch: "main", branch, ct);
        await git.ApplyHunksAsync(repo, branch, s.ChangesJson!, ct);
        var prUrl = await git.OpenPullRequestAsync(repo, branch, "main",
            title: ExtractTitle(s.ChangesJson!), body: RenderPrBody(s), ct);

        return (s, $$"""{"pr_url":"{{prUrl}}","branch":"{{branch}}"}""");
    }
}

// Factory — replace .WithOutputFrom(policyGate):
.AddSwitch(policyGate, sw => sw
    .AddCase<AutofixRunState>(
        s => PolicyGate.EvaluateNextStatus(s.Policy, s.Confidence?.Confidence ?? 0) == "applied",
        createPr)
    .WithDefault(finalize))
.AddEdge(createPr, finalize)
.WithOutputFrom(finalize)
```

---

## 8. Multi-repo codegen fan-out

```csharp
// services/qyl.loom/Autofix/Workflow/RepoFanoutSelector.cs
internal static class RepoFanoutSelector
{
    // Returns which target indices the message should broadcast to.
    public static IEnumerable<int> Select(AutofixRunState? s, int targetCount)
    {
        if (s?.SolutionPlan is null) return [];
        var touched = ExtractTouchedRepoIndices(s.SolutionPlan, targetCount);
        return touched.Count == 0 ? Enumerable.Range(0, targetCount) : touched;
    }
}

// services/qyl.loom/Autofix/Workflow/Executors/PerRepoDiffGenExecutor.cs
internal sealed class PerRepoDiffGenExecutor(CollectorClient c, IChatClient llm, string repo)
    : AutofixPipelineExecutor($"autofix.diff.{repo}", 4, $"diff_{repo}", c) { /* scoped to repo */ }

// services/qyl.loom/Autofix/Workflow/Executors/MergeChangesExecutor.cs
internal sealed class MergeChangesExecutor
    : Executor<AutofixRunState[], AutofixRunState>("autofix.merge_changes")
{
    public override ValueTask<AutofixRunState> HandleAsync(
        AutofixRunState[] perRepo, IWorkflowContext _, CancellationToken __ = default)
        => ValueTask.FromResult(perRepo[0] with { ChangesJson = AggregateChanges(perRepo) });
}

// Factory:
var repos = policy.Value.KnownRepos;
var perRepoDiffs = repos
    .Select(r => ActivatorUtilities.CreateInstance<PerRepoDiffGenExecutor>(services, r))
    .ToArray();
var merge = ActivatorUtilities.CreateInstance<MergeChangesExecutor>(services);

.AddFanOutEdge<AutofixRunState>(solutionPlan, perRepoDiffs, RepoFanoutSelector.Select)
.AddFanInBarrierEdge(perRepoDiffs, merge)
.AddEdge(merge, confidence)
```

---

## 9. Coding agent handoff button (MCP prompt is shipped)

```csharp
// services/qyl.loom/Autofix/Handoff/HandoffPayload.cs
public sealed record HandoffPayload(
    string IssueId, string ErrorType, string RcaReport, string SolutionPlan, string? AffectedFiles);

// services/qyl.loom/Autofix/Handoff/IHandoffPayloadBuilder.cs
public interface IHandoffPayloadBuilder
{
    ValueTask<HandoffPayload> BuildAsync(string runId, CancellationToken ct);
}

// services/qyl.loom/Autofix/Handoff/HandoffPayloadBuilder.cs
internal sealed class HandoffPayloadBuilder(CollectorClient collector) : IHandoffPayloadBuilder
{
    public async ValueTask<HandoffPayload> BuildAsync(string runId, CancellationToken ct)
    {
        var (run, steps) = await collector.GetFixRunWithStepsAsync(runId, ct);
        var issue = await collector.GetIssueByIdAsync(run.IssueId, ct);
        return new HandoffPayload(
            run.IssueId,
            issue!.ErrorType,
            RcaReport: steps.First(s => s.StepName == "root_cause_analysis").OutputJson ?? "",
            SolutionPlan: steps.First(s => s.StepName == "solution_planning").OutputJson ?? "",
            AffectedFiles: ExtractAffectedFiles(steps));
    }
}

// Program.cs
services.AddSingleton<IHandoffPayloadBuilder, HandoffPayloadBuilder>();

// Endpoint
app.MapGet("/api/v1/loom/fix-runs/{runId}/handoff-payload", async (
    string runId, IHandoffPayloadBuilder builder, CancellationToken ct) =>
    Results.Ok(await builder.BuildAsync(runId, ct)));

// Dashboard: <HandoffMenu /> fetches payload → local MCP client:
//   mcp.prompts.get("qyl.fix_handoff", { issueId, errorType, rcaReport, solutionPlan, affectedFiles })
```

---

## 10. Checkout locally

```csharp
// services/qyl.loom/Autofix/Patch/PatchRenderer.cs
public static class PatchRenderer
{
    // changesJson (schema_version=1) → unified diff, one file per entry.
    public static string ToUnifiedDiff(string changesJson) { /* --- +++ @@ ... */ }
}

// Endpoint
app.MapGet("/api/v1/loom/fix-runs/{runId}/patch", async (
    string runId, CollectorClient c, CancellationToken ct) =>
{
    var run = await c.GetFixRunByIdAsync(runId, ct);
    if (run?.ChangesJson is null) return Results.NotFound();
    return Results.Text(PatchRenderer.ToUnifiedDiff(run.ChangesJson), "text/x-diff");
});

// Dashboard shows:
//   git checkout -b qyl/autofix/{runId[..8]}
//   curl .../patch | git apply
```

---

## 11. Slack `/Fix with qyl`

```csharp
// services/qyl.collector/Slack/ISlackInteractionHandler.cs
public interface ISlackInteractionHandler
{
    string ActionId { get; }
    ValueTask HandleAsync(JsonElement payload, CancellationToken ct);
}

// services/qyl.loom/Slack/FixWithQylHandler.cs
internal sealed class FixWithQylHandler(AutofixOrchestrator orchestrator) : ISlackInteractionHandler
{
    public string ActionId => "fix_with_qyl";
    public async ValueTask HandleAsync(JsonElement p, CancellationToken ct)
    {
        var issueId = p.GetProperty("actions")[0].GetProperty("value").GetString()!;
        await orchestrator.CreateFixRunAsync(issueId, FixPolicy.AutoApply, ct: ct);
    }
}

// services/qyl.collector/Slack/SlackWebhookEndpoints.cs
[QylMapEndpoints]
public static class SlackWebhookEndpoints
{
    public static void MapSlackWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/slack/interactions", async (
            HttpRequest req, IServiceProvider services, CancellationToken ct) =>
        {
            var form = await req.ReadFormAsync(ct);
            var payload = JsonDocument.Parse(form["payload"]!).RootElement;
            var actionId = payload.GetProperty("actions")[0].GetProperty("action_id").GetString()!;
            foreach (var h in services.GetServices<ISlackInteractionHandler>()
                                      .Where(h => h.ActionId == actionId))
                await h.HandleAsync(payload, ct);
            return Results.Ok();
        });
    }
}
```

---

## 12. Advanced settings

```csharp
// services/qyl.loom/AutofixPolicyOptions.cs
public sealed class AutofixPolicyOptions
{
    public bool EnableCodeGeneration { get; init; } = true;
    public bool EnablePrCreation     { get; init; } = true;
    public IReadOnlyList<string> KnownRepos { get; init; } = [];
    public Dictionary<string, string> ProjectToRepoMap { get; init; } = [];
    public Dictionary<string, string> DefaultBranchOverrides { get; init; } = [];

    public string? ProjectToRepo(string issueId) =>
        ProjectToRepoMap.TryGetValue(ExtractProject(issueId), out var r) ? r : null;
}

// Program.cs
services.Configure<AutofixPolicyOptions>(configuration.GetSection("Autofix"));

// Readers: CreatePrExecutor, factory (KnownRepos → PerRepoDiffGen), PolicyGate.
// Optional admin endpoints:
//   GET /api/v1/loom/policy
//   PUT /api/v1/loom/policy    (persist to collector, IOptionsMonitor hot-reload)
```

---

## Pattern growth map

| Existing qyl pattern | Grows at |
|---|---|
| Factory (`AutofixWorkflowFactory`, `ExplorationWorkflowFactory`) | #1, #2, #7, #8 |
| `AutofixPipelineExecutor` subclass | #2 earlyStop, #7 CreatePr, #8 PerRepoDiffGen |
| Strategy handler (`IGitHubEventHandler`) | #3, #4, #5 |
| Mirror strategy (`ISlackInteractionHandler`) | #11 |
| Extension methods on `HttpClient` | #6 status checks |
| Static rendering helper | #10 PatchRenderer |
| DI-injected port (`IFixRunGitClient`, `IFixRunApprovalStore`, `IFixRunCancellationRegistry`, `IExplorationFeedbackChannel`) | #1, #2, #3, #7 |
| `IOptions<T>` | #12 |
| Custom `WorkflowEvent` subclass | #3 if propagated as event |
| `[McpServerPromptType]` (already shipped) | #9 |

Every gap is an instance of a pattern already proven in the codebase. No new architecture.
