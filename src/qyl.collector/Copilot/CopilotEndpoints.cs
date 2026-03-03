using System.Diagnostics;
using Microsoft.Extensions.AI;
using qyl.copilot;
using qyl.copilot.Auth;
using qyl.copilot.Providers;
using qyl.copilot.Routing;
using qyl.protocol.Copilot;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace qyl.collector.Copilot;

/// <summary>
///     REST endpoints for GitHub Copilot integration.
///     Streams chat/workflow responses as Server-Sent Events.
/// </summary>
internal static class CopilotEndpoints
{
    public static WebApplication MapCopilotEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/copilot");

        group.MapGet("/status", GetStatusAsync);
        group.MapGet("/llm/status", GetLlmStatusAsync);
        group.MapPost("/chat", ChatAsync);
        group.MapGet("/workflows", GetWorkflowsAsync);
        group.MapPost("/workflows/{name}/run", RunWorkflowAsync);

        // Execution history endpoints
        group.MapGet("/executions", GetExecutionsAsync);
        group.MapGet("/executions/{id}", GetExecutionByIdAsync);

        return app;
    }

    private static async Task<IResult> GetStatusAsync(
        CopilotAuthProvider authProvider,
        CancellationToken ct)
    {
        var status = await authProvider.GetStatusAsync(ct).ConfigureAwait(false);
        return Results.Ok(status);
    }

    private static async Task<IResult> GetLlmStatusAsync(
        LlmProviderOptions options,
        CopilotAuthProvider authProvider,
        CancellationToken ct)
    {
        var authStatus = await authProvider.GetStatusAsync(ct).ConfigureAwait(false);
        return Results.Ok(new LlmProviderStatus
        {
            Configured = options.IsConfigured || authStatus.IsAuthenticated,
            Provider = options.IsConfigured ? options.Provider
                     : authStatus.IsAuthenticated ? "github-models" : null,
            Model = options.IsConfigured ? options.Model
                  : authStatus.IsAuthenticated ? "gpt-4o-mini" : null
        });
    }

    private static async Task ChatAsync(
        ChatRequest request,
        CopilotAdapterFactory factory,
        DuckDbStore store,
        HttpContext ctx,
        CopilotAuthProvider authProvider,
        LlmProviderOptions llmOptions,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var routing = TrackModeRouter.Resolve(request.Mode, request.Prompt, request.Context?.AdditionalContext);
        var routeContext = TrackModeRouter.BuildRoutingContext(routing);
        var routedSystemPrompt = TrackModeRouter.MergeSystemPrompt(request.SystemPrompt, routing.EffectiveMode);
        var routedContext = BuildAdapterContext(request.Context, routedSystemPrompt, routeContext);
        var routeMode = TrackModeRouter.ToWireValue(routing.EffectiveMode);
        var requestMode = TrackModeRouter.ToWireValue(request.Mode);
        var traceId = Activity.Current?.TraceId.ToString();
        var runBaseContext = new AgentRunAuditContext
        {
            RunId = Guid.NewGuid().ToString("N"),
            TraceId = traceId,
            AgentType = "chat",
            AgentName = "qyl.copilot.chat",
            RequestedMode = requestMode,
            TrackMode = routeMode,
            RouterReason = routing.Reason,
            StartTimeUnixNano = TimeConversions.ToUnixNanoUnsigned(TimeProvider.System.GetUtcNow())
        };

        ctx.Response.Headers["x-qyl-track-mode"] = routeMode;

        var hasGitHubAuth = false;

        // 1. Copilot adapter (free via GitHub auth) — try first
        if ((await authProvider.GetStatusAsync(ct).ConfigureAwait(false)).IsAuthenticated)
        {
            hasGitHubAuth = true;
            try
            {
                var adapter = await factory.GetAdapterAsync(ct).ConfigureAwait(false);
                await StreamSseWithAuditAsync(ctx,
                    adapter.ChatAsync(request.Prompt, routedContext, ct),
                    store,
                    runBaseContext with { Provider = "copilot", Model = "github-copilot" },
                    ct);
                return;
            }
            catch (InvalidOperationException)
            {
                // Auth expired or Copilot SDK failed — fall through to GitHub Models
            }
        }

        // 1.5. GitHub Models (free for all GitHub users — automatic fallback)
        if (hasGitHubAuth)
        {
            try
            {
                var authResult = await authProvider.GetTokenAsync(ct).ConfigureAwait(false);
                if (authResult is { Success: true, Token: { Length: > 0 } ghToken })
                {
                    var ghModelsOptions = new LlmProviderOptions
                    {
                        Provider = "github-models",
                        ApiKey = ghToken,
                        Model = "gpt-4o-mini"
                    };
                    var httpClient = httpClientFactory.CreateClient("qyl-llm-github-models");
                    using var client = LlmProviderFactory.Create(ghModelsOptions, httpClient);
                    if (client is not null)
                    {
                        await StreamSseWithAuditAsync(ctx,
                            StreamByokChatAsync(client, request.Prompt, routedSystemPrompt, ct),
                            store,
                            runBaseContext with
                            {
                                Provider = ghModelsOptions.Provider,
                                Model = ghModelsOptions.Model
                            },
                            ct);
                        return;
                    }
                }
            }
            catch (HttpRequestException)
            {
                // GitHub Models unavailable (rate limit, scope issue) — fall through
            }
            catch (InvalidOperationException)
            {
                // Provider creation failed — fall through
            }
        }

        // 2. Server-configured LLM (QYL_LLM_PROVIDER)
        if (llmOptions.IsConfigured)
        {
            var httpClient = httpClientFactory.CreateClient("qyl-llm");
            using var client = LlmProviderFactory.Create(llmOptions, httpClient);
            if (client is not null)
            {
                await StreamSseWithAuditAsync(ctx,
                    StreamByokChatAsync(client, request.Prompt, routedSystemPrompt, ct),
                    store,
                    runBaseContext with
                    {
                        Provider = llmOptions.Provider,
                        Model = llmOptions.Model
                    },
                    ct);
                return;
            }
        }

        // 3. BYOK: visitor provides their own key per-request
        if (request.Llm is { Provider: { Length: > 0 } } byok)
        {
            var byokOptions = new LlmProviderOptions
            {
                Provider = byok.Provider,
                ApiKey = byok.ApiKey,
                Model = byok.Model,
                Endpoint = byok.Endpoint
            };

            IChatClient? client = null;
            try
            {
                var httpClient = httpClientFactory.CreateClient("qyl-llm-byok");
                client = LlmProviderFactory.Create(byokOptions, httpClient);
                if (client is null)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsJsonAsync(
                        new { error = $"Unsupported BYOK provider: {byok.Provider}" }, ct).ConfigureAwait(false);
                    return;
                }

                await StreamSseWithAuditAsync(ctx,
                    StreamByokChatAsync(client, request.Prompt, routedSystemPrompt, ct),
                    store,
                    runBaseContext with
                    {
                        Provider = byok.Provider,
                        Model = byok.Model
                    },
                    ct);
            }
            catch (InvalidOperationException ex)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message }, ct).ConfigureAwait(false);
            }
            finally
            {
                client?.Dispose();
            }

            return;
        }

        // 4. Nothing available — provide actionable guidance
        ctx.Response.StatusCode = 503;
        var errorMessage = hasGitHubAuth
            ? "GitHub connected but all automatic providers failed. " +
              "Configure an LLM (QYL_LLM_PROVIDER=ollama|openai|anthropic|github-models) or provide an API key in Settings."
            : "No LLM configured. Options: (1) Connect GitHub for free GitHub Models access, " +
              "(2) set QYL_LLM_PROVIDER + QYL_LLM_API_KEY, or (3) provide an API key in Settings.";

        await ctx.Response.WriteAsJsonAsync(new
        {
            error = errorMessage,
            byokSupported = true,
            gitHubAuthenticated = hasGitHubAuth
        }, ct).ConfigureAwait(false);
    }

    private static CopilotContext? BuildAdapterContext(
        CopilotContext? baseContext,
        string? routedSystemPrompt,
        string? routeContext)
    {
        var mergedAdditionalContext = MergeContextSegments(
            routedSystemPrompt,
            routeContext,
            baseContext?.AdditionalContext);

        if (mergedAdditionalContext is null)
        {
            return baseContext;
        }

        return (baseContext ?? new CopilotContext()) with { AdditionalContext = mergedAdditionalContext };
    }

    private static string? MergeContextSegments(params string?[] segments)
    {
        var values = segments
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Select(static s => s!.Trim())
            .ToArray();

        return values.Length is 0 ? null : string.Join("\n\n", values);
    }

    /// <summary>
    ///     Streams chat responses from a BYOK IChatClient as StreamUpdate events.
    ///     No GitHub Copilot SDK required — uses Microsoft.Extensions.AI directly.
    /// </summary>
    private static async IAsyncEnumerable<StreamUpdate> StreamByokChatAsync(
        IChatClient client,
        string prompt,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        IAsyncEnumerable<ChatResponseUpdate> streaming;
        if (systemPrompt is not null)
        {
            var messages = new List<AiChatMessage>
            {
                new(AiChatRole.System, systemPrompt),
                new(AiChatRole.User, prompt)
            };
            streaming = client.GetStreamingResponseAsync(messages, cancellationToken: ct);
        }
        else
        {
            streaming = client.GetStreamingResponseAsync(prompt, cancellationToken: ct);
        }

        await foreach (var update in streaming.ConfigureAwait(false))
        {
            var now = TimeProvider.System.GetUtcNow();
            var text = update.Text;
            if (!string.IsNullOrEmpty(text))
            {
                yield return new StreamUpdate
                {
                    Kind = StreamUpdateKind.Content,
                    Content = text,
                    Timestamp = now
                };
            }
        }

        yield return new StreamUpdate
        {
            Kind = StreamUpdateKind.Completed,
            Timestamp = TimeProvider.System.GetUtcNow()
        };
    }

    private static async Task<IResult> GetWorkflowsAsync(
        WorkflowEngineFactory engineFactory,
        CancellationToken ct)
    {
        try
        {
            var engine = await engineFactory.GetEngineAsync(ct).ConfigureAwait(false);
            var workflows = engine.GetWorkflows();

            var dtos = workflows.Select(static w => new WorkflowDto
            {
                Name = w.Name,
                Description = w.Description,
                Trigger = w.Trigger.ToString().ToUpperInvariant(),
                Tools = w.Tools
            }).ToList();

            return Results.Ok(new WorkflowListResponse { Workflows = dtos });
        }
        catch (InvalidOperationException ex) when (ex.Message.ContainsOrdinal("Authentication failed"))
        {
            return Results.Ok(new WorkflowListResponse { Workflows = [] });
        }
    }

    private static async Task RunWorkflowAsync(
        string name,
        WorkflowRunRequest? request,
        WorkflowEngineFactory engineFactory,
        DuckDbStore store,
        HttpContext ctx,
        CancellationToken ct)
    {
        var requestedMode = request?.Mode ?? TrackMode.Auto;
        var routing = TrackModeRouter.Resolve(requestedMode, name, request?.Context?.AdditionalContext);
        var routeMode = TrackModeRouter.ToWireValue(routing.EffectiveMode);
        var traceId = Activity.Current?.TraceId.ToString();
        var auditContext = new AgentRunAuditContext
        {
            RunId = Guid.NewGuid().ToString("N"),
            TraceId = traceId,
            AgentType = "workflow",
            AgentName = $"qyl.copilot.workflow:{name}",
            Provider = "copilot",
            Model = "github-copilot",
            RequestedMode = TrackModeRouter.ToWireValue(requestedMode),
            TrackMode = routeMode,
            RouterReason = routing.Reason,
            StartTimeUnixNano = TimeConversions.ToUnixNanoUnsigned(TimeProvider.System.GetUtcNow())
        };
        ctx.Response.Headers["x-qyl-track-mode"] = routeMode;

        IAsyncEnumerable<StreamUpdate> stream;
        try
        {
            var engine = await engineFactory.GetEngineAsync(ct).ConfigureAwait(false);
            stream = engine.ExecuteAsync(name,
                request?.Parameters,
                request?.Context?.AdditionalContext,
                requestedMode,
                ct);
        }
        catch (Exception ex)
        {
            stream = StreamEngineFailureAsync(ex.Message, ct);
        }

        await StreamSseWithAuditAsync(ctx,
            stream,
            store,
            auditContext,
            ct);
    }

    private static readonly string[] s_sensitiveToolKeywords =
    [
        "apply", "delete", "deploy", "exec", "git", "migrate", "open_fix_pr", "patch", "pr", "rollback", "shell",
        "write"
    ];

    private static readonly string[] s_deniedKeywords =
    [
        "approval", "denied", "forbidden", "not allowed", "permission", "unauthorized"
    ];

    private static async Task StreamSseWithAuditAsync(
        HttpContext ctx,
        IAsyncEnumerable<StreamUpdate> updates,
        DuckDbStore store,
        AgentRunAuditContext auditContext,
        CancellationToken ct)
    {
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers.Connection = "keep-alive";

        var inputTokens = 0L;
        var outputTokens = 0L;
        var status = "running";
        string? error = null;
        var toolStates = new List<ToolAuditState>();
        var pendingToolStates = new Queue<int>();
        var decisionRecords = new List<AgentDecisionRecord> { CreateRouterDecision(auditContext) };
        var sequence = 0;

        try
        {
            await foreach (var update in updates.ConfigureAwait(false).WithCancellation(ct))
            {
                ObserveStreamUpdate(update, auditContext, toolStates, pendingToolStates, ref sequence,
                    ref inputTokens, ref outputTokens, ref status, ref error);
                await WriteSseEventAsync(ctx, update, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            status = "cancelled";
            error ??= "Request cancelled";
        }
        catch (InvalidOperationException ex) when (ex.Message.ContainsOrdinal("Authentication failed"))
        {
            status = "failed";
            error = "Copilot authentication not available";
            await WriteSseEventAsync(ctx,
                new StreamUpdate
                {
                    Kind = StreamUpdateKind.Error,
                    Error = error,
                    Timestamp = TimeProvider.System.GetUtcNow()
                },
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            status = "failed";
            error = ex.Message;
            await WriteSseEventAsync(ctx,
                new StreamUpdate
                {
                    Kind = StreamUpdateKind.Error,
                    Error = error,
                    Timestamp = TimeProvider.System.GetUtcNow()
                },
                ct).ConfigureAwait(false);
        }
        finally
        {
            await PersistAuditAsync(store, auditContext, toolStates, decisionRecords,
                inputTokens, outputTokens, status, error).ConfigureAwait(false);
        }
    }

    private static async Task PersistAuditAsync(
        DuckDbStore store,
        AgentRunAuditContext auditContext,
        List<ToolAuditState> toolStates,
        List<AgentDecisionRecord> decisionRecords,
        long inputTokens,
        long outputTokens,
        string status,
        string? error)
    {
        var endTime = TimeProvider.System.GetUtcNow();
        var endTimeUnixNano = TimeConversions.ToUnixNanoUnsigned(endTime);
        var durationNs = (long)(endTimeUnixNano - auditContext.StartTimeUnixNano);
        if (status is "running")
        {
            status = string.IsNullOrWhiteSpace(error) ? "completed" : "failed";
        }

        foreach (var state in toolStates.Where(static s => s.Call.EndTime is null))
        {
            state.Call = state.Call with
            {
                EndTime = endTimeUnixNano,
                DurationNs = state.Call.StartTime.HasValue ? (long)(endTimeUnixNano - state.Call.StartTime.Value) : null,
                Status = status is "completed" ? "completed" : "failed",
                ErrorMessage = state.Call.ErrorMessage ?? (status is "completed" ? null : "Run ended before tool completed")
            };

            if (state.Decision.RequiresApproval &&
                string.Equals(state.Decision.ApprovalStatus, "awaiting_approval", StringComparison.OrdinalIgnoreCase))
            {
                state.Decision = state.Decision with { Outcome = "awaiting_approval" };
            }
            else if (string.Equals(state.Decision.Outcome, "executing", StringComparison.OrdinalIgnoreCase))
            {
                state.Decision = state.Decision with { Outcome = status is "completed" ? "executed" : "failed" };
            }
        }

        decisionRecords.AddRange(toolStates.Select(static s => s.Decision));
        var approvalStatus = ComputeRunApprovalStatus(decisionRecords);
        var evidenceCount = decisionRecords.Sum(static d => CountEvidenceLinks(d.EvidenceJson));

        var auditSummary = new AgentRunAudit
        {
            RunId = auditContext.RunId,
            TraceId = auditContext.TraceId,
            TrackMode = auditContext.TrackMode,
            ApprovalStatus = approvalStatus,
            DecisionCount = decisionRecords.Count,
            EvidenceCount = evidenceCount
        };

        var runRecord = new AgentRunRecord
        {
            RunId = auditContext.RunId,
            TraceId = auditContext.TraceId,
            AgentName = auditContext.AgentName,
            AgentType = auditContext.AgentType,
            Provider = auditContext.Provider,
            Model = auditContext.Model,
            Status = status,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalCost = 0,
            ToolCallCount = toolStates.Count,
            StartTime = auditContext.StartTimeUnixNano,
            EndTime = endTimeUnixNano,
            DurationNs = durationNs,
            ErrorMessage = error,
            MetadataJson = JsonSerializer.Serialize(auditSummary),
            TrackMode = auditContext.TrackMode,
            ApprovalStatus = approvalStatus,
            EvidenceCount = evidenceCount
        };

        await store.InsertAgentRunAsync(runRecord, CancellationToken.None).ConfigureAwait(false);

        foreach (var state in toolStates)
        {
            await store.InsertToolCallAsync(state.Call, CancellationToken.None).ConfigureAwait(false);
        }

        foreach (var decision in decisionRecords)
        {
            await store.InsertAgentDecisionAsync(decision, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static void ObserveStreamUpdate(
        StreamUpdate update,
        AgentRunAuditContext auditContext,
        List<ToolAuditState> toolStates,
        Queue<int> pendingToolStates,
        ref int sequence,
        ref long inputTokens,
        ref long outputTokens,
        ref string status,
        ref string? error)
    {
        if (update.InputTokens.HasValue)
        {
            inputTokens = update.InputTokens.Value;
        }

        if (update.OutputTokens.HasValue)
        {
            outputTokens = update.OutputTokens.Value;
        }

        switch (update.Kind)
        {
            case StreamUpdateKind.ToolCall:
            {
                sequence++;
                var callId = $"{auditContext.RunId}-tool-{sequence:D4}";
                var eventTime = ToUnixNano(update.Timestamp);
                var requiresApproval = IsApprovalRequiredTool(update.ToolName);

                toolStates.Add(new ToolAuditState
                {
                    Call = new ToolCallRecord
                    {
                        CallId = callId,
                        RunId = auditContext.RunId,
                        TraceId = auditContext.TraceId,
                        ToolName = update.ToolName,
                        ToolType = "mcp",
                        ArgumentsJson = update.ToolArguments,
                        Status = "running",
                        StartTime = eventTime,
                        SequenceNumber = sequence
                    },
                    Decision = new AgentDecisionRecord
                    {
                        DecisionId = $"{callId}-decision",
                        RunId = auditContext.RunId,
                        TraceId = auditContext.TraceId,
                        DecisionType = requiresApproval ? "approval" : "tool",
                        Outcome = requiresApproval ? "awaiting_approval" : "executing",
                        RequiresApproval = requiresApproval,
                        ApprovalStatus = requiresApproval ? "awaiting_approval" : "not_required",
                        Reason = requiresApproval
                            ? $"Tool '{update.ToolName}' requires approval policy evaluation."
                            : $"Tool '{update.ToolName}' invocation started.",
                        EvidenceJson = BuildEvidenceJson(auditContext.TraceId, auditContext.RunId, update.ToolName),
                        CreatedAtUnixNano = eventTime
                    }
                });
                pendingToolStates.Enqueue(toolStates.Count - 1);
                break;
            }
            case StreamUpdateKind.ToolResult:
            {
                if (!pendingToolStates.TryDequeue(out var index))
                {
                    break;
                }

                var state = toolStates[index];
                var eventTime = ToUnixNano(update.Timestamp);
                var denied = IsDeniedToolResult(update);
                var failed = !string.IsNullOrWhiteSpace(update.Error) || denied;

                state.Call = state.Call with
                {
                    ResultJson = update.ToolResult,
                    EndTime = eventTime,
                    DurationNs = state.Call.StartTime.HasValue ? (long)(eventTime - state.Call.StartTime.Value) : null,
                    ErrorMessage = update.Error,
                    Status = failed ? "failed" : "completed"
                };

                var outcome = denied
                    ? "denied"
                    : failed
                        ? "failed"
                        : state.Decision.RequiresApproval ? "approved" : "executed";
                var approvalStatus = state.Decision.RequiresApproval
                    ? denied ? "denied" : "approved"
                    : "not_required";

                state.Decision = state.Decision with
                {
                    Outcome = outcome,
                    ApprovalStatus = approvalStatus,
                    Reason = BuildToolDecisionReason(state.Call.ToolName, update, outcome),
                    EvidenceJson = BuildEvidenceJson(auditContext.TraceId, auditContext.RunId, state.Call.ToolName),
                    CreatedAtUnixNano = eventTime
                };

                toolStates[index] = state;
                break;
            }
            case StreamUpdateKind.Completed:
                if (!string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    status = "completed";
                }

                break;
            case StreamUpdateKind.Error:
                if (!string.IsNullOrWhiteSpace(update.Error))
                {
                    status = "failed";
                    error = update.Error;
                }

                break;
        }

        if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(error))
        {
            error = "Execution failed";
        }
    }

    private static async Task WriteSseEventAsync(HttpContext ctx, StreamUpdate update, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(update, CopilotSerializerContext.Default.StreamUpdate);
        var eventName = MapEventName(update.Kind);
        await ctx.Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct).ConfigureAwait(false);
        await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<StreamUpdate> StreamEngineFailureAsync(
        string errorMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var message = string.IsNullOrWhiteSpace(errorMessage)
            ? "Workflow engine initialization failed."
            : errorMessage;

        yield return new StreamUpdate
        {
            Kind = StreamUpdateKind.Error,
            Error = message,
            Timestamp = TimeProvider.System.GetUtcNow()
        };

        await Task.CompletedTask;
    }

    private static AgentDecisionRecord CreateRouterDecision(AgentRunAuditContext context) =>
        new()
        {
            DecisionId = $"{context.RunId}-routing",
            RunId = context.RunId,
            TraceId = context.TraceId,
            DecisionType = "routing",
            Outcome = "selected",
            RequiresApproval = false,
            ApprovalStatus = "not_required",
            Reason = context.RouterReason,
            EvidenceJson = BuildEvidenceJson(context.TraceId, context.RunId),
            MetadataJson = JsonSerializer.Serialize(new
            {
                requested_mode = context.RequestedMode,
                effective_mode = context.TrackMode
            }),
            CreatedAtUnixNano = context.StartTimeUnixNano
        };

    private static string ComputeRunApprovalStatus(IEnumerable<AgentDecisionRecord> decisions)
    {
        var approvalDecisions = decisions
            .Where(static d => d.RequiresApproval)
            .ToArray();

        if (approvalDecisions.Length is 0)
        {
            return "not_required";
        }

        if (approvalDecisions.Any(static d => string.Equals(d.ApprovalStatus, "denied", StringComparison.OrdinalIgnoreCase)))
        {
            return "denied";
        }

        if (approvalDecisions.Any(static d => string.Equals(d.ApprovalStatus, "awaiting_approval", StringComparison.OrdinalIgnoreCase)))
        {
            return "awaiting_approval";
        }

        return "approved";
    }

    private static ulong ToUnixNano(DateTimeOffset timestamp)
    {
        var value = timestamp == default ? TimeProvider.System.GetUtcNow() : timestamp;
        return TimeConversions.ToUnixNanoUnsigned(value);
    }

    private static bool IsApprovalRequiredTool(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        return s_sensitiveToolKeywords.Any(k => toolName.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDeniedToolResult(StreamUpdate update)
    {
        var text = $"{update.Error} {update.ToolResult}";
        return s_deniedKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildToolDecisionReason(string? toolName, StreamUpdate update, string outcome)
    {
        if (!string.IsNullOrWhiteSpace(update.Error))
        {
            return $"Tool '{toolName}' ended with error: {update.Error}";
        }

        return $"Tool '{toolName}' outcome: {outcome}.";
    }

    private static string? BuildEvidenceJson(string? traceId, string runId, string? toolName = null)
    {
        var links = new List<AgentEvidenceLink>();
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            links.Add(new AgentEvidenceLink { Label = "Trace", Href = $"/traces/{traceId}" });
        }

        links.Add(new AgentEvidenceLink { Label = "Agent Run", Href = $"/agents/{runId}" });

        if (!string.IsNullOrWhiteSpace(toolName))
        {
            links.Add(new AgentEvidenceLink { Label = $"Tool: {toolName}", Href = $"/agents/{runId}" });
        }

        return JsonSerializer.Serialize(links);
    }

    private static int CountEvidenceLinks(string? evidenceJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(evidenceJson);
            return document.RootElement.ValueKind is JsonValueKind.Array
                ? document.RootElement.GetArrayLength()
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private sealed record AgentRunAuditContext
    {
        public required string RunId { get; init; }
        public string? TraceId { get; init; }
        public required string AgentName { get; init; }
        public required string AgentType { get; init; }
        public string? Provider { get; init; }
        public string? Model { get; init; }
        public required string RequestedMode { get; init; }
        public required string TrackMode { get; init; }
        public string? RouterReason { get; init; }
        public required ulong StartTimeUnixNano { get; init; }
    }

    private sealed class ToolAuditState
    {
        public required ToolCallRecord Call { get; set; }
        public required AgentDecisionRecord Decision { get; set; }
    }

    /// <summary>
    ///     Maps StreamUpdateKind to snake_case SSE event names (AG-UI convention).
    /// </summary>
    private static string MapEventName(StreamUpdateKind kind) => kind switch
    {
        StreamUpdateKind.ToolCall => "tool_call",
        StreamUpdateKind.ToolResult => "tool_result",
        _ => kind.ToString().ToUpperInvariant()
    };

    private static async Task<IResult> GetExecutionsAsync(
        WorkflowEngineFactory engineFactory,
        string? workflow,
        string? status,
        int? limit,
        CancellationToken ct)
    {
        try
        {
            var engine = await engineFactory.GetEngineAsync(ct).ConfigureAwait(false);

            WorkflowStatus? statusFilter = status is not null && Enum.TryParse<WorkflowStatus>(status, true, out var s)
                ? s
                : null;

            var executions = await engine.GetExecutionsAsync(workflow, statusFilter, limit ?? 50, ct)
                .ConfigureAwait(false);

            var dtos = executions.Select(static e => new ExecutionDto
            {
                Id = e.Id,
                WorkflowName = e.WorkflowName,
                Status = e.Status.ToString().ToUpperInvariant(),
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                Error = e.Error,
                InputTokens = e.InputTokens,
                OutputTokens = e.OutputTokens,
                TraceId = e.TraceId
            }).ToList();

            return Results.Ok(new ExecutionListResponse { Executions = dtos, Total = dtos.Count });
        }
        catch (InvalidOperationException ex) when (ex.Message.ContainsOrdinal("Authentication failed"))
        {
            return Results.Ok(new ExecutionListResponse { Executions = [], Total = 0 });
        }
    }

    private static async Task<IResult> GetExecutionByIdAsync(
        string id,
        WorkflowEngineFactory engineFactory,
        CancellationToken ct)
    {
        try
        {
            var engine = await engineFactory.GetEngineAsync(ct).ConfigureAwait(false);
            if (await engine.GetExecutionAsync(id, ct).ConfigureAwait(false) is not { } execution)
                return Results.NotFound();

            return Results.Ok(new ExecutionDto
            {
                Id = execution.Id,
                WorkflowName = execution.WorkflowName,
                Status = execution.Status.ToString().ToUpperInvariant(),
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                Result = execution.Result,
                Error = execution.Error,
                InputTokens = execution.InputTokens,
                OutputTokens = execution.OutputTokens,
                TraceId = execution.TraceId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.ContainsOrdinal("Authentication failed"))
        {
            return Results.NotFound();
        }
    }
}

/// <summary>
///     Workflow execution DTO for list and detail responses.
///     Result is populated only in detail responses (null-ignored in list via JsonIgnoreCondition).
/// </summary>
internal sealed record ExecutionDto
{
    public required string Id { get; init; }
    public required string WorkflowName { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Result { get; init; }
    public string? Error { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public string? TraceId { get; init; }
}

/// <summary>
///     Response containing execution history.
/// </summary>
internal sealed record ExecutionListResponse
{
    public required IReadOnlyList<ExecutionDto> Executions { get; init; }
    public int Total { get; init; }
}

/// <summary>
///     Workflow list item for the REST API.
/// </summary>
internal sealed record WorkflowDto
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Trigger { get; init; }
    public IEnumerable<string> Tools { get; init; } = [];
}

/// <summary>
///     Response containing available workflows.
/// </summary>
internal sealed record WorkflowListResponse
{
    public required IReadOnlyList<WorkflowDto> Workflows { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CopilotAuthStatus))]
[JsonSerializable(typeof(TrackMode))]
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ByokLlmConfig))]
[JsonSerializable(typeof(WorkflowRunRequest))]
[JsonSerializable(typeof(StreamUpdate))]
[JsonSerializable(typeof(AgentRunAudit))]
[JsonSerializable(typeof(AgentDecision))]
[JsonSerializable(typeof(AgentEvidenceLink))]
[JsonSerializable(typeof(List<AgentEvidenceLink>))]
[JsonSerializable(typeof(WorkflowDto))]
[JsonSerializable(typeof(WorkflowListResponse))]
[JsonSerializable(typeof(ExecutionDto))]
[JsonSerializable(typeof(ExecutionListResponse))]
[JsonSerializable(typeof(LlmProviderStatus))]
internal sealed partial class CopilotSerializerContext : JsonSerializerContext;
