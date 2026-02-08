// =============================================================================
// qyl Codex Telemetry Mapper - Transforms Codex custom events to OTel GenAI
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.39.0
// =============================================================================
//
// Codex (OpenAI's CLI) emits custom codex.* prefixed telemetry.
// This mapper transforms Codex events to standard OTel GenAI semantic conventions.
//
// Codex Event Types:
// - codex.conversation_starts: Session/conversation initialization
// - codex.api_request: API call attempts with status/duration
// - codex.sse_event: Streaming events with token counts on response.completed
// - codex.user_prompt: User input (content redacted unless enabled)
// - codex.tool_decision: Tool approval/denial decisions
// - codex.tool_result: Tool execution results
//
// Target GenAI Semconv (OTel 1.39):
// - gen_ai.provider.name = "openai"
// - gen_ai.operation.name = derived from event type
// - gen_ai.request.model / gen_ai.response.model
// - gen_ai.usage.input_tokens / gen_ai.usage.output_tokens
// - gen_ai.conversation.id
// - gen_ai.response.finish_reasons
// - gen_ai.tool.name / gen_ai.tool.call.id
// =============================================================================

namespace qyl.collector.Ingestion;

/// <summary>
///     Transforms Codex custom telemetry events to OTel GenAI semantic conventions.
///     Integrated as a preprocessing step in the OTLP ingestion pipeline.
/// </summary>
public static class CodexTelemetryMapper
{
    // =========================================================================
    // Codex Event Name Constants (codex.* prefix)
    // =========================================================================

    private const string CodexPrefix = "codex.";
    private const string ConversationStarts = "codex.conversation_starts";
    private const string ApiRequest = "codex.api_request";
    private const string SseEvent = "codex.sse_event";
    private const string UserPrompt = "codex.user_prompt";
    private const string ToolDecision = "codex.tool_decision";
    private const string ToolResult = "codex.tool_result";

    // =========================================================================
    // Codex Attribute Keys (source attributes)
    // Used attributes are mapped to GenAI semconv, others preserved for context
    // =========================================================================

    private const string CodexModel = "codex.model";
    private const string CodexConversationId = "codex.conversation_id";
    private const string CodexThreadId = "codex.thread_id";
    private const string CodexSuccess = "codex.success";
    private const string CodexErrorType = "codex.error_type";
    private const string CodexErrorMessage = "codex.error_message";
    private const string CodexInputTokens = "codex.input_tokens";
    private const string CodexOutputTokens = "codex.output_tokens";
    private const string CodexToolName = "codex.tool_name";
    private const string CodexToolOutput = "codex.tool_output";
    private const string CodexFinishReason = "codex.finish_reason";

    // Future expansion: These Codex attributes are preserved in AttributesJson
    // but not currently mapped to GenAI semconv:
    // - codex.reasoning_enabled, codex.reasoning_effort (reasoning config)
    // - codex.approval_policy, codex.sandbox_policy (security config)
    // - codex.attempt, codex.status, codex.duration_ms (request lifecycle)
    // - codex.event_kind (SSE event classification)
    // - codex.total_tokens, codex.prompt_length (additional metrics)
    // - codex.tool_approved, codex.tool_denied (approval decisions)
    // - codex.tool_duration_ms, codex.tool_success (tool execution metrics)

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    ///     Determines if a span contains Codex telemetry that should be transformed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCodexSpan(string? spanName)
    {
        return spanName is not null &&
               spanName.StartsWith(CodexPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Determines if attributes contain Codex telemetry.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasCodexAttributes(IDictionary<string, string> attributes)
    {
        return attributes.ContainsKey(CodexModel) ||
               attributes.ContainsKey(CodexConversationId) ||
               attributes.ContainsKey(CodexThreadId);
    }

    /// <summary>
    ///     Transforms Codex attributes to OTel GenAI semantic conventions.
    ///     Mutates the attributes dictionary in-place for efficiency.
    /// </summary>
    /// <returns>True if transformation was applied, false otherwise.</returns>
    public static bool TransformAttributes(string? spanName, IDictionary<string, string> attributes)
    {
        if (!IsCodexSpan(spanName) && !HasCodexAttributes(attributes))
            return false;

        var transformed = false;

        // Always set provider to OpenAI for Codex telemetry
        if (!attributes.ContainsKey(GenAiAttributes.ProviderName))
        {
            attributes[GenAiAttributes.ProviderName] = GenAiAttributes.Providers.OpenAi;
            transformed = true;
        }

        // Map operation name from span name
        transformed |= MapOperationName(spanName, attributes);

        // Map model
        transformed |= MapModel(attributes);

        // Map conversation/thread ID
        transformed |= MapConversationId(attributes);

        // Map token usage
        transformed |= MapTokenUsage(attributes);

        // Map finish reasons
        transformed |= MapFinishReasons(attributes);

        // Map tool attributes
        transformed |= MapToolAttributes(attributes);

        // Map error attributes
        transformed |= MapErrorAttributes(attributes);

        return transformed;
    }

    /// <summary>
    ///     Transforms a SpanStorageRow with Codex telemetry to use GenAI attributes.
    ///     Creates a new row with transformed attributes.
    /// </summary>
    public static SpanStorageRow TransformSpan(SpanStorageRow span)
    {
        // Parse existing attributes
        var attributes = ParseAttributes(span.AttributesJson);
        if (attributes is null)
            return span;

        // Apply transformations
        if (!TransformAttributes(span.Name, attributes))
            return span;

        // Extract promoted GenAI fields from transformed attributes
        var genAi = ExtractGenAiFields(attributes);

        // Serialize updated attributes
        var newAttributesJson = JsonSerializer.Serialize(
            attributes,
            QylSerializerContext.Default.DictionaryStringString);

        // Create new span with updated fields
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

    /// <summary>
    ///     Transforms a batch of spans, applying Codex transformations where applicable.
    /// </summary>
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

    // =========================================================================
    // Mapping Helpers
    // =========================================================================

    private static bool MapOperationName(string? spanName, IDictionary<string, string> attributes)
    {
        if (spanName is null || attributes.ContainsKey(GenAiAttributes.OperationName))
            return false;

        var operation = spanName switch
        {
            ConversationStarts => GenAiAttributes.Operations.Chat,
            ApiRequest => GenAiAttributes.Operations.Chat,
            SseEvent => GenAiAttributes.Operations.Chat,
            UserPrompt => GenAiAttributes.Operations.Chat,
            ToolDecision => GenAiAttributes.Operations.ExecuteTool,
            ToolResult => GenAiAttributes.Operations.ExecuteTool,
            _ when spanName.StartsWith(CodexPrefix, StringComparison.Ordinal) => GenAiAttributes.Operations.Chat,
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

        var transformed = false;

        if (!attributes.ContainsKey(GenAiAttributes.RequestModel))
        {
            attributes[GenAiAttributes.RequestModel] = model;
            transformed = true;
        }

        // For completed requests, also set response model
        if (attributes.TryGetValue(CodexSuccess, out var success) &&
            success.Equals("true", StringComparison.OrdinalIgnoreCase) &&
            !attributes.ContainsKey(GenAiAttributes.ResponseModel))
        {
            attributes[GenAiAttributes.ResponseModel] = model;
            transformed = true;
        }

        return transformed;
    }

    private static bool MapConversationId(IDictionary<string, string> attributes)
    {
        // Try conversation_id first, then thread_id
        _ = attributes.TryGetValue(CodexConversationId, out var conversationId);
        conversationId ??= attributes.TryGetValue(CodexThreadId, out var threadId) ? threadId : null;

        if (conversationId is null || attributes.ContainsKey(GenAiAttributes.ConversationId))
            return false;

        attributes[GenAiAttributes.ConversationId] = conversationId;
        return true;
    }

    private static bool MapTokenUsage(IDictionary<string, string> attributes)
    {
        var transformed = false;

        // Map input tokens
        if (attributes.TryGetValue(CodexInputTokens, out var inputTokens) &&
            !attributes.ContainsKey(GenAiAttributes.UsageInputTokens))
        {
            attributes[GenAiAttributes.UsageInputTokens] = inputTokens;
            transformed = true;
        }

        // Map output tokens
        if (attributes.TryGetValue(CodexOutputTokens, out var outputTokens) &&
            !attributes.ContainsKey(GenAiAttributes.UsageOutputTokens))
        {
            attributes[GenAiAttributes.UsageOutputTokens] = outputTokens;
            transformed = true;
        }

        return transformed;
    }

    private static bool MapFinishReasons(IDictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue(CodexFinishReason, out var finishReason))
            return false;

        if (attributes.ContainsKey(GenAiAttributes.ResponseFinishReasons))
            return false;

        // OTel expects an array format for finish_reasons
        // Serialize as JSON array for consistency
        var reasonsArray = JsonSerializer.Serialize(
            [finishReason],
            QylSerializerContext.Default.StringArray);
        attributes[GenAiAttributes.ResponseFinishReasons] = reasonsArray;

        return true;
    }

    private static bool MapToolAttributes(IDictionary<string, string> attributes)
    {
        var transformed = false;

        // Map tool name
        if (attributes.TryGetValue(CodexToolName, out var toolName) &&
            !attributes.ContainsKey(GenAiAttributes.ToolName))
        {
            attributes[GenAiAttributes.ToolName] = toolName;
            transformed = true;
        }

        // Map tool type (Codex tools are function-based)
        if (toolName is not null && !attributes.ContainsKey(GenAiAttributes.ToolType))
        {
            attributes[GenAiAttributes.ToolType] = GenAiAttributes.ToolTypes.Function;
            transformed = true;
        }

        // Map tool result to tool.call.result
        if (attributes.TryGetValue(CodexToolOutput, out var toolOutput) &&
            !attributes.ContainsKey(GenAiAttributes.ToolCallResult))
        {
            attributes[GenAiAttributes.ToolCallResult] = toolOutput;
            transformed = true;
        }

        return transformed;
    }

    private static bool MapErrorAttributes(IDictionary<string, string> attributes)
    {
        var transformed = false;

        // Map error type
        if (attributes.TryGetValue(CodexErrorType, out var errorType) &&
            !attributes.ContainsKey(GenAiAttributes.ErrorType))
        {
            attributes[GenAiAttributes.ErrorType] = errorType;
            transformed = true;
        }

        // Map error message to exception.message
        if (attributes.TryGetValue(CodexErrorMessage, out var errorMessage) &&
            !attributes.ContainsKey(GenAiAttributes.ExceptionMessage))
        {
            attributes[GenAiAttributes.ExceptionMessage] = errorMessage;
            transformed = true;
        }

        return transformed;
    }

    // =========================================================================
    // Extraction Helpers
    // =========================================================================

    private static Dictionary<string, string>? ParseAttributes(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize(
                attributesJson,
                QylSerializerContext.Default.DictionaryStringString);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasCodexAttributesFromJson(string? attributesJson)
    {
        if (string.IsNullOrEmpty(attributesJson))
            return false;

        // Quick string check before parsing
        return attributesJson.Contains("codex.", StringComparison.Ordinal);
    }

    private readonly record struct GenAiFields(
        string? ProviderName,
        string? RequestModel,
        string? ResponseModel,
        long? InputTokens,
        long? OutputTokens,
        string? StopReason,
        string? ToolName,
        string? ToolCallId);

    private static GenAiFields ExtractGenAiFields(IReadOnlyDictionary<string, string> attributes)
    {
        return new GenAiFields(
            ProviderName: attributes.GetValueOrDefault(GenAiAttributes.ProviderName),
            RequestModel: attributes.GetValueOrDefault(GenAiAttributes.RequestModel),
            ResponseModel: attributes.GetValueOrDefault(GenAiAttributes.ResponseModel),
            InputTokens: ParseNullableLong(attributes.GetValueOrDefault(GenAiAttributes.UsageInputTokens)),
            OutputTokens: ParseNullableLong(attributes.GetValueOrDefault(GenAiAttributes.UsageOutputTokens)),
            StopReason: attributes.GetValueOrDefault(GenAiAttributes.ResponseFinishReasons),
            ToolName: attributes.GetValueOrDefault(GenAiAttributes.ToolName),
            ToolCallId: attributes.GetValueOrDefault(GenAiAttributes.ToolCallId)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long? ParseNullableLong(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        return long.TryParse(value, out var result) ? result : null;
    }
}

/// <summary>
///     Extension methods for integrating Codex transformation into OTLP pipeline.
/// </summary>
public static class CodexTelemetryExtensions
{
    /// <summary>
    ///     Applies Codex transformations to a span batch before storage.
    ///     Call this in the OTLP ingestion pipeline.
    /// </summary>
    public static SpanBatch WithCodexTransformations(this SpanBatch batch)
    {
        // Quick check: if no spans need transformation, return original
        var needsTransform = false;
        foreach (var span in batch.Spans)
        {
            if (CodexTelemetryMapper.IsCodexSpan(span.Name))
            {
                needsTransform = true;
                break;
            }
        }

        if (!needsTransform)
            return batch;

        var transformed = CodexTelemetryMapper.TransformBatch(batch.Spans);
        return new SpanBatch(transformed);
    }
}
