using Qyl.Contracts.Copilot;

namespace Qyl.Loom;

/// <summary>
///     REST + SSE endpoints for the interactive Loom debugging workflow.
///     Drives the sidebar panel: insight → explore → code-it-up.
/// </summary>
public static class LoomEndpoints
{
    public static void MapLoomEndpoints(this WebApplication app)
    {
        // ── Stage 1: Pre-investigation insight (fast, no streaming) ──────────
        app.MapGet("/api/v1/loom/{issueId}/insight", static async (
            string issueId,
            LoomInsightService insightService,
            CancellationToken ct) =>
        {
            var insight = await insightService.GenerateInsightAsync(issueId, ct)
                .ConfigureAwait(false);

            return insight is not null
                ? Results.Ok(insight)
                : Results.NotFound(new { error = $"Issue '{issueId}' not found." });
        });

        // ── Stages 2-5: Interactive exploration (SSE streaming) ──────────────
        app.MapPost("/api/v1/loom/{issueId}/explore", static async (
            string issueId,
            LoomExploreRequest? request,
            LoomExplorerService explorerService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";

            await foreach (var update in explorerService
                               .ExploreAsync(issueId, request?.UserContext, ct)
                               .ConfigureAwait(false))
            {
                var eventName = MapEventName(update.Kind);
                var json = JsonSerializer.Serialize(update, StreamUpdateJsonContext.Default.StreamUpdate);
                await httpContext.Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct)
                    .ConfigureAwait(false);
                await httpContext.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        });

        // ── Stage 5: "Code It Up" trigger ────────────────────────────────────
        app.MapPost("/api/v1/loom/{issueId}/code-it-up", static async (
            string issueId,
            LoomCodeItUpRequest request,
            AutofixOrchestrator orchestrator,
            PrCreationService prService,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var issue = await store.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
            if (issue is null)
                return Results.NotFound(new { error = $"Issue '{issueId}' not found." });

            // Create a fix run with auto-apply policy (user clicked "Code It Up")
            var run = await orchestrator.CreateFixRunAsync(
                issueId, issue, FixPolicy.AutoApply, ct).ConfigureAwait(false);

            // If a repo is specified, attempt immediate PR creation after pipeline completes
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

            return Results.Ok(response);
        });
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

// JSON context for SSE serialization of StreamUpdate
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StreamUpdate))]
public partial class StreamUpdateJsonContext : JsonSerializerContext;
