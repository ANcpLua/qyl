using Qyl.ServiceDefaults.Instrumentation;

namespace qyl.copilot.Instrumentation;

/// <summary>
///     Metrics for qyl Copilot — auto-generated via [Meter] attributes.
///     Covers both OTel 1.39 GenAI client metrics and qyl-specific workflow metrics.
/// </summary>
[Meter("qyl.copilot", Version = "1.0.0")]
public static partial class CopilotMetrics
{
    [Histogram("gen_ai.client.token.usage", Unit = "{token}",
        Description = "Measures the number of tokens used in GenAI operations")]
    public static partial void RecordTokenUsage(
        long value,
        [Tag("gen_ai.system")] string system,
        [Tag("gen_ai.token.type")] string tokenType);

    [Histogram("gen_ai.client.operation.duration", Unit = "s", Description = "Duration of GenAI operations in seconds")]
    public static partial void RecordOperationDuration(
        double value,
        [Tag("gen_ai.system")] string system,
        [Tag("gen_ai.operation.name")] string operation);

    [Histogram("gen_ai.client.time_to_first_token", Unit = "s",
        Description = "Time to first token in streaming GenAI operations")]
    public static partial void RecordTimeToFirstToken(
        double value,
        [Tag("gen_ai.system")] string system);

    [Histogram("qyl.copilot.workflow.duration", Unit = "s",
        Description = "Duration of qyl workflow executions in seconds")]
    public static partial void RecordWorkflowDuration(
        double value,
        [Tag("qyl.workflow.name")] string workflowName,
        [Tag("qyl.workflow.status")] string status);

    [Counter("qyl.copilot.workflow.executions", Unit = "{execution}",
        Description = "Number of qyl workflow executions")]
    public static partial void RecordWorkflowExecution(
        [Tag("qyl.workflow.name")] string workflowName,
        [Tag("qyl.workflow.trigger")] string trigger);

    [Histogram("gen_ai.client.tool.duration", Unit = "s", Description = "Duration of tool execution")]
    public static partial void RecordToolDuration(
        double value,
        [Tag("gen_ai.tool.name")] string toolName,
        [Tag("gen_ai.system")] string system);

    [Counter("gen_ai.client.tool.calls", Unit = "{call}", Description = "Number of tool calls")]
    public static partial void RecordToolCall(
        [Tag("gen_ai.tool.name")] string toolName,
        [Tag("gen_ai.system")] string system);
}
