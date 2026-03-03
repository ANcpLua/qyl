// =============================================================================
// qyl.copilot - OpenTelemetry Instrumentation
// OTel 1.40 GenAI semantic conventions for GitHub Copilot
// Metrics are auto-generated via CopilotMetrics [Meter] class
// =============================================================================

using qyl.protocol.Attributes;
using Qyl.ServiceDefaults.Instrumentation.GenAi;

namespace qyl.copilot.Instrumentation;

/// <summary>
///     OpenTelemetry instrumentation for qyl Copilot integration.
///     Follows OTel 1.40 GenAI semantic conventions.
///     Metrics are defined declaratively in <see cref="CopilotMetrics" />.
/// </summary>
public static class CopilotInstrumentation
{
    // =========================================================================
    // Provider & Operation Values
    // =========================================================================

    /// <summary>
    ///     gen_ai.provider.name value for GitHub Copilot (OTel 1.40).
    /// </summary>
    public const string GenAiProviderName = GenAiAttributes.Providers.GitHubCopilot;

    /// <summary>
    ///     gen_ai.request.model value for GitHub Copilot chat requests (OTel 1.40).
    /// </summary>
    public const string GenAiRequestModel = "github-copilot";

    /// <summary>
    ///     gen_ai.operation.name for chat operations.
    /// </summary>
    public const string OperationChat = GenAiAttributes.Operations.Chat;

    /// <summary>
    ///     gen_ai.operation.name for workflow operations (OTel 1.40 agent span).
    /// </summary>
    public const string OperationWorkflow = GenAiAttributes.Operations.InvokeAgent;

    /// <summary>
    ///     gen_ai.operation.name for tool execution.
    /// </summary>
    public const string OperationExecuteTool = GenAiAttributes.Operations.ExecuteTool;

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

    // =========================================================================
    // Span Creation Helpers
    // =========================================================================

    /// <summary>
    ///     Starts a chat operation span with OTel 1.40 GenAI attributes.
    /// </summary>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartChatSpan()
    {
        if (ActivitySource.StartActivity("gen_ai.chat", ActivityKind.Client) is not { } activity) return null;

        activity.SetTag(GenAiAttributes.ProviderName, GenAiProviderName);
        activity.SetTag(GenAiAttributes.RequestModel, GenAiRequestModel);
        activity.SetTag(GenAiAttributes.OperationName, OperationChat);
        activity.SetTag(GenAiAttributes.OutputType, GenAiAttributes.OutputTypes.Text);

        return activity;
    }

    /// <summary>
    ///     Starts a workflow execution span with OTel 1.40 GenAI attributes.
    /// </summary>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="executionId">The unique execution ID.</param>
    /// <param name="trigger">The trigger type.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartWorkflowSpan(string workflowName, string? executionId = null, string? trigger = null)
    {
        if (ActivitySource.StartActivity($"{OperationWorkflow} {workflowName}", ActivityKind.Client) is not { } activity)
            return null;

        activity.SetTag(GenAiAttributes.ProviderName, GenAiProviderName);
        activity.SetTag(GenAiAttributes.RequestModel, GenAiRequestModel);
        activity.SetTag(GenAiAttributes.OperationName, OperationWorkflow);
        activity.SetTag(GenAiAttributes.OutputType, GenAiAttributes.OutputTypes.Text);
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
    ///     Starts a tool execution span with OTel 1.40 GenAI attributes.
    /// </summary>
    /// <param name="toolName">The tool/function name.</param>
    /// <param name="toolCallId">The tool call ID.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartToolSpan(string toolName, string? toolCallId = null)
    {
        if (GenAiInstrumentation.StartToolExecutionSpan(toolName, toolCallId) is not { } activity) return null;

        activity.SetTag(GenAiAttributes.ProviderName, GenAiProviderName);
        activity.SetTag(GenAiAttributes.RequestModel, GenAiRequestModel);

        return activity;
    }
}
