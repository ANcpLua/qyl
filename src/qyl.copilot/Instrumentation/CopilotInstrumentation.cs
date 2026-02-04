// =============================================================================
// qyl.copilot - OpenTelemetry Instrumentation
// OTel 1.39 GenAI semantic conventions for GitHub Copilot
// =============================================================================

namespace qyl.copilot.Instrumentation;

/// <summary>
///     OpenTelemetry instrumentation for qyl Copilot integration.
///     Follows OTel 1.39 GenAI semantic conventions.
/// </summary>
public static class CopilotInstrumentation
{
    // =========================================================================
    // OTel 1.39 GenAI Semantic Convention Constants
    // =========================================================================

    /// <summary>
    ///     gen_ai.system value for GitHub Copilot (OTel 1.39).
    /// </summary>
    public const string GenAiSystem = "github_copilot";

    /// <summary>
    ///     gen_ai.operation.name for chat operations.
    /// </summary>
    public const string OperationChat = "chat";

    /// <summary>
    ///     gen_ai.operation.name for workflow operations.
    /// </summary>
    public const string OperationWorkflow = "workflow";

    /// <summary>
    ///     gen_ai.operation.name for tool execution.
    /// </summary>
    public const string OperationExecuteTool = "execute_tool";

    // =========================================================================
    // Attribute Names (OTel 1.39 GenAI Semantic Conventions)
    // =========================================================================

    /// <summary>gen_ai.system - The GenAI provider name.</summary>
    public const string AttrGenAiSystem = "gen_ai.system";

    /// <summary>gen_ai.operation.name - The operation type.</summary>
    public const string AttrGenAiOperationName = "gen_ai.operation.name";

    /// <summary>gen_ai.request.model - The model requested.</summary>
    public const string AttrGenAiRequestModel = "gen_ai.request.model";

    /// <summary>gen_ai.response.model - The model used in response.</summary>
    public const string AttrGenAiResponseModel = "gen_ai.response.model";

    /// <summary>gen_ai.usage.input_tokens - Input/prompt tokens.</summary>
    public const string AttrGenAiInputTokens = "gen_ai.usage.input_tokens";

    /// <summary>gen_ai.usage.output_tokens - Output/completion tokens.</summary>
    public const string AttrGenAiOutputTokens = "gen_ai.usage.output_tokens";

    /// <summary>gen_ai.response.finish_reasons - Stop reasons.</summary>
    public const string AttrGenAiFinishReasons = "gen_ai.response.finish_reasons";

    /// <summary>gen_ai.tool.name - Tool/function name.</summary>
    public const string AttrGenAiToolName = "gen_ai.tool.name";

    /// <summary>gen_ai.tool.call_id - Tool invocation ID.</summary>
    public const string AttrGenAiToolCallId = "gen_ai.tool.call_id";

    // =========================================================================
    // qyl-Specific Attribute Names
    // =========================================================================

    /// <summary>qyl.workflow.name - Workflow name being executed.</summary>
    public const string AttrWorkflowName = "qyl.workflow.name";

    /// <summary>qyl.workflow.execution_id - Unique workflow execution ID.</summary>
    public const string AttrWorkflowExecutionId = "qyl.workflow.execution_id";

    /// <summary>qyl.workflow.status - Workflow execution status.</summary>
    public const string AttrWorkflowStatus = "qyl.workflow.status";

    /// <summary>qyl.workflow.trigger - Workflow trigger type.</summary>
    public const string AttrWorkflowTrigger = "qyl.workflow.trigger";

    // =========================================================================
    // Instrumentation Sources
    // =========================================================================

    /// <summary>
    ///     ActivitySource name for Copilot instrumentation.
    /// </summary>
    public const string SourceName = "qyl.copilot";

    /// <summary>
    ///     Meter name for Copilot metrics.
    /// </summary>
    public const string MeterName = "qyl.copilot";

    /// <summary>
    ///     ActivitySource for creating spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    /// <summary>
    ///     Meter for creating metrics.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    // =========================================================================
    // OTel 1.39 GenAI Metrics
    // =========================================================================

    /// <summary>
    ///     gen_ai.client.token.usage - Token consumption histogram.
    ///     Per OTel 1.39 GenAI semantic conventions.
    /// </summary>
    public static readonly Histogram<long> TokenUsage = Meter.CreateHistogram<long>(
        "gen_ai.client.token.usage",
        "{token}",
        "Measures the number of tokens used in GenAI operations");

    /// <summary>
    ///     gen_ai.client.operation.duration - Operation latency histogram.
    ///     Per OTel 1.39 GenAI semantic conventions.
    /// </summary>
    public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "gen_ai.client.operation.duration",
        "s",
        "Duration of GenAI operations in seconds");

    /// <summary>
    ///     gen_ai.client.time_to_first_token - Time to first token for streaming.
    ///     Per OTel 1.39 GenAI semantic conventions.
    /// </summary>
    public static readonly Histogram<double> TimeToFirstToken = Meter.CreateHistogram<double>(
        "gen_ai.client.time_to_first_token",
        "s",
        "Time to first token in streaming GenAI operations");

    // =========================================================================
    // qyl-Specific Workflow Metrics
    // =========================================================================

    /// <summary>
    ///     qyl.copilot.workflow.duration - Workflow execution duration.
    ///     Distinct from gen_ai.client.operation.duration (single LLM call).
    /// </summary>
    public static readonly Histogram<double> WorkflowDuration = Meter.CreateHistogram<double>(
        "qyl.copilot.workflow.duration",
        "s",
        "Duration of qyl workflow executions in seconds");

    /// <summary>
    ///     qyl.copilot.workflow.executions - Workflow execution count.
    /// </summary>
    public static readonly Counter<long> WorkflowExecutions = Meter.CreateCounter<long>(
        "qyl.copilot.workflow.executions",
        "{execution}",
        "Number of qyl workflow executions");

    // =========================================================================
    // Span Creation Helpers
    // =========================================================================

    /// <summary>
    ///     Starts a chat operation span with OTel 1.39 GenAI attributes.
    /// </summary>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartChatSpan()
    {
        var activity = ActivitySource.StartActivity("gen_ai.chat", ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag(AttrGenAiSystem, GenAiSystem);
        activity.SetTag(AttrGenAiOperationName, OperationChat);

        return activity;
    }

    /// <summary>
    ///     Starts a workflow execution span with OTel 1.39 GenAI attributes.
    /// </summary>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="executionId">The unique execution ID.</param>
    /// <param name="trigger">The trigger type.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartWorkflowSpan(string workflowName, string? executionId = null, string? trigger = null)
    {
        var activity = ActivitySource.StartActivity($"gen_ai.workflow {workflowName}", ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag(AttrGenAiSystem, GenAiSystem);
        activity.SetTag(AttrGenAiOperationName, OperationWorkflow);
        activity.SetTag(AttrWorkflowName, workflowName);

        if (executionId is not null)
        {
            activity.SetTag(AttrWorkflowExecutionId, executionId);
        }

        if (trigger is not null)
        {
            activity.SetTag(AttrWorkflowTrigger, trigger);
        }

        return activity;
    }

    /// <summary>
    ///     Starts a tool execution span with OTel 1.39 GenAI attributes.
    /// </summary>
    /// <param name="toolName">The tool/function name.</param>
    /// <param name="toolCallId">The tool call ID.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartToolSpan(string toolName, string? toolCallId = null)
    {
        var activity = ActivitySource.StartActivity($"gen_ai.execute_tool {toolName}", ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag(AttrGenAiSystem, GenAiSystem);
        activity.SetTag(AttrGenAiOperationName, OperationExecuteTool);
        activity.SetTag(AttrGenAiToolName, toolName);

        if (toolCallId is not null)
        {
            activity.SetTag(AttrGenAiToolCallId, toolCallId);
        }

        return activity;
    }
}
