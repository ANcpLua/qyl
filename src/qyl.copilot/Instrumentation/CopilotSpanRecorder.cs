// =============================================================================
// qyl.copilot - Span Recorder for qyl-specific Attributes
// SDK handles gen_ai.* attributes via UseOpenTelemetry()
// =============================================================================

using System.Diagnostics;

namespace qyl.copilot.Instrumentation;

/// <summary>
///     Records qyl-specific attributes on spans.
///     Note: SDK handles gen_ai.* attributes via UseOpenTelemetry().
///     This provides helpers for qyl-specific workflow metadata.
/// </summary>
public static class CopilotSpanRecorder
{
    // =========================================================================
    // qyl Workflow Attributes
    // SDK handles gen_ai.* attributes; these are qyl-specific additions
    // =========================================================================

    /// <summary>
    ///     Sets qyl workflow metadata on a span.
    /// </summary>
    public static void SetWorkflowContext(
        Activity? activity,
        string workflowName,
        string? executionId = null)
    {
        if (activity is null) return;

        activity.SetTag("qyl.workflow.name", workflowName);

        if (executionId is not null)
        {
            activity.SetTag("qyl.workflow.execution_id", executionId);
        }
    }

    /// <summary>
    ///     Records time to first token for streaming responses.
    /// </summary>
    public static void RecordTimeToFirstToken(Activity? activity, double ttftSeconds)
        => activity?.SetTag("qyl.copilot.time_to_first_token", ttftSeconds);

    /// <summary>
    ///     Records workflow execution status.
    /// </summary>
    public static void RecordWorkflowStatus(Activity? activity, string status)
        => activity?.SetTag("qyl.workflow.status", status);
}
