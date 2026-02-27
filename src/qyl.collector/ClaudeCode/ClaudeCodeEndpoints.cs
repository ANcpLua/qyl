using System.Text.Json.Serialization;

namespace qyl.collector.ClaudeCode;

internal static class ClaudeCodeEndpoints
{
    public static WebApplication MapClaudeCodeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/claude-code");

        group.MapGet("/sessions", GetSessionsAsync);
        group.MapGet("/sessions/{sessionId}/timeline", GetTimelineAsync);
        group.MapGet("/sessions/{sessionId}/tools", GetToolSummaryAsync);
        group.MapGet("/sessions/{sessionId}/cost", GetCostBreakdownAsync);

        return app;
    }

    private static async Task<IResult> GetSessionsAsync(
        DuckDbStore store,
        int? limit,
        string? after,
        CancellationToken ct)
    {
        var sessions = await store.GetClaudeCodeSessionsAsync(
            limit ?? 50,
            after,
            ct).ConfigureAwait(false);

        return Results.Ok(new ClaudeCodeSessionsResponse
        {
            Sessions = sessions,
            Total = sessions.Count
        });
    }

    private static async Task<IResult> GetTimelineAsync(
        string sessionId,
        DuckDbStore store,
        CancellationToken ct)
    {
        var events = await store.GetClaudeCodeTimelineAsync(sessionId, ct).ConfigureAwait(false);
        return Results.Ok(new ClaudeCodeTimelineResponse
        {
            SessionId = sessionId,
            Events = events,
            Total = events.Count
        });
    }

    private static async Task<IResult> GetToolSummaryAsync(
        string sessionId,
        DuckDbStore store,
        CancellationToken ct)
    {
        var tools = await store.GetClaudeCodeToolSummaryAsync(sessionId, ct).ConfigureAwait(false);
        return Results.Ok(new ClaudeCodeToolSummaryResponse
        {
            SessionId = sessionId,
            Tools = tools,
            Total = tools.Count
        });
    }

    private static async Task<IResult> GetCostBreakdownAsync(
        string sessionId,
        DuckDbStore store,
        CancellationToken ct)
    {
        var breakdown = await store.GetClaudeCodeCostBreakdownAsync(sessionId, ct).ConfigureAwait(false);
        return Results.Ok(new ClaudeCodeCostResponse
        {
            SessionId = sessionId,
            Models = breakdown,
            TotalCostUsd = breakdown.Sum(static m => m.TotalCostUsd)
        });
    }
}

// =============================================================================
// DTOs
// =============================================================================

public sealed record ClaudeCodeSession
{
    public required string SessionId { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset LastActivityTime { get; init; }
    public int TotalPrompts { get; init; }
    public int TotalApiCalls { get; init; }
    public int TotalToolCalls { get; init; }
    public double TotalCostUsd { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public IReadOnlyList<string> Models { get; init; } = [];
    public string? TerminalType { get; init; }
    public string? ClaudeCodeVersion { get; init; }
}

public sealed record ClaudeCodeEvent
{
    public required string EventName { get; init; }
    public string? PromptId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? ToolName { get; init; }
    public string? Model { get; init; }
    public double? CostUsd { get; init; }
    public double? DurationMs { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public bool? Success { get; init; }
    public string? Decision { get; init; }
    public string? Error { get; init; }
    public int? PromptLength { get; init; }
}

public sealed record ClaudeCodeToolSummary
{
    public required string ToolName { get; init; }
    public int CallCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public double AvgDurationMs { get; init; }
    public int AcceptCount { get; init; }
    public int RejectCount { get; init; }
}

public sealed record ClaudeCodeCostBreakdown
{
    public required string Model { get; init; }
    public int ApiCalls { get; init; }
    public double TotalCostUsd { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long CacheReadTokens { get; init; }
    public long CacheCreationTokens { get; init; }
}

// =============================================================================
// Response wrappers
// =============================================================================

internal sealed record ClaudeCodeSessionsResponse
{
    public required IReadOnlyList<ClaudeCodeSession> Sessions { get; init; }
    public int Total { get; init; }
}

internal sealed record ClaudeCodeTimelineResponse
{
    public required string SessionId { get; init; }
    public required IReadOnlyList<ClaudeCodeEvent> Events { get; init; }
    public int Total { get; init; }
}

internal sealed record ClaudeCodeToolSummaryResponse
{
    public required string SessionId { get; init; }
    public required IReadOnlyList<ClaudeCodeToolSummary> Tools { get; init; }
    public int Total { get; init; }
}

internal sealed record ClaudeCodeCostResponse
{
    public required string SessionId { get; init; }
    public required IReadOnlyList<ClaudeCodeCostBreakdown> Models { get; init; }
    public double TotalCostUsd { get; init; }
}

// =============================================================================
// JSON serializer context (AOT-compatible)
// =============================================================================

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ClaudeCodeSession))]
[JsonSerializable(typeof(ClaudeCodeEvent))]
[JsonSerializable(typeof(ClaudeCodeToolSummary))]
[JsonSerializable(typeof(ClaudeCodeCostBreakdown))]
[JsonSerializable(typeof(ClaudeCodeSessionsResponse))]
[JsonSerializable(typeof(ClaudeCodeTimelineResponse))]
[JsonSerializable(typeof(ClaudeCodeToolSummaryResponse))]
[JsonSerializable(typeof(ClaudeCodeCostResponse))]
internal sealed partial class ClaudeCodeSerializerContext : JsonSerializerContext;
