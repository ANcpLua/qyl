
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Collector.Ingestion;

public static class CodexTelemetryMapper
{

    private const string CodexPrefix = "codex.";
    private const string ConversationStarts = "codex.conversation_starts";
    private const string ApiRequest = "codex.api_request";
    private const string SseEvent = "codex.sse_event";
    private const string UserPrompt = "codex.user_prompt";
    private const string ToolDecision = "codex.tool_decision";
    private const string ToolResult = "codex.tool_result";


    private const string CodexModel = "codex.model";
    private const string CodexConversationId = "codex.conversation_id";
    private const string CodexThreadId = "codex.thread_id";
    private const string CodexSuccess = "codex.success";
    private const string CodexErrorType = "codex.error_type";
    private const string CodexInputTokens = "codex.input_tokens";
    private const string CodexOutputTokens = "codex.output_tokens";
    private const string CodexToolName = "codex.tool_name";
    private const string CodexFinishReason = "codex.finish_reason";



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCodexSpan(string? spanName) =>
        spanName is not null &&
        spanName.StartsWithOrdinal(CodexPrefix);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasCodexAttributes(IDictionary<string, string> attributes) =>
        attributes.ContainsKey(CodexModel) ||
        attributes.ContainsKey(CodexConversationId) ||
        attributes.ContainsKey(CodexThreadId);

    public static bool TransformAttributes(string? spanName, IDictionary<string, string> attributes)
    {
        if (!IsCodexSpan(spanName) && !HasCodexAttributes(attributes))
            return false;

        var transformed = false;

        transformed |= attributes.TryAdd(GenAiAttributes.ProviderName, GenAiAttributes.ProviderNameValues.Openai);

        transformed |= MapOperationName(spanName, attributes);

        transformed |= MapModel(attributes);

        transformed |= MapTokenUsage(attributes);

        transformed |= MapFinishReasons(attributes);

        transformed |= MapToolAttributes(attributes);

        transformed |= MapErrorAttributes(attributes);

        return transformed;
    }

    public static SpanStorageRow TransformSpan(SpanStorageRow span)
    {
        if (ParseAttributes(span.AttributesJson) is not { } attributes)
            return span;

        if (!TransformAttributes(span.Name, attributes))
            return span;

        var genAi = ExtractGenAiFields(attributes);

        var newAttributesJson = JsonSerializer.Serialize(
            attributes,
            QylSerializerContext.Default.DictionaryStringString);

        return span with
        {
            GenAiProviderName = genAi.ProviderName ?? span.GenAiProviderName,
            GenAiRequestModel = genAi.RequestModel ?? span.GenAiRequestModel,
            GenAiResponseModel = genAi.ResponseModel ?? span.GenAiResponseModel,
            GenAiInputTokens = genAi.InputTokens ?? span.GenAiInputTokens,
            GenAiOutputTokens = genAi.OutputTokens ?? span.GenAiOutputTokens,
            GenAiStopReason = genAi.StopReason ?? span.GenAiStopReason,
            GenAiToolName = genAi.ToolName ?? span.GenAiToolName,
            GenAiToolCallId = genAi.ToolCallId ?? span.GenAiToolCallId,
            AttributesJson = newAttributesJson
        };
    }

    public static List<SpanStorageRow> TransformBatch(IReadOnlyList<SpanStorageRow> spans)
    {
        var result = new List<SpanStorageRow>(spans.Count);

        foreach (var span in spans)
        {
            if (IsCodexSpan(span.Name) || HasCodexAttributesFromJson(span.AttributesJson))
            {
                result.Add(TransformSpan(span));
            }
            else
            {
                result.Add(span);
            }
        }

        return result;
    }


    private static bool MapOperationName(string? spanName, IDictionary<string, string> attributes)
    {
        if (spanName is null || attributes.ContainsKey(GenAiAttributes.OperationName))
            return false;

        var operation = spanName switch
        {
            ConversationStarts => GenAiAttributes.OperationNameValues.Chat,
            ApiRequest => GenAiAttributes.OperationNameValues.Chat,
            SseEvent => GenAiAttributes.OperationNameValues.Chat,
            UserPrompt => GenAiAttributes.OperationNameValues.Chat,
            ToolDecision => GenAiAttributes.OperationNameValues.ExecuteTool,
            ToolResult => GenAiAttributes.OperationNameValues.ExecuteTool,
            _ when spanName.StartsWithOrdinal(CodexPrefix) => GenAiAttributes.OperationNameValues.Chat,
            _ => null
        };

        if (operation is not null)
        {
            attributes[GenAiAttributes.OperationName] = operation;
            return true;
        }

        return false;
    }

    private static bool MapModel(IDictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue(CodexModel, out var model))
            return false;

        var addedRequest = attributes.TryAdd(GenAiAttributes.RequestModel, model);
        var addedResponse = attributes.TryGetValue(CodexSuccess, out var success)
            && success.EqualsIgnoreCase("true")
            && attributes.TryAdd(GenAiAttributes.ResponseModel, model);

        return addedRequest || addedResponse;
    }

    private static bool MapTokenUsage(IDictionary<string, string> attributes)
    {
        var transformed = false;

        transformed |= attributes.TryGetValue(CodexInputTokens, out var inputTokens) &&
                       attributes.TryAdd(GenAiAttributes.UsageInputTokens, inputTokens);

        transformed |= attributes.TryGetValue(CodexOutputTokens, out var outputTokens) &&
                       attributes.TryAdd(GenAiAttributes.UsageOutputTokens, outputTokens);

        return transformed;
    }

    private static bool MapFinishReasons(IDictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue(CodexFinishReason, out var finishReason))
            return false;

        if (attributes.ContainsKey(GenAiAttributes.ResponseFinishReasons))
            return false;

        var reasonsArray = JsonSerializer.Serialize(
            [finishReason],
            QylSerializerContext.Default.StringArray);
        attributes[GenAiAttributes.ResponseFinishReasons] = reasonsArray;

        return true;
    }

    private static bool MapToolAttributes(IDictionary<string, string> attributes)
    {
        var transformed = false;

        transformed |= attributes.TryGetValue(CodexToolName, out var toolName) &&
                       attributes.TryAdd(GenAiAttributes.ToolName, toolName);

        return transformed;
    }

    private static bool MapErrorAttributes(IDictionary<string, string> attributes)
    {
        var transformed = false;

        transformed |= attributes.TryGetValue(CodexErrorType, out var errorType) &&
                       attributes.TryAdd(ErrorAttributes.Type, errorType);

        return transformed;
    }


    private static Dictionary<string, string>? ParseAttributes(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return null;

        var result = ErrorExtractor.ParseAttributesJson(attributesJson);
        return result.Count > 0 ? result : null;
    }

    private static bool HasCodexAttributesFromJson(string? attributesJson) =>
        !string.IsNullOrEmpty(attributesJson) &&
        attributesJson.ContainsOrdinal("codex.");

    private static GenAiFields ExtractGenAiFields(IReadOnlyDictionary<string, string> attributes) =>
        new(
            attributes.GetValueOrDefault(GenAiAttributes.ProviderName),
            attributes.GetValueOrDefault(GenAiAttributes.RequestModel),
            attributes.GetValueOrDefault(GenAiAttributes.ResponseModel),
            ParseNullableLong(attributes.GetValueOrDefault(GenAiAttributes.UsageInputTokens)),
            ParseNullableLong(attributes.GetValueOrDefault(GenAiAttributes.UsageOutputTokens)),
            attributes.GetValueOrDefault(GenAiAttributes.ResponseFinishReasons),
            attributes.GetValueOrDefault(GenAiAttributes.ToolName),
            attributes.GetValueOrDefault(GenAiAttributes.ToolCallId)
        );

    private static long? ParseNullableLong(string? value) =>
        AttributeParsing.ParseNullableLong(value);

    private readonly record struct GenAiFields(
        string? ProviderName,
        string? RequestModel,
        string? ResponseModel,
        long? InputTokens,
        long? OutputTokens,
        string? StopReason,
        string? ToolName,
        string? ToolCallId);
}

public static class CodexTelemetryExtensions
{
    public static SpanBatch WithCodexTransformations(this SpanBatch batch)
    {
        var needsTransform = batch.Spans.Any(static span => CodexTelemetryMapper.IsCodexSpan(span.Name));

        if (!needsTransform)
            return batch;

        var transformed = CodexTelemetryMapper.TransformBatch(batch.Spans);
        return new SpanBatch(transformed);
    }
}
