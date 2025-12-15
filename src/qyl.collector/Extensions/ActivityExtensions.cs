// =============================================================================
// C# 14 Extension Members for Activity Enrichment
// =============================================================================

namespace qyl.collector.Extensions;

/// <summary>
///     Extension members for <see cref="Activity" /> to simplify GenAI span creation and enrichment.
/// </summary>
public static class ActivityExtensions
{
    extension(Activity activity)
    {
        /// <summary>Returns true if the activity has error status.</summary>
        public bool IsError => activity.Status == ActivityStatusCode.Error;

        /// <summary>Returns true if the activity has OK status.</summary>
        public bool IsOk => activity.Status == ActivityStatusCode.Ok;

        /// <summary>Gets the duration in milliseconds.</summary>
        public double DurationMs => activity.Duration.TotalMilliseconds;

        /// <summary>Returns true if this is a root span (no parent).</summary>
        public bool IsRootSpan => activity.Parent is null && activity.ParentId is null;

        /// <summary>Gets the GenAI provider name if present.</summary>
        public string? ProviderName =>
            activity.GetTagItem(GenAiAttributes.ProviderName) as string;

        /// <summary>Gets the GenAI request model if present.</summary>
        public string? RequestModel =>
            activity.GetTagItem(GenAiAttributes.RequestModel) as string;

        /// <summary>Gets the input token count.</summary>
        public long InputTokens =>
            activity.GetTagItem(GenAiAttributes.UsageInputTokens) as long? ?? 0;

        /// <summary>Gets the output token count.</summary>
        public long OutputTokens =>
            activity.GetTagItem(GenAiAttributes.UsageOutputTokens) as long? ?? 0;

        /// <summary>Gets the total token count (input + output).</summary>
        public long TotalTokens =>
            (activity.GetTagItem(GenAiAttributes.UsageInputTokens) as long? ?? 0) +
            (activity.GetTagItem(GenAiAttributes.UsageOutputTokens) as long? ?? 0);

        /// <summary>Returns true if this is a GenAI span.</summary>
        public bool IsGenAiSpan =>
            activity.GetTagItem(GenAiAttributes.ProviderName) is not null ||
            activity.GetTagItem(GenAiAttributes.RequestModel) is not null;

        /// <summary>Sets the GenAI provider name.</summary>
        public Activity SetGenAiProvider(string provider)
        {
            activity.SetTag(GenAiAttributes.ProviderName, provider);
            return activity;
        }

        /// <summary>Sets the GenAI request model.</summary>
        public Activity SetGenAiModel(string model)
        {
            activity.SetTag(GenAiAttributes.RequestModel, model);
            return activity;
        }

        /// <summary>Sets the GenAI operation name.</summary>
        public Activity SetGenAiOperation(string operation)
        {
            activity.SetTag(GenAiAttributes.OperationName, operation);
            return activity;
        }

        /// <summary>Sets input and output token counts.</summary>
        public Activity SetGenAiTokens(long input, long output)
        {
            activity.SetTag(GenAiAttributes.UsageInputTokens, input);
            activity.SetTag(GenAiAttributes.UsageOutputTokens, output);
            return activity;
        }

        /// <summary>Records an exception on the activity.</summary>
        public Activity RecordException(Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.SetTag("exception.type", ex.GetType().FullName);
            activity.SetTag("exception.message", ex.Message);
            activity.SetTag("exception.stacktrace", ex.StackTrace);
            return activity;
        }

        /// <summary>Sets the session ID tag.</summary>
        public Activity SetSession(string sessionId)
        {
            activity.SetTag("session.id", sessionId);
            return activity;
        }
    }

    extension(ActivitySource source)
    {
        /// <summary>
        ///     Starts a new GenAI span with provider and model pre-configured.
        /// </summary>
        public Activity? StartGenAiSpan(
            string operationName,
            string provider,
            string model,
            ActivityKind kind = ActivityKind.Client)
        {
            var activity = source.StartActivity(operationName, kind);
            if (activity is null)
                return null;

            activity.SetGenAiProvider(provider);
            activity.SetGenAiModel(model);
            activity.SetGenAiOperation(operationName);
            return activity;
        }
    }
}
