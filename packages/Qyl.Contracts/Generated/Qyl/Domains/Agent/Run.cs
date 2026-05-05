#nullable enable

namespace Qyl.Domains.Agent.Run;

public sealed class AgentRunEntity
{
    public required string RunId { get; init; }
    public required string TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ParentRunId { get; init; }
    public string? SessionId { get; init; }
    public required string AgentName { get; init; }
    public string? AgentId { get; init; }
    public string? AgentDescription { get; init; }
    public string? OperationName { get; init; }
    public string? GenAiRequestModel { get; init; }
    public string? GenAiProviderName { get; init; }
    public required Qyl.Domains.Agent.Run.AgentRunStatus Status { get; init; }
    public long? GenAiInputTokens { get; init; }
    public long? GenAiOutputTokens { get; init; }
    public double? GenAiCostUsd { get; init; }
    public int? ToolCallCount { get; init; }
    public required long StartTimeUnixNano { get; init; }
    public long? EndTimeUnixNano { get; init; }
    public long? DurationNs { get; init; }
    public string? ServiceName { get; init; }
    public string? ErrorMessage { get; init; }
    public string? AttributesJson { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

public enum AgentRunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}
