using Qyl.Contracts.Copilot;

namespace Qyl.Collector.Autofix;

/// <summary>
///     REST + SSE endpoints for the interactive Loom debugging workflow.
///     Drives the sidebar panel: insight → explore → code-it-up.
/// </summary>
public static class LoomEndpoints
{
    public static void MapLoomEndpoints(this WebApplication app)
    {
        // ── Stage 1: Pre-investigation insight (fast, no streaming) ──────────
        app.MapGet("/api/v1/loom/{issueId}/insight", static async Task<IResult> (
            string issueId,
            LoomInsightService insightService,
            CancellationToken ct) =>
        {
            var insight = await insightService.GenerateInsightAsync(issueId, ct)
                .ConfigureAwait(false);

            return insight is not null
                ? TypedResults.Ok(insight)
                : TypedResults.NotFound(new { error = $"Issue '{issueId}' not found." });
        });

        // ── Stages 2-5: Interactive exploration (SSE streaming) ──────────────
        app.MapPost("/api/v1/loom/{issueId}/explore", static (
                string issueId,
                LoomExploreRequest? request,
                LoomOrchestrator orchestrator,
                CancellationToken ct) =>
            TypedResults.ServerSentEvents(
                StreamExploreAsync(orchestrator, issueId, request?.UserContext, ct),
                null));

        // ── Stage 5: "Code It Up" trigger ────────────────────────────────────
        // Collector creates the fix run (data plane). Loom picks it up for
        // orchestration (RCA, diff, confidence scoring) via its background service.
        app.MapPost("/api/v1/loom/{issueId}/code-it-up", static async Task<IResult> (
            string issueId,
            LoomCodeItUpRequest request,
            PrCreationService prService,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var issue = await store.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
            if (issue is null)
                return TypedResults.NotFound(new { error = $"Issue '{issueId}' not found." });

            var run = new FixRunRecord
            {
                RunId = Guid.NewGuid().ToString("N"),
                IssueId = issueId,
                Status = "pending",
                Policy = FixPolicy.AutoApply.ToString()
            };
            await store.InsertFixRunAsync(run, ct).ConfigureAwait(false);

            string? prUrl = null;
            if (!string.IsNullOrWhiteSpace(request.Repo))
            {
                var prResult = await prService.CreatePrAsync(
                    run.RunId, request.Repo, request.BaseBranch, ct).ConfigureAwait(false);

                if (prResult.Success)
                    prUrl = prResult.PrUrl;
            }

            LoomCodeItUpResponse response = new(
                true,
                run.RunId,
                prUrl,
                null);

            return TypedResults.Ok(response);
        });
    }

    private static async IAsyncEnumerable<SseItem<StreamUpdate>> StreamExploreAsync(
        LoomOrchestrator orchestrator,
        string issueId,
        string? userContext,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var update in orchestrator
                           .ExploreAsync(issueId, userContext, ct)
                           .ConfigureAwait(false))
        {
            yield return new SseItem<StreamUpdate>(update, MapEventName(update.Kind));
        }
    }

    private static string MapEventName(StreamUpdateKind kind) => kind switch
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
}

// ── Request/Response DTOs ────────────────────────────────────────────────────

public sealed record LoomCodeItUpRequest(
    [property: JsonPropertyName("repo")] string? Repo,
    [property: JsonPropertyName("base_branch")]
    string? BaseBranch);

public sealed record LoomCodeItUpResponse(
    [property: JsonPropertyName("success")]
    bool Success,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("pr_url")] string? PrUrl,
    [property: JsonPropertyName("error")] string? Error);

// ── JSON context for SSE serialization ───────────────────────────────────────

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StreamUpdate))]
[JsonSerializable(typeof(LoomCodeItUpResponse))]
internal partial class LoomStreamUpdateJsonContext : JsonSerializerContext;
