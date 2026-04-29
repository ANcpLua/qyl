using System.Net.ServerSentEvents;
using Microsoft.AspNetCore.Hosting;
using Qyl.Contracts.Copilot;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.Mcp;
using Qyl.Loom;
using Qyl.Loom.Agents;
using Qyl.Loom.Autofix;
using Qyl.Loom.Autofix.Workflow;
using Qyl.Loom.Clients;
using Qyl.Loom.CodeReview;
using Qyl.Loom.Exploration;
using Qyl.Loom.Workflows;
using Qyl.Loom.Workflows.Prompts;
using AutofixOrchestrator = Qyl.Loom.Autofix.AutofixOrchestrator;

var builder = WebApplication.CreateBuilder(args);

// Railway / PaaS convention: respect $PORT when provided, fall back to the qyl default.
// Matches qyl.collector's CollectorPortOptions and qyl.mcp's QylMcpServiceCollectionExtensions.
// WebApplicationBuilder doesn't expose UseUrls directly; UseSetting("urls", ...) is the
// minimal-API equivalent and runs before Kestrel bind.
if (int.TryParse(builder.Configuration["PORT"], out var port) && port > 0)
{
    builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://0.0.0.0:{port}");
}

builder.AddQylServiceDefaults(options =>
{
    options.AdditionalActivitySources.Add("Qyl.Loom");
});

builder.Services.AddHttpClient<CollectorClient>(client =>
{
    var baseUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddStandardResilienceHandler();

builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri("https://api.github.com");
    client.DefaultRequestHeaders.Add("User-Agent", "qyl-loom");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
}).AddStandardResilienceHandler();

// qyl three-builder pattern — chat-client → agents → workflow. Every AIAgent
// and every Workflow flows through these singletons so the
// .AsBuilder().UseQylAgentTelemetry().Build() wrap is centralized and the
// workflow topology is constructed once.
builder.Services.AddSingleton<IQylLoomChatClientBuilder, QylLoomChatClientBuilder>();
builder.Services.AddSingleton<IQylLoomAgentsBuilder, QylLoomAgentsBuilder>();
builder.Services.AddSingleton<IQylLoomWorkflowBuilder, QylLoomWorkflowBuilder>();

// Autofix workflow infrastructure — per-run state, run registry, step ledger,
// lifecycle bus, workflow factory. All singleton; per-run state keyed by runId.
builder.Services.AddSingleton<AutofixReportAssemblyState>();
builder.Services.AddSingleton<AutofixRunRegistry>();
builder.Services.AddSingleton<AutofixContextLoader>();
builder.Services.AddSingleton<AutofixContextTools>();
builder.Services.AddSingleton<IAutofixStepLedger, CollectorAutofixStepLedger>();
builder.Services.AddSingleton<IAutofixLifecycleBus, InMemoryAutofixLifecycleBus>();
builder.Services.AddSingleton<AutofixRunConfigStore>();
builder.Services.AddSingleton<AutofixWorkflowFactory>();

// Checkpoint persistence — file-backed JsonCheckpointStore so workflow runs
// survive process restart and dashboard refresh. Root path configurable via
// QYL_AUTOFIX_CHECKPOINT_ROOT env var; otherwise falls under the OS temp dir.
builder.Services.AddSingleton<FileSystemAutofixCheckpointStore>();
builder.Services.AddSingleton(sp =>
    CheckpointManager.CreateJson(sp.GetRequiredService<FileSystemAutofixCheckpointStore>()));

// Background pipelines — TriagePipelineService, AutofixAgentService, and
// RegressionDetectionService auto-register via [QylHostedService] through the
// generator's QylGeneratedRegistry.RegisterQylHostedServices hook.
builder.Services.AddSingleton<AutofixOrchestrator>();
builder.Services.AddSingleton<LoomAutofixRunner>();

// Exploration (interactive investigation)
builder.Services.AddSingleton<ExplorationContextBuilder>();
builder.Services.AddSingleton<ExplorationSessionStore>();
builder.Services.AddSingleton<ExplorationDiagnostician>();
builder.Services.AddSingleton<ExplorationStrategist>();
builder.Services.AddSingleton<ExplorationInsightService>();
builder.Services.AddSingleton<ExplorationOrchestrator>();

// Code review
builder.Services.AddSingleton<CodeReviewService>();

// MCP server — tools dispatched by official MCP SDK; telemetry via the qyl
// instrumentation facade so loom's MCP surface produces the same JSON-RPC
// envelope spans, gen_ai.execute_tool spans, and silent-error capture as
// qyl.mcp does. Both servers emit on the canonical "qyl.mcp" ActivitySource
// (registered by AddQylServiceDefaults), so the OTel pipeline picks them up
// without any extra source-list entries.
builder.Services.AddSingleton<LoomGodAnalyzerServer>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .UseQylMcpInstrumentation(ActivitySources.McpSource, options => options.Transport = "http")
    .WithTools<LoomGodAnalyzerServer>()
    .WithTools<LoomWorkflowTools>()
    .WithTools<Qyl.Loom.Autofix.Workflow.AutofixContextToolsWrapper>()
    .WithPrompts<CodeReviewPrompt>()
    .WithPrompts<LoomHandoffPrompts>()
    .WithPrompts<LoomAutofixPrompts>()
    .WithPrompts<OnboardingPrompts>()
    .WithPrompts<AiMonitoringPrompts>()
    .WithPrompts<FixIssuePrompts>()
    .WithPrompts<ReviewBotPrompts>();

var app = builder.Build();

app.MapQylEndpoints();

// ── Exploration HTTP endpoints ──────────────────────────────────────────────

app.MapGet("/api/v1/loom/{issueId}/insight", async (
    string issueId,
    ExplorationInsightService insightService,
    CancellationToken ct) =>
{
    var insight = await insightService.GenerateInsightAsync(issueId, ct).ConfigureAwait(false);
    return insight is not null
        ? Results.Ok(insight)
        : Results.NotFound(new { error = $"Issue '{issueId}' not found." });
});

app.MapPost("/api/v1/loom/{issueId}/explore", (
        string issueId,
        ExplorationExploreRequest? request,
        ExplorationOrchestrator orchestrator,
        CancellationToken ct) =>
    TypedResults.ServerSentEvents(
        StreamExploreAsync(orchestrator, issueId, request?.UserContext, ct),
        null));

app.MapPost("/api/v1/loom/{issueId}/code-it-up", async (
    string issueId,
    ExplorationCodeItUpRequest request,
    AutofixOrchestrator autofixOrchestrator,
    AutofixRunConfigStore configStore,
    CollectorClient collector,
    CancellationToken ct) =>
{
    var issue = await collector.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
    if (issue is null)
        return Results.NotFound(new { error = $"Issue '{issueId}' not found." });

    var run = await autofixOrchestrator.CreateFixRunAsync(issueId, FixPolicy.AutoApply, ct: ct)
        .ConfigureAwait(false);

    // HITL only when the caller explicitly opts in via request_review. Without this
    // flag the dashboard-initiated run completes autonomously — there is no production
    // approval endpoint yet, so forcing Interactive on every code-it-up adds two
    // 5-minute timeout waits to every run for no benefit.
    if (request.RequestReview)
    {
        configStore.Set(run.RunId, AutofixWorkflowDefaults.Interactive);
    }

    if (string.IsNullOrWhiteSpace(request.Repo))
        return Results.Ok(new ExplorationCodeItUpResponse(true, run.RunId, null, null));

    var pr = await collector
        .CreatePullRequestAsync(issueId, run.RunId, request.Repo, request.BaseBranch, ct)
        .ConfigureAwait(false);

    return Results.Ok(new ExplorationCodeItUpResponse(true, run.RunId, pr.PrUrl, pr.Error));
});

// ── Autofix workflow lifecycle SSE ──────────────────────────────────────────

app.MapGet("/api/v1/loom/autofix/{runId}/lifecycle", (
        string runId,
        IAutofixLifecycleBus bus,
        CancellationToken ct) =>
    TypedResults.ServerSentEvents(
        StreamLifecycleAsync(bus, runId, ct),
        null));

// ── Code review endpoints ───────────────────────────────────────────────────

app.MapPost("/api/v1/code-review/{owner}/{repo}/pulls/{prNumber:int}", async (
    string owner, string repo, int prNumber,
    CodeReviewService reviewService, CancellationToken ct) =>
{
    var result = await reviewService
        .ReviewPullRequestAsync($"{owner}/{repo}", prNumber, ct)
        .ConfigureAwait(false);
    return Results.Ok(result);
});

app.MapGet("/api/v1/code-review/{owner}/{repo}/pulls/{prNumber:int}", (
    string owner, string repo, int prNumber,
    CodeReviewService reviewService) =>
{
    var cached = reviewService.GetCachedResult($"{owner}/{repo}", prNumber);
    return cached is not null ? Results.Ok(cached) : Results.NotFound();
});

app.MapPost("/api/v1/code-review/{owner}/{repo}/pulls/{prNumber:int}/post", async (
    string owner, string repo, int prNumber,
    CodeReviewService reviewService, CancellationToken ct) =>
{
    var cached = reviewService.GetCachedResult($"{owner}/{repo}", prNumber);
    if (cached is null || cached.Comments.Count is 0)
        return Results.BadRequest(new { error = "No review comments available. Run a review first." });

    var posted = await reviewService
        .PostReviewCommentsAsync($"{owner}/{repo}", prNumber, cached.Comments, ct)
        .ConfigureAwait(false);

    return posted
        ? Results.Ok(new { posted = cached.Comments.Count })
        : Results.Problem("Failed to post some or all review comments to GitHub.");
});

// ── MCP server ──────────────────────────────────────────────────────────────

app.MapMcp("/mcp/loom");

app.Run();
return;

// ── SSE helper ──────────────────────────────────────────────────────────────

static async IAsyncEnumerable<SseItem<AutofixLifecycleEnvelope>> StreamLifecycleAsync(
    IAutofixLifecycleBus bus,
    string runId,
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var envelope in bus.SubscribeAsync(runId, ct).ConfigureAwait(false))
    {
        yield return new SseItem<AutofixLifecycleEnvelope>(envelope, envelope.Kind);
    }
}

static async IAsyncEnumerable<SseItem<StreamUpdate>> StreamExploreAsync(
    ExplorationOrchestrator orchestrator,
    string issueId,
    string? userContext,
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var update in orchestrator
                       .ExploreAsync(issueId, userContext, ct)
                       .ConfigureAwait(false))
    {
        var eventName = update.Kind switch
        {
            StreamUpdateKind.Content => "CONTENT",
            StreamUpdateKind.ToolCall => "TOOL_CALL",
            StreamUpdateKind.ToolResult => "TOOL_RESULT",
            StreamUpdateKind.Progress => "PROGRESS",
            StreamUpdateKind.Completed => "COMPLETED",
            StreamUpdateKind.Error => "ERROR",
            StreamUpdateKind.Metadata => "METADATA",
            _ => "UNKNOWN"
        };
        yield return new SseItem<StreamUpdate>(update, eventName);
    }
}
