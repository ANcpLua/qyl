using Microsoft.Extensions.AI;
using qyl.copilot;
using qyl.copilot.Auth;
using qyl.copilot.Providers;
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
        HttpContext ctx,
        CopilotAuthProvider authProvider,
        LlmProviderOptions llmOptions,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var hasGitHubAuth = false;

        // 1. Copilot adapter (free via GitHub auth) — try first
        if ((await authProvider.GetStatusAsync(ct).ConfigureAwait(false)).IsAuthenticated)
        {
            hasGitHubAuth = true;
            try
            {
                var adapter = await factory.GetAdapterAsync(ct).ConfigureAwait(false);
                // Thread system prompt through context if provided
                var context = request.Context;
                if (request.SystemPrompt is not null)
                {
                    context = (context ?? new CopilotContext()) with
                    {
                        AdditionalContext = request.SystemPrompt +
                            (context?.AdditionalContext is not null ? "\n\n" + context.AdditionalContext : "")
                    };
                }
                await StreamSseAsync(ctx, adapter.ChatAsync(request.Prompt, context, ct), ct);
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
                        await StreamSseAsync(ctx,
                            StreamByokChatAsync(client, request.Prompt, request.SystemPrompt, ct), ct);
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
                await StreamSseAsync(ctx, StreamByokChatAsync(client, request.Prompt, request.SystemPrompt, ct), ct);
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

                await StreamSseAsync(ctx, StreamByokChatAsync(client, request.Prompt, request.SystemPrompt, ct), ct);
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
        HttpContext ctx,
        CancellationToken ct)
    {
        var engine = await engineFactory.GetEngineAsync(ct).ConfigureAwait(false);
        await StreamSseAsync(ctx,
            engine.ExecuteAsync(name, request?.Parameters, request?.Context?.AdditionalContext, ct), ct);
    }

    private static async Task StreamSseAsync(
        HttpContext ctx,
        IAsyncEnumerable<StreamUpdate> updates,
        CancellationToken ct)
    {
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var update in updates.ConfigureAwait(false).WithCancellation(ct))
            {
                var json = JsonSerializer.Serialize(update, CopilotSerializerContext.Default.StreamUpdate);
                var eventName = MapEventName(update.Kind);
                await ctx.Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct).ConfigureAwait(false);
                await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected - expected for SSE streams
        }
        catch (InvalidOperationException ex) when (ex.Message.ContainsOrdinal("Authentication failed"))
        {
            var error = new StreamUpdate
            {
                Kind = StreamUpdateKind.Error,
                Error = "Copilot authentication not available",
                Timestamp = TimeProvider.System.GetUtcNow()
            };
            var json = JsonSerializer.Serialize(error, CopilotSerializerContext.Default.StreamUpdate);
            await ctx.Response.WriteAsync($"event: error\ndata: {json}\n\n", ct).ConfigureAwait(false);
            await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);
        }
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
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ByokLlmConfig))]
[JsonSerializable(typeof(WorkflowRunRequest))]
[JsonSerializable(typeof(StreamUpdate))]
[JsonSerializable(typeof(WorkflowDto))]
[JsonSerializable(typeof(WorkflowListResponse))]
[JsonSerializable(typeof(ExecutionDto))]
[JsonSerializable(typeof(ExecutionListResponse))]
[JsonSerializable(typeof(LlmProviderStatus))]
internal sealed partial class CopilotSerializerContext : JsonSerializerContext;
