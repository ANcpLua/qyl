// =============================================================================
// qyl.copilot - Span Recorder
// OTel 1.39 GenAI semantic convention attribute recording
// =============================================================================

using qyl.protocol.Attributes;
using qyl.protocol.Attributes.Generated;
using Qyl.ServiceDefaults.Instrumentation.GenAi;

namespace qyl.copilot.Instrumentation;

/// <summary>
///     Records OTel 1.39 GenAI semantic convention attributes on spans.
///     Provides helpers for both standard gen_ai.* and qyl-specific attributes.
/// </summary>
public static class CopilotSpanRecorder
{
    private static readonly string[] FinishReasonStop = ["stop"];
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
    // Conversation & User Identity
    // =========================================================================

    /// <summary>
    ///     Records the conversation/thread ID on a span per OTel 1.39.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="conversationId">The conversation/session/thread identifier.</param>
    public static void RecordConversationId(Activity? activity, string? conversationId)
    {
        if (activity is null || string.IsNullOrEmpty(conversationId)) return;

        activity.SetTag(GenAiAttributes.ConversationId, conversationId);
    }

    /// <summary>
    ///     Records the end user identity on a span per OTel semantic conventions.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="userId">The user identifier (e.g., GitHub username).</param>
    public static void RecordEndUserId(Activity? activity, string? userId)
    {
        if (activity is null || string.IsNullOrEmpty(userId)) return;

        activity.SetTag(EnduserIdAttributes.Id, userId);
    }

    // =========================================================================
    // Response Attributes
    // =========================================================================

    /// <summary>
    ///     Records the response model on a span per OTel 1.39.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="responseModel">The model that generated the response.</param>
    public static void RecordResponseModel(Activity? activity, string? responseModel)
    {
        if (activity is null || string.IsNullOrEmpty(responseModel)) return;

        activity.SetTag(GenAiAttributes.ResponseModel, responseModel);
    }

    /// <summary>
    ///     Records the finish reasons on a span per OTel 1.39.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="finishReasons">Array of reasons the model stopped generating tokens.</param>
    public static void RecordFinishReasons(Activity? activity, string[] finishReasons)
    {
        if (activity is null || finishReasons.Length is 0) return;

        activity.SetTag(GenAiAttributes.ResponseFinishReasons, finishReasons);
    }

    // =========================================================================
    // Data Source
    // =========================================================================

    /// <summary>
    ///     Records the data source ID on a span per OTel 1.39.
    ///     Used when workflows reference knowledge sources.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="dataSourceId">The data source identifier.</param>
    public static void RecordDataSourceId(Activity? activity, string? dataSourceId)
    {
        if (activity is null || string.IsNullOrEmpty(dataSourceId)) return;

        activity.SetTag(GenAiDataSourceAttributes.Id, dataSourceId);
    }

    // =========================================================================
    // Error Recording
    // =========================================================================

    /// <summary>
    ///     Records an error on a span per OTel conventions.
    ///     Sets both error.type and exception details.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    /// <param name="exception">The exception that occurred.</param>
    public static void RecordError(Activity? activity, Exception exception)
    {
        if (activity is not null)
        {
            // Set error.type per GenAI semconv (categorized error type)
            var errorType = exception switch
            {
                OperationCanceledException => "cancelled",
                TimeoutException => "timeout",
                HttpRequestException { StatusCode: { } code } => ((int)code).ToString(),
                _ => exception.GetType().Name
            };
            activity.SetTag(GenAiAttributes.ErrorType, errorType);
        }

        GenAiInstrumentation.RecordException(activity, exception);
    }

    /// <summary>
    ///     Marks a span as successful with standard finish reason.
    /// </summary>
    /// <param name="activity">The activity to record on.</param>
    public static void RecordSuccess(Activity? activity)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Ok);
        activity.SetTag(GenAiAttributes.ResponseFinishReasons, FinishReasonStop);
    }
}
