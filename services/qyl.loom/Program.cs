using System.Net.ServerSentEvents;
using Microsoft.AspNetCore.Hosting;
using Qyl.Contracts.Copilot;
using Qyl.Instrumentation.Instrumentation;
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

// Apex three-builder pattern — chat-client → agents → workflow. Every AIAgent
// and every Workflow flows through these singletons so the
// .AsBuilder().UseQylAgentTelemetry().Build() wrap is centralized and the
// workflow topology is constructed once.
builder.Services.AddSingleton<IQylLoomChatClientBuilder, QylLoomChatClientBuilder>();
builder.Services.AddSingleton<IQylLoomAgentsBuilder, QylLoomAgentsBuilder>();
builder.Services.AddSingleton<IQylLoomWorkflowBuilder, QylLoomWorkflowBuilder>();

// Autofix workflow infrastructure — per-run state, run registry, step ledger,
// workflow factory. All singleton; per-run state keyed by runId.
builder.Services.AddSingleton<AutofixReportAssemblyState>();
builder.Services.AddSingleton<AutofixRunRegistry>();
builder.Services.AddSingleton<AutofixContextLoader>();
builder.Services.AddSingleton<IAutofixStepLedger, CollectorAutofixStepLedger>();
builder.Services.AddSingleton<AutofixWorkflowFactory>();

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

// MCP server — tools dispatched by official MCP SDK
builder.Services.AddSingleton<LoomGodAnalyzerServer>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<LoomGodAnalyzerServer>()
    .WithTools<LoomWorkflowTools>()
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
    CollectorClient collector,
    CancellationToken ct) =>
{
    var issue = await collector.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
    if (issue is null)
        return Results.NotFound(new { error = $"Issue '{issueId}' not found." });

    var run = await autofixOrchestrator.CreateFixRunAsync(issueId, FixPolicy.AutoApply, ct: ct)
        .ConfigureAwait(false);

    if (string.IsNullOrWhiteSpace(request.Repo))
        return Results.Ok(new ExplorationCodeItUpResponse(true, run.RunId, null, null));

    var pr = await collector
        .CreatePullRequestAsync(issueId, run.RunId, request.Repo, request.BaseBranch, ct)
        .ConfigureAwait(false);

    return Results.Ok(new ExplorationCodeItUpResponse(true, run.RunId, pr.PrUrl, pr.Error));
});

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
