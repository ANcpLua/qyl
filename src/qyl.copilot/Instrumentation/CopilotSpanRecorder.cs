// =============================================================================
// qyl.copilot - Span Recorder
// OTel 1.39 GenAI semantic convention attribute recording
// =============================================================================

using qyl.protocol.Attributes;

namespace qyl.copilot.Instrumentation;

/// <summary>
///     Records OTel 1.39 GenAI semantic convention attributes on spans.
///     Provides helpers for both standard gen_ai.* and qyl-specific attributes.
/// </summary>
public static class CopilotSpanRecorder
{
    // =========================================================================
    // OTel 1.39 GenAI Attribute Recording
    // =========================================================================

    /// <summary>
    ///     Sets core GenAI attributes on a span.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="requestModel">The model requested.</param>
    /// <param name="responseModel">The model used (may differ from request).</param>
    public static void SetGenAiModel(Activity? activity, string? requestModel, string? responseModel = null)
    {
        if (activity is null) return;

        if (requestModel is not null)
        {
            activity.SetTag(GenAiAttributes.RequestModel, requestModel);
        }

        if (responseModel is not null)
        {
            activity.SetTag(GenAiAttributes.ResponseModel, responseModel);
        }
    }

    /// <summary>
    ///     Records token usage on a span per OTel 1.39.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="inputTokens">Input/prompt tokens.</param>
    /// <param name="outputTokens">Output/completion tokens.</param>
    public static void RecordTokenUsage(Activity? activity, long inputTokens, long outputTokens)
    {
        if (activity is null) return;

        activity.SetTag(GenAiAttributes.UsageInputTokens, inputTokens);
        activity.SetTag(GenAiAttributes.UsageOutputTokens, outputTokens);
    }

    /// <summary>
    ///     Records finish reasons on a span per OTel 1.39.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="finishReasons">The stop/finish reasons.</param>
    public static void RecordFinishReasons(Activity? activity, params string[] finishReasons)
    {
        if (activity is null || finishReasons.Length == 0) return;

        activity.SetTag(GenAiAttributes.ResponseFinishReasons, finishReasons);
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

    // =========================================================================
    // qyl Workflow Attribute Recording
    // =========================================================================

    /// <summary>
    ///     Sets qyl workflow metadata on a span.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="executionId">The unique execution ID.</param>
    /// <param name="trigger">The trigger type.</param>
    public static void SetWorkflowContext(
        Activity? activity,
        string workflowName,
        string? executionId = null,
        string? trigger = null)
    {
        if (activity is null) return;

        activity.SetTag(CopilotInstrumentation.AttrWorkflowName, workflowName);

        if (executionId is not null)
        {
            activity.SetTag(CopilotInstrumentation.AttrWorkflowExecutionId, executionId);
        }

        if (trigger is not null)
        {
            activity.SetTag(CopilotInstrumentation.AttrWorkflowTrigger, trigger);
        }
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
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);

        // Record exception as an event per OTel semantic conventions
        var exceptionTags = new ActivityTagsCollection
        {
            { GenAiAttributes.ExceptionType, exception.GetType().FullName },
            { GenAiAttributes.ExceptionMessage, exception.Message }
        };

        if (exception.StackTrace is not null)
        {
            exceptionTags.Add(GenAiAttributes.ExceptionStacktrace, exception.StackTrace);
        }

        activity.AddEvent(new ActivityEvent("exception", tags: exceptionTags));
    }

    /// <summary>
    ///     Records an error message on a span.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="errorMessage">The error message.</param>
    public static void RecordError(Activity? activity, string errorMessage)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, errorMessage);
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
