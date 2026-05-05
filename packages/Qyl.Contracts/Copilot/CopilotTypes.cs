
using System.Text.Json.Serialization;

namespace Qyl.Contracts.Copilot;

[JsonConverter(typeof(JsonStringEnumConverter<TrackMode>))]
public enum TrackMode
{
    Auto,

    Creative,

    Reasoning,

    Enterprise
}

public enum ChatRole
{
    System,

    User,

    Assistant,

    Tool
}

public enum StreamUpdateKind
{
    Content,

    ToolCall,

    ToolResult,

    Completed,

    Error,

    Progress,

    Metadata
}

public sealed record StreamUpdate
{
    public required StreamUpdateKind Kind { get; init; }

    public string? Content { get; init; }

    public string? ToolName { get; init; }

    public string? ToolArguments { get; init; }

    public string? ToolResult { get; init; }

    public string? Error { get; init; }

    public int? Progress { get; init; }

    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}

public sealed record ChatMessage
{
    public required ChatRole Role { get; init; }

    public required string Content { get; init; }

    public string? ToolCallId { get; init; }

    public string? ToolName { get; init; }

    public DateTimeOffset? Timestamp { get; init; }
}

public sealed record CopilotContext
{
    public string? SessionId { get; init; }

    public IReadOnlyList<ChatMessage>? History { get; init; }

    public IReadOnlyDictionary<string, string>? Parameters { get; init; }

    public string? AdditionalContext { get; init; }

    public int? MaxTokens { get; init; }

    public double? Temperature { get; init; }
}

public sealed record CopilotAuthStatus
{
    public required bool IsAuthenticated { get; init; }

    public string? AuthMethod { get; init; }

    public string? Username { get; init; }

    public IReadOnlyList<string>? Capabilities { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public string? Error { get; init; }
}

public sealed record ChatRequest
{
    public required string Prompt { get; init; }

    public TrackMode Mode { get; init; } = TrackMode.Auto;

    public string? SystemPrompt { get; init; }

    public CopilotContext? Context { get; init; }

    public ByokLlmConfig? Llm { get; init; }
}

public sealed record ByokLlmConfig
{
    public required string Provider { get; init; }

    public string? ApiKey { get; init; }

    public string? Model { get; init; }

    public string? Endpoint { get; init; }
}

public sealed record WorkflowRunRequest
{
    public required string WorkflowName { get; init; }

    public TrackMode Mode { get; init; } = TrackMode.Auto;

    public IReadOnlyDictionary<string, string>? Parameters { get; init; }

    public CopilotContext? Context { get; init; }
}
