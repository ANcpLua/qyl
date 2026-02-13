using qyl.copilot;
using qyl.copilot.Auth;
using qyl.protocol.Copilot;

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

    private static async Task ChatAsync(
        ChatRequest request,
        CopilotAdapterFactory factory,
        HttpContext ctx,
        CancellationToken ct)
    {
        var adapter = await factory.GetAdapterAsync(ct).ConfigureAwait(false);
        await StreamSseAsync(ctx, adapter.ChatAsync(request.Prompt, request.Context, ct), ct);
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
        catch (InvalidOperationException ex) when (ex.Message.Contains("Authentication failed", StringComparison.Ordinal))
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
        await StreamSseAsync(ctx, engine.ExecuteAsync(name, request?.Parameters, request?.Context?.AdditionalContext, ct), ct);
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
            await foreach (var update in updates.ConfigureAwait(false))
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
        catch (InvalidOperationException ex) when (ex.Message.Contains("Authentication failed", StringComparison.Ordinal))
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
        catch (InvalidOperationException ex) when (ex.Message.Contains("Authentication failed", StringComparison.Ordinal))
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
            var execution = await engine.GetExecutionAsync(id, ct).ConfigureAwait(false);

            if (execution is null)
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
        catch (InvalidOperationException ex) when (ex.Message.Contains("Authentication failed", StringComparison.Ordinal))
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
    public IReadOnlyList<string>? Tools { get; init; }
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
[JsonSerializable(typeof(WorkflowRunRequest))]
[JsonSerializable(typeof(StreamUpdate))]
[JsonSerializable(typeof(WorkflowDto))]
[JsonSerializable(typeof(WorkflowListResponse))]
[JsonSerializable(typeof(ExecutionDto))]
[JsonSerializable(typeof(ExecutionListResponse))]
internal sealed partial class CopilotSerializerContext : JsonSerializerContext;
