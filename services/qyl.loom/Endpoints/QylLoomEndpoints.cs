
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Qyl.Contracts.Copilot;
using Qyl.Loom.Autofix;
using Qyl.Loom.Autofix.Workflow;
using Qyl.Loom.CodeReview;
using Qyl.Loom.Exploration;
using AutofixOrchestrator = Qyl.Loom.Autofix.AutofixOrchestrator;

namespace Qyl.Loom.Endpoints;

public static class QylLoomEndpoints
{
    public static WebApplication MapQylLoomEndpoints(this WebApplication app)
    {
        Guard.NotNull(app);

        MapExplorationEndpoints(app);
        MapAutofixLifecycleEndpoints(app);
        MapCodeReviewEndpoints(app);

        app.MapMcp("/mcp/loom");

        return app;
    }

    private static void MapExplorationEndpoints(WebApplication app)
    {
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
    }

    private static void MapAutofixLifecycleEndpoints(WebApplication app)
    {
        app.MapGet("/api/v1/loom/autofix/{runId}/lifecycle", (
                string runId,
                IAutofixLifecycleBus bus,
                CancellationToken ct) =>
            TypedResults.ServerSentEvents(
                StreamLifecycleAsync(bus, runId, ct),
                null));
    }

    private static void MapCodeReviewEndpoints(WebApplication app)
    {
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
    }


    private static async IAsyncEnumerable<SseItem<AutofixLifecycleEnvelope>> StreamLifecycleAsync(
        IAutofixLifecycleBus bus,
        string runId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var envelope in bus.SubscribeAsync(runId, ct).ConfigureAwait(false))
        {
            yield return new SseItem<AutofixLifecycleEnvelope>(envelope, envelope.Kind);
        }
    }

    private static async IAsyncEnumerable<SseItem<StreamUpdate>> StreamExploreAsync(
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
}
