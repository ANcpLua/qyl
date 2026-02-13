// =============================================================================
// qyl.instrumentation.generators - Response Attribute Extractor
// Runtime extraction of GenAI response attributes
// Owner: qyl.instrumentation.generators
// =============================================================================

namespace qyl.instrumentation.generators.Extractors;

/// <summary>
///     Generates code for extracting response attributes at runtime.
///     This is emitted into the interceptor code to pull usage/model info from responses.
/// </summary>
internal static class ResponseExtractorSource
{
    /// <summary>
    ///     Source code for the ExtractResponseAttributes method.
    ///     Uses duck typing to handle different SDK response types.
    /// </summary>
    public const string Source = """

                                     /// <summary>
                                     /// Extracts response attributes using duck typing.
                                     /// Works with OpenAI, Anthropic, Ollama, and Azure AI response types.
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

                                         // Try to extract output modality hints
                                         TrySetTag(activity, type, response, "OutputType", GenAiAttributes.OutputType);
                                         TrySetTag(activity, type, response, "ResponseFormat", GenAiAttributes.OutputType);

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

                                         // Try to extract finish reasons as semconv string[]
                                         TrySetFinishReasons(activity, type, response);
                                     }

                                     private static void TrySetRequestChoiceCount(
                                         Activity? activity,
                                         params (string Name, object? Value)[] arguments)
                                     {
                                         if (activity is null || arguments.Length is 0)
                                         {
                                             return;
                                         }

                                         foreach (var (name, value) in arguments)
                                         {
                                             if (value is null)
                                             {
                                                 continue;
                                             }

                                             if (IsChoiceCountName(name) && TryReadPositiveLong(value, out var count))
                                             {
                                                 activity.SetTag(GenAiAttributes.RequestChoiceCount, count);
                                                 return;
                                             }

                                             if (TryExtractChoiceCountFromObject(value, out count))
                                             {
                                                 activity.SetTag(GenAiAttributes.RequestChoiceCount, count);
                                                 return;
                                             }
                                         }
                                     }

                                     private static bool TryExtractChoiceCountFromObject(object value, out long count)
                                     {
                                         count = 0;

                                         var type = value.GetType();
                                         if (type.IsPrimitive || value is string)
                                         {
                                             return false;
                                         }

                                         foreach (var propertyName in new[] { "N", "ChoiceCount", "Choices", "CandidateCount", "NumChoices" })
                                         {
                                             var prop = type.GetProperty(propertyName);
                                             if (prop?.GetValue(value) is { } propValue && TryReadPositiveLong(propValue, out count))
                                             {
                                                 return true;
                                             }
                                         }

                                         return false;
                                     }

                                     private static bool IsChoiceCountName(string name)
                                     {
                                         return string.Equals(name, "n", StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(name, "choiceCount", StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(name, "choices", StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(name, "candidateCount", StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(name, "numChoices", StringComparison.OrdinalIgnoreCase);
                                     }

                                     private static bool TryReadPositiveLong(object value, out long number)
                                     {
                                         switch (value)
                                         {
                                             case int intValue when intValue > 0:
                                                 number = intValue;
                                                 return true;
                                             case long longValue when longValue > 0:
                                                 number = longValue;
                                                 return true;
                                             case short shortValue when shortValue > 0:
                                                 number = shortValue;
                                                 return true;
                                             case byte byteValue when byteValue > 0:
                                                 number = byteValue;
                                                 return true;
                                             default:
                                                 number = 0;
                                                 return false;
                                         }
                                     }

                                     private static void TrySetFinishReasons<T>(Activity activity, Type type, T obj)
                                     {
                                         if (TryExtractFinishReasons(type, obj) is { Length: > 0 } reasons)
                                         {
                                             activity.SetTag(GenAiAttributes.ResponseFinishReasons, reasons);
                                         }
                                     }

                                     private static string[]? TryExtractFinishReasons<T>(Type type, T obj)
                                     {
                                         var reasons = new List<string>();

                                         if (TryGetString(type, obj, "FinishReason") is { Length: > 0 } finishReason)
                                         {
                                             reasons.Add(finishReason);
                                         }

                                         if (TryGetString(type, obj, "StopReason") is { Length: > 0 } stopReason)
                                         {
                                             reasons.Add(stopReason);
                                         }

                                         if (type.GetProperty("FinishReasons")?.GetValue(obj) is global::System.Collections.IEnumerable finishReasons)
                                         {
                                             foreach (var item in finishReasons)
                                             {
                                                 if (item?.ToString() is { Length: > 0 } value)
                                                 {
                                                     reasons.Add(value);
                                                 }
                                             }
                                         }

                                         if (type.GetProperty("StopReasons")?.GetValue(obj) is global::System.Collections.IEnumerable stopReasons)
                                         {
                                             foreach (var item in stopReasons)
                                             {
                                                 if (item?.ToString() is { Length: > 0 } value)
                                                 {
                                                     reasons.Add(value);
                                                 }
                                             }
                                         }

                                         if (type.GetProperty("Choices")?.GetValue(obj) is global::System.Collections.IEnumerable choices)
                                         {
                                             foreach (var choice in choices)
                                             {
                                                 if (choice is null)
                                                 {
                                                     continue;
                                                 }

                                                 var choiceType = choice.GetType();
                                                 if (TryGetString(choiceType, choice, "FinishReason") is { Length: > 0 } choiceFinishReason)
                                                 {
                                                     reasons.Add(choiceFinishReason);
                                                 }

                                                 if (TryGetString(choiceType, choice, "StopReason") is { Length: > 0 } choiceStopReason)
                                                 {
                                                     reasons.Add(choiceStopReason);
                                                 }
                                             }
                                         }

                                         return reasons.Count is 0
                                             ? null
                                             : reasons.Where(static r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal).ToArray();
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

                                     private static string? TryGetString<T>(Type type, T obj, string propName)
                                     {
                                         var prop = type.GetProperty(propName);
                                         return prop?.GetValue(obj)?.ToString();
                                     }
                                 """;
}
