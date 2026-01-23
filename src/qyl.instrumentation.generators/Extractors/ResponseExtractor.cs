// =============================================================================
// qyl.instrumentation.generators - Response Attribute Extractor
// Runtime extraction of GenAI response attributes
// Owner: qyl.instrumentation.generators
// =============================================================================

namespace qyl.instrumentation.generators.Extractors;

/// <summary>
/// Generates code for extracting response attributes at runtime.
/// This is emitted into the interceptor code to pull usage/model info from responses.
/// </summary>
internal static class ResponseExtractorSource
{
    /// <summary>
    /// Source code for the ExtractResponseAttributes method.
    /// Uses duck typing to handle different SDK response types.
    /// </summary>
    public const string Source = """

            /// <summary>
            /// Extracts response attributes using duck typing.
            /// Works with OpenAI, Anthropic, Ollama response types.
            /// </summary>
            private static void ExtractResponseAttributes<T>(Activity? activity, T response)
            {
                if (activity is null || response is null) return;

                var type = response.GetType();

                // Try to extract gen_ai.response.id
                TrySetTag(activity, type, response, "Id", GenAiAttributes.ResponseId);
                TrySetTag(activity, type, response, "ResponseId", GenAiAttributes.ResponseId);

                // Try to extract gen_ai.response.model
                TrySetTag(activity, type, response, "Model", GenAiAttributes.ResponseModel);
                TrySetTag(activity, type, response, "ModelId", GenAiAttributes.ResponseModel);

                // Try to extract usage (nested object)
                var usageProp = type.GetProperty("Usage");
                if (usageProp?.GetValue(response) is { } usage)
                {
                    var usageType = usage.GetType();
                    TrySetTagInt(activity, usageType, usage, "InputTokens", GenAiAttributes.UsageInputTokens);
                    TrySetTagInt(activity, usageType, usage, "OutputTokens", GenAiAttributes.UsageOutputTokens);
                    // OpenAI naming (maps to same attributes)
                    TrySetTagInt(activity, usageType, usage, "PromptTokens", GenAiAttributes.UsageInputTokens);
                    TrySetTagInt(activity, usageType, usage, "CompletionTokens", GenAiAttributes.UsageOutputTokens);
                }

                // Try to extract finish reason
                TrySetTag(activity, type, response, "FinishReason", GenAiAttributes.ResponseFinishReasons);
                TrySetTag(activity, type, response, "StopReason", GenAiAttributes.ResponseFinishReasons);
            }

            private static void TrySetTag<T>(Activity activity, Type type, T obj, string propName, string tagName)
            {
                var prop = type.GetProperty(propName);
                if (prop?.GetValue(obj) is { } value)
                {
                    activity.SetTag(tagName, value.ToString());
                }
            }

            private static void TrySetTagInt<T>(Activity activity, Type type, T obj, string propName, string tagName)
            {
                var prop = type.GetProperty(propName);
                if (prop?.GetValue(obj) is int value)
                {
                    activity.SetTag(tagName, value);
                }
                else if (prop?.GetValue(obj) is long longValue)
                {
                    activity.SetTag(tagName, longValue);
                }
            }
        """;
}
