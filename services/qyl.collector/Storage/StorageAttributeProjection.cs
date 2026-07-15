using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Collector.Storage;

internal static class StorageAttributeProjection
{
    private static readonly FrozenSet<string> s_genAiOperationNames = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        GenAiAttributes.OperationNameValues.Chat,
        GenAiAttributes.OperationNameValues.CreateAgent,
        GenAiAttributes.OperationNameValues.CreateMemory,
        GenAiAttributes.OperationNameValues.CreateMemoryStore,
        GenAiAttributes.OperationNameValues.DeleteMemory,
        GenAiAttributes.OperationNameValues.DeleteMemoryStore,
        GenAiAttributes.OperationNameValues.Embeddings,
        GenAiAttributes.OperationNameValues.ExecuteTool,
        GenAiAttributes.OperationNameValues.GenerateContent,
        GenAiAttributes.OperationNameValues.InvokeAgent,
        GenAiAttributes.OperationNameValues.InvokeWorkflow,
        GenAiAttributes.OperationNameValues.Plan,
        GenAiAttributes.OperationNameValues.Retrieval,
        GenAiAttributes.OperationNameValues.SearchMemory,
        GenAiAttributes.OperationNameValues.TextCompletion,
        GenAiAttributes.OperationNameValues.UpdateMemory,
        GenAiAttributes.OperationNameValues.UpsertMemory);

    private static readonly FrozenSet<string> s_genAiOutputTypes = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        GenAiAttributes.OutputTypeValues.Image,
        GenAiAttributes.OutputTypeValues.Json,
        GenAiAttributes.OutputTypeValues.Speech,
        GenAiAttributes.OutputTypeValues.Text);

    internal static SpanHotAttributeProjection ExtractSpanHotAttributes(
        IReadOnlyDictionary<string, OtlpAttributeValue> attributes) =>
        new(
            SessionId: attributes.GetFirstValueOrDefault(AttributeKeySets.SessionCorrelation),
            GenAiProviderName: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiProviderName),
            GenAiOperationName: Canonicalize(
                attributes.GetString(CollectorSemanticAttributeCatalog.GenAiOperationName),
                s_genAiOperationNames),
            GenAiOutputType: Canonicalize(
                attributes.GetString(CollectorSemanticAttributeCatalog.GenAiOutputType),
                s_genAiOutputTypes),
            GenAiRequestModel: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiRequestModel),
            GenAiResponseModel: attributes.GetString(CollectorSemanticAttributeCatalog.GenAiResponseModel),
            GenAiInputTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiInputTokens),
            GenAiOutputTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiOutputTokens),
            GenAiCacheReadInputTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiCacheReadInputTokens),
            GenAiCacheCreationInputTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiCacheCreationInputTokens),
            GenAiReasoningTokens: attributes.GetInt64(CollectorSemanticAttributeCatalog.GenAiReasoningTokens));

    private static string? GetString(this IReadOnlyDictionary<string, OtlpAttributeValue> attributes, string? key) =>
        key is null ? null : attributes.GetValueOrDefault(key)?.AsString();

    private static long? GetInt64(this IReadOnlyDictionary<string, OtlpAttributeValue> attributes, string? key) =>
        key is null ? null : attributes.GetValueOrDefault(key)?.AsInt64();

    private static string? Canonicalize(string? value, FrozenSet<string> allowedValues)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return allowedValues.TryGetValue(value.Trim(), out var canonicalValue)
            ? canonicalValue
            : null;
    }
}

internal readonly record struct SpanHotAttributeProjection(
    string? SessionId,
    string? GenAiProviderName,
    string? GenAiOperationName,
    string? GenAiOutputType,
    string? GenAiRequestModel,
    string? GenAiResponseModel,
    long? GenAiInputTokens,
    long? GenAiOutputTokens,
    long? GenAiCacheReadInputTokens,
    long? GenAiCacheCreationInputTokens,
    long? GenAiReasoningTokens);
