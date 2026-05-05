#nullable enable

namespace Qyl.Domains.Agent.ToolCall;

public sealed class ToolCallEntity
{
    public required string CallId { get; init; }
    public required string RunId { get; init; }
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public required string ToolName { get; init; }
    public string? ToolType { get; init; }
    public string? ToolDescription { get; init; }
    public string? ArgumentsJson { get; init; }
    public string? ResultJson { get; init; }
    public required Qyl.Domains.Agent.ToolCall.ToolCallStatus Status { get; init; }
    public required long StartTimeUnixNano { get; init; }
    public long? EndTimeUnixNano { get; init; }
    public long? DurationNs { get; init; }
    public string? ErrorMessage { get; init; }
    public required int SequenceNumber { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

public enum ToolCallStatus
{
    Running,
    Completed,
    Failed
}
