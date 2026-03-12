using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Qyl.Agents.Agents;
using Qyl.Agents.Context;
using Qyl.Collector.Autofix;
using Qyl.Contracts.Autofix;

namespace Qyl.Collector.Copilot;

/// <summary>
///     AG-UI SSE endpoints for the conversational Loom investigation agent.
///     Manages session lifecycle, streaming, interrupts, and background handoffs.
/// </summary>
public static class LoomAguiEndpoints
{
    public static IEndpointRouteBuilder MapLoomAguiEndpoints(
        this IEndpointRouteBuilder endpoints,
        AIAgent agent)
    {
        Guard.NotNull(endpoints);
        Guard.NotNull(agent);

        var group = endpoints.MapGroup("/api/v1/loom");

        // ── Start or continue conversational Loom session ───────────────────
        group.MapPost("/{issueId}/chat", async (
            string issueId,
            LoomChatRequest? request,
            LoomSessionStore sessionStore,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            // Find existing active session or create new one
            IReadOnlyList<LoomSession> existing =
                await sessionStore.GetByIssueAsync(issueId, ct);
            LoomSession session = existing.FirstOrDefault(s => !s.Status.IsTerminal())
                ?? await sessionStore.CreateAsync(issueId, ct: ct);

            // Set up SSE
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";

            // Create agent session with StateBag keys for context providers
            var agentSession = await agent.CreateSessionAsync(ct);
            agentSession.StateBag.SetValue(ObservabilityContextProvider.IssueIdKey, issueId);
            agentSession.StateBag.SetValue(LoomTools.SessionIdKey, session.SessionId);

            // Replay prior messages if resuming
            IReadOnlyList<LoomMessage> history =
                await sessionStore.GetMessagesAsync(session.SessionId, ct);
            if (history.Count > 0)
                await ReplayHistoryAsync(httpContext.Response, history, ct);

            // Determine the prompt — user message or default investigation prompt
            string prompt = request?.Message ?? "Investigate the error described in the context.";

            // Persist the user message
            await sessionStore.AppendMessageAsync(session.SessionId,
                new LoomMessage(LoomMessageRole.User, prompt), ct);

            // Update session to active
            session.Stage = LoomStage.Exploring;
            session.Status = LoomStatus.Active;
            await sessionStore.UpdateAsync(session, ct);

            // Stream agent response
            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            session.CancellationTokenSource = sessionCts;

            try
            {
                var fullResponse = new System.Text.StringBuilder();

                await foreach (var update in agent.RunStreamingAsync(
                    prompt, agentSession, cancellationToken: sessionCts.Token))
                {
                    string? text = update.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        fullResponse.Append(text);
                        await WriteSseEventAsync(httpContext.Response, "TEXT_DELTA",
                            new { delta = text }, ct);
                    }
                }

                // Persist assistant response
                if (fullResponse.Length > 0)
                {
                    await sessionStore.AppendMessageAsync(session.SessionId,
                        new LoomMessage(LoomMessageRole.Assistant, fullResponse.ToString()), ct);
                }

                session.Status = LoomStatus.Completed;
                await WriteSseEventAsync(httpContext.Response, "RUN_FINISHED", new { }, ct);
            }
            catch (OperationCanceledException) when (sessionCts.IsCancellationRequested)
            {
                // Interrupted — session stays active for reconnection
                await WriteSseEventAsync(httpContext.Response, "RUN_INTERRUPTED", new { }, ct);
            }
            catch (HttpRequestException ex)
            {
                // LLM API call failed (network, auth, rate limit)
                session.Status = LoomStatus.Failed;
                session.Error = ex.Message;
                await WriteSseEventAsync(httpContext.Response, "RUN_ERROR",
                    new { error = ex.Message }, ct);
            }
            catch (InvalidOperationException ex)
            {
                // Agent misconfiguration or invalid state
                session.Status = LoomStatus.Failed;
                session.Error = ex.Message;
                await WriteSseEventAsync(httpContext.Response, "RUN_ERROR",
                    new { error = ex.Message }, ct);
            }

            await sessionStore.UpdateAsync(session, ct);
        });

        // ── Interrupt running agent ─────────────────────────────────────────
        group.MapPost("/{sessionId}/interrupt", async (
            string sessionId,
            InterruptRequest request,
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            LoomSession? session = await sessionStore.GetAsync(sessionId, ct);
            if (session is null) return Results.NotFound();
            if (session.Status.IsTerminal())
                return Results.Conflict("Session is terminal");

            session.CancellationTokenSource?.Cancel();

            await sessionStore.AppendMessageAsync(sessionId,
                new LoomMessage(LoomMessageRole.User, request.Message), ct);
            session.Stage = LoomStage.Exploring;
            session.Status = LoomStatus.Active;
            session.PauseReason = null;
            await sessionStore.UpdateAsync(session, ct);

            return Results.Ok();
        });

        // ── List background sessions ready for handoff ──────────────────────
        group.MapGet("/pending-handoffs", async (
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            IReadOnlyList<LoomSessionSummary> handoffs =
                await sessionStore.GetPendingHandoffsAsync(ct);
            return Results.Ok(handoffs);
        });

        // ── Convert background session to interactive ───────────────────────
        group.MapPost("/{sessionId}/attach", async (
            string sessionId,
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            LoomSession? session = await sessionStore.GetAsync(sessionId, ct);
            if (session is null) return Results.NotFound();
            if (session.Mode != LoomSessionMode.Background)
                return Results.Conflict("Session is not a background session");
            if (session.Status != LoomStatus.Completed)
                return Results.Conflict("Session is not completed");

            session.Mode = LoomSessionMode.Interactive;
            session.Status = LoomStatus.Idle;
            await sessionStore.UpdateAsync(session, ct);

            return Results.Ok(session);
        });

        // ── Get session state ───────────────────────────────────────────────
        group.MapGet("/{sessionId}", async (
            string sessionId,
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            LoomSession? session = await sessionStore.GetAsync(sessionId, ct);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });

        // ── Get full message history ────────────────────────────────────────
        group.MapGet("/{sessionId}/messages", async (
            string sessionId,
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            IReadOnlyList<LoomMessage> messages =
                await sessionStore.GetMessagesAsync(sessionId, ct);
            return Results.Ok(messages);
        });

        return endpoints;
    }

    private static async Task ReplayHistoryAsync(
        HttpResponse response,
        IReadOnlyList<LoomMessage> history,
        CancellationToken ct)
    {
        foreach (LoomMessage msg in history)
        {
            string eventType = msg.Role switch
            {
                LoomMessageRole.Assistant => "TEXT_DELTA",
                LoomMessageRole.Tool => "TOOL_RESULT",
                _ => "TEXT_DELTA"
            };

            await WriteSseEventAsync(response, eventType,
                new { delta = msg.Content, replay = true }, ct);
        }
    }

    private static async Task WriteSseEventAsync(
        HttpResponse response, string eventType, object data, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(data, LoomSseJsonContext.Default.Options);
        await response.WriteAsync($"event: {eventType}\ndata: {json}\n\n", ct)
            .ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
}

public sealed record LoomChatRequest(
    [property: JsonPropertyName("message")] string? Message);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoomChatRequest))]
internal partial class LoomSseJsonContext : JsonSerializerContext;
