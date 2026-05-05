#nullable enable

namespace Qyl.Domains.Agent.Workflow;

public sealed class WorkflowExecutionEntity
{
    public required string ExecutionId { get; init; }
    public string? TraceId { get; init; }
    public required string WorkflowName { get; init; }
    public required Qyl.Domains.Agent.Workflow.WorkflowTrigger Trigger { get; init; }
    public required Qyl.Domains.Agent.Workflow.WorkflowExecutionStatus Status { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public long? GenAiInputTokens { get; init; }
    public long? GenAiOutputTokens { get; init; }
    public double? GenAiCostUsd { get; init; }
    public int? NodeCount { get; init; }
    public int? CompletedNodes { get; init; }
    public required long StartTimeUnixNano { get; init; }
    public long? EndTimeUnixNano { get; init; }
    public long? DurationNs { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

public enum WorkflowTrigger
{
    Manual,
    Schedule,
    Event,
    Workflow
}

public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
