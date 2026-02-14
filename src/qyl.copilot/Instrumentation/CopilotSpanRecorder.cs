// =============================================================================
// qyl.copilot - Span Recorder
// OTel 1.39 GenAI semantic convention attribute recording
// =============================================================================

using qyl.protocol.Attributes;
using Qyl.ServiceDefaults.Instrumentation.GenAi;

namespace qyl.copilot.Instrumentation;

/// <summary>
///     Records OTel 1.39 GenAI semantic convention attributes on spans.
///     Provides helpers for both standard gen_ai.* and qyl-specific attributes.
/// </summary>
public static class CopilotSpanRecorder
{
    /// <summary>
    ///     Records token usage on a span per OTel 1.39.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="inputTokens">Input/prompt tokens.</param>
    /// <param name="outputTokens">Output/completion tokens.</param>
    public static void RecordTokenUsage(Activity? activity, int inputTokens, int outputTokens)
    {
        if (activity is null) return;

        activity.SetTag(GenAiAttributes.UsageInputTokens, inputTokens);
        activity.SetTag(GenAiAttributes.UsageOutputTokens, outputTokens);
    }

    /// <summary>
    ///     Records time to first token for streaming responses.
    ///     Uses standard OTel gen_ai.client.time_to_first_token metric.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="ttftSeconds">Time to first token in seconds.</param>
    public static void RecordTimeToFirstToken(Activity? activity, double ttftSeconds)
    {
        // Record on span as event
        activity?.AddEvent(new ActivityEvent("first_token", tags: new ActivityTagsCollection
        {
            { "gen_ai.client.time_to_first_token", ttftSeconds }
        }));

        // Also record as metric
        CopilotMetrics.RecordTimeToFirstToken(ttftSeconds, CopilotInstrumentation.GenAiSystem);
    }

    /// <summary>
    ///     Records workflow execution status.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="status">The workflow status (completed, failed, cancelled).</param>
    public static void RecordWorkflowStatus(Activity? activity, string status)
    {
        if (activity is null) return;

        activity.SetTag(CopilotInstrumentation.AttrWorkflowStatus, status);
    }

    // =========================================================================
    // Error Recording
    // =========================================================================

    /// <summary>
    ///     Records an error on a span per OTel conventions.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="exception">The exception that occurred.</param>
    public static void RecordError(Activity? activity, Exception exception)
    {
        GenAiInstrumentation.RecordException(activity, exception);
    }

    /// <summary>
    ///     Marks a span as successful.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    public static void RecordSuccess(Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}
