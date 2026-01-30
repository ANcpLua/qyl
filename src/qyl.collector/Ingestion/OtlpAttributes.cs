// =============================================================================
// qyl OTLP Ingestion - OTel GenAI Semantic Conventions v1.39.0
// https://opentelemetry.io/docs/specs/semconv/gen-ai/
// Target: .NET 10 / C# 14
// =============================================================================

#pragma warning disable AL0012 // Intentional deprecated attribute references for backward compatibility

namespace qyl.collector.Ingestion;

/// <summary>
///     OTel GenAI Semantic Conventions v1.39.0 - UTF-8 attribute keys for zero-allocation parsing.
///     Uses direct StartsWith checks for prefix matching (NOT SearchValues which is for substring).
/// </summary>
internal static class OtlpGenAiAttributes
{
    // =========================================================================
    // CORE ATTRIBUTES (Required/Conditionally Required)
    // =========================================================================

    /// <summary>gen_ai.operation.name - The operation being performed (REQUIRED)</summary>
    public static ReadOnlySpan<byte> OperationName => "gen_ai.operation.name"u8;

    /// <summary>gen_ai.provider.name - Provider identifier (REQUIRED)</summary>
    public static ReadOnlySpan<byte> ProviderName => "gen_ai.provider.name"u8;

    /// <summary>gen_ai.request.model - Requested model name</summary>
    public static ReadOnlySpan<byte> RequestModel => "gen_ai.request.model"u8;

    /// <summary>gen_ai.response.model - Actual model that responded</summary>
    public static ReadOnlySpan<byte> ResponseModel => "gen_ai.response.model"u8;

    /// <summary>gen_ai.response.id - Unique completion identifier</summary>
    public static ReadOnlySpan<byte> ResponseId => "gen_ai.response.id"u8;

    /// <summary>server.address - GenAI server address (Recommended)</summary>
    public static ReadOnlySpan<byte> ServerAddress => "server.address"u8;

    /// <summary>server.port - GenAI server port (Conditionally Required)</summary>
    public static ReadOnlySpan<byte> ServerPort => "server.port"u8;

    // =========================================================================
    // REQUEST PARAMETERS
    // =========================================================================

    /// <summary>gen_ai.request.max_tokens - Max tokens to generate</summary>
    public static ReadOnlySpan<byte> RequestMaxTokens => "gen_ai.request.max_tokens"u8;

    /// <summary>gen_ai.request.temperature - Temperature setting (0.0-2.0)</summary>
    public static ReadOnlySpan<byte> RequestTemperature => "gen_ai.request.temperature"u8;

    /// <summary>gen_ai.request.top_p - Nucleus sampling threshold</summary>
    public static ReadOnlySpan<byte> RequestTopP => "gen_ai.request.top_p"u8;

    /// <summary>gen_ai.request.top_k - Top-k sampling parameter</summary>
    public static ReadOnlySpan<byte> RequestTopK => "gen_ai.request.top_k"u8;

    /// <summary>gen_ai.request.frequency_penalty - Frequency penalty (-2.0 to 2.0)</summary>
    public static ReadOnlySpan<byte> RequestFrequencyPenalty => "gen_ai.request.frequency_penalty"u8;

    /// <summary>gen_ai.request.presence_penalty - Presence penalty (-2.0 to 2.0)</summary>
    public static ReadOnlySpan<byte> RequestPresencePenalty => "gen_ai.request.presence_penalty"u8;

    /// <summary>gen_ai.request.stop_sequences - Stop sequence array</summary>
    public static ReadOnlySpan<byte> RequestStopSequences => "gen_ai.request.stop_sequences"u8;

    /// <summary>gen_ai.request.seed - Reproducibility seed</summary>
    public static ReadOnlySpan<byte> RequestSeed => "gen_ai.request.seed"u8;

    /// <summary>gen_ai.request.choice.count - Number of completions to generate</summary>
    public static ReadOnlySpan<byte> RequestChoiceCount => "gen_ai.request.choice.count"u8;

    /// <summary>gen_ai.request.encoding_formats - Embedding encoding formats</summary>
    public static ReadOnlySpan<byte> RequestEncodingFormats => "gen_ai.request.encoding_formats"u8;

    // =========================================================================
    // RESPONSE ATTRIBUTES
    // =========================================================================

    /// <summary>gen_ai.response.finish_reasons - Array of stop reasons per choice</summary>
    public static ReadOnlySpan<byte> ResponseFinishReasons => "gen_ai.response.finish_reasons"u8;

    // =========================================================================
    // USAGE/TOKENS
    // =========================================================================

    /// <summary>gen_ai.usage.input_tokens - Input/prompt token count</summary>
    public static ReadOnlySpan<byte> InputTokens => "gen_ai.usage.input_tokens"u8;

    /// <summary>gen_ai.usage.output_tokens - Output/completion token count</summary>
    public static ReadOnlySpan<byte> OutputTokens => "gen_ai.usage.output_tokens"u8;

    // =========================================================================
    // OUTPUT TYPE
    // =========================================================================

    /// <summary>gen_ai.output.type - Output modality (text, json, image, speech)</summary>
    public static ReadOnlySpan<byte> OutputType => "gen_ai.output.type"u8;

    // =========================================================================
    // CONVERSATION/SESSION
    // =========================================================================

    /// <summary>gen_ai.conversation.id - Session/thread identifier</summary>
    public static ReadOnlySpan<byte> ConversationId => "gen_ai.conversation.id"u8;

    // =========================================================================
    // AGENT ATTRIBUTES (gen_ai.agent.*)
    // =========================================================================

    /// <summary>gen_ai.agent.id - Unique agent identifier</summary>
    public static ReadOnlySpan<byte> AgentId => "gen_ai.agent.id"u8;

    /// <summary>gen_ai.agent.name - Human-readable agent name</summary>
    public static ReadOnlySpan<byte> AgentName => "gen_ai.agent.name"u8;

    /// <summary>gen_ai.agent.description - Agent description</summary>
    public static ReadOnlySpan<byte> AgentDescription => "gen_ai.agent.description"u8;

    // =========================================================================
    // TOOL ATTRIBUTES (gen_ai.tool.*)
    // =========================================================================

    /// <summary>gen_ai.tool.name - Tool name</summary>
    public static ReadOnlySpan<byte> ToolName => "gen_ai.tool.name"u8;

    /// <summary>gen_ai.tool.call.id - Tool call identifier</summary>
    public static ReadOnlySpan<byte> ToolCallId => "gen_ai.tool.call.id"u8;

    /// <summary>gen_ai.tool.type - Tool type (function, extension, datastore)</summary>
    public static ReadOnlySpan<byte> ToolType => "gen_ai.tool.type"u8;

    /// <summary>gen_ai.tool.description - Tool description</summary>
    public static ReadOnlySpan<byte> ToolDescription => "gen_ai.tool.description"u8;

    /// <summary>gen_ai.tool.call.arguments - Tool call parameters (sensitive)</summary>
    public static ReadOnlySpan<byte> ToolCallArguments => "gen_ai.tool.call.arguments"u8;

    /// <summary>gen_ai.tool.call.result - Tool call result (sensitive)</summary>
    public static ReadOnlySpan<byte> ToolCallResult => "gen_ai.tool.call.result"u8;

    /// <summary>gen_ai.tool.definitions - Available tool definitions (large)</summary>
    public static ReadOnlySpan<byte> ToolDefinitions => "gen_ai.tool.definitions"u8;

    // =========================================================================
    // DATA SOURCE (RAG)
    // =========================================================================

    /// <summary>gen_ai.data_source.id - RAG data source identifier</summary>
    public static ReadOnlySpan<byte> DataSourceId => "gen_ai.data_source.id"u8;

    // =========================================================================
    // CONTENT ATTRIBUTES (Opt-In, SENSITIVE)
    // =========================================================================

    /// <summary>gen_ai.system_instructions - System prompt/instructions (sensitive)</summary>
    public static ReadOnlySpan<byte> SystemInstructions => "gen_ai.system_instructions"u8;

    /// <summary>gen_ai.input.messages - Input chat history (sensitive, large)</summary>
    public static ReadOnlySpan<byte> InputMessages => "gen_ai.input.messages"u8;

    /// <summary>gen_ai.output.messages - Output completions (sensitive, large)</summary>
    public static ReadOnlySpan<byte> OutputMessages => "gen_ai.output.messages"u8;

    // =========================================================================
    // EMBEDDINGS
    // =========================================================================

    /// <summary>gen_ai.embeddings.dimension.count - Output embedding dimensions</summary>
    public static ReadOnlySpan<byte> EmbeddingsDimensionCount => "gen_ai.embeddings.dimension.count"u8;

    // =========================================================================
    // EVALUATION (gen_ai.evaluation.*)
    // =========================================================================

    /// <summary>gen_ai.evaluation.name - Evaluation metric name</summary>
    public static ReadOnlySpan<byte> EvaluationName => "gen_ai.evaluation.name"u8;

    /// <summary>gen_ai.evaluation.score.value - Numeric evaluation score</summary>
    public static ReadOnlySpan<byte> EvaluationScoreValue => "gen_ai.evaluation.score.value"u8;

    /// <summary>gen_ai.evaluation.score.label - Human-readable score label</summary>
    public static ReadOnlySpan<byte> EvaluationScoreLabel => "gen_ai.evaluation.score.label"u8;

    /// <summary>gen_ai.evaluation.explanation - Score explanation</summary>
    public static ReadOnlySpan<byte> EvaluationExplanation => "gen_ai.evaluation.explanation"u8;

    // =========================================================================
    // TOKEN TYPE (for metrics)
    // =========================================================================

    /// <summary>gen_ai.token.type - Token category (input, output)</summary>
    public static ReadOnlySpan<byte> TokenType => "gen_ai.token.type"u8;

    // =========================================================================
    // DEPRECATED ATTRIBUTES (accept for backward compat)
    // =========================================================================

    /// <summary>gen_ai.system - DEPRECATED: Use gen_ai.provider.name</summary>
    public static ReadOnlySpan<byte> DeprecatedSystem => "gen_ai.system"u8;

    /// <summary>gen_ai.usage.prompt_tokens - DEPRECATED: Use gen_ai.usage.input_tokens</summary>
    public static ReadOnlySpan<byte> DeprecatedPromptTokens => "gen_ai.usage.prompt_tokens"u8;

    /// <summary>gen_ai.usage.completion_tokens - DEPRECATED: Use gen_ai.usage.output_tokens</summary>
    public static ReadOnlySpan<byte> DeprecatedCompletionTokens => "gen_ai.usage.completion_tokens"u8;

    /// <summary>gen_ai.prompt - DEPRECATED: Use gen_ai.input.messages</summary>
    public static ReadOnlySpan<byte> DeprecatedPrompt => "gen_ai.prompt"u8;

    /// <summary>gen_ai.completion - DEPRECATED: Use gen_ai.output.messages</summary>
    public static ReadOnlySpan<byte> DeprecatedCompletion => "gen_ai.completion"u8;

    /// <summary>gen_ai.openai.request.seed - DEPRECATED: Use gen_ai.request.seed</summary>
    public static ReadOnlySpan<byte> DeprecatedOpenAiRequestSeed => "gen_ai.openai.request.seed"u8;

    // Legacy agents.* prefix (pre-1.38 - now use gen_ai.agent.* and gen_ai.tool.*)
    public static ReadOnlySpan<byte> LegacyAgentsAgentId => "agents.agent.id"u8;
    public static ReadOnlySpan<byte> LegacyAgentsAgentName => "agents.agent.name"u8;
    public static ReadOnlySpan<byte> LegacyAgentsToolName => "agents.tool.name"u8;
    public static ReadOnlySpan<byte> LegacyAgentsToolCallId => "agents.tool.call_id"u8;

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    /// <summary>
    ///     Checks if the key starts with "gen_ai." prefix using direct comparison.
    ///     NOTE: Do NOT use SearchValues for prefix matching - it does substring matching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGenAiAttribute(ReadOnlySpan<byte> key)
    {
        if (key.Length < 7) return false;
        return key[..7].SequenceEqual("gen_ai."u8);
    }

    /// <summary>
    ///     Checks if the key starts with "gen_ai.agent." or "gen_ai.tool." prefix.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAgentOrToolAttribute(ReadOnlySpan<byte> key)
    {
        if (key.Length < 13) return false; // "gen_ai.agent." = 13 chars
        return key.StartsWith("gen_ai.agent."u8) || key.StartsWith("gen_ai.tool."u8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetCurrentName(string deprecatedKey, [NotNullWhen(true)] out string? currentKey) =>
        SchemaNormalizer.TryGetCurrentName(deprecatedKey, out currentKey);
}

// =============================================================================
// WELL-KNOWN VALUES
// =============================================================================

/// <summary>
///     Well-known values for gen_ai.operation.name (OTel v1.39)
/// </summary>
internal static class GenAiOperationName
{
    public const string Chat = "chat";
    public const string TextCompletion = "text_completion";
    public const string GenerateContent = "generate_content";
    public const string Embeddings = "embeddings";
    public const string CreateAgent = "create_agent";
    public const string InvokeAgent = "invoke_agent";
    public const string ExecuteTool = "execute_tool";
}

/// <summary>
///     Well-known values for gen_ai.output.type (OTel v1.39)
/// </summary>
internal static class GenAiOutputType
{
    public const string Text = "text";
    public const string Json = "json";
    public const string Image = "image";
    public const string Speech = "speech";
}

/// <summary>
///     Well-known values for gen_ai.tool.type (OTel v1.39)
/// </summary>
internal static class GenAiToolType
{
    public const string Function = "function";
    public const string Extension = "extension";
    public const string Datastore = "datastore";
}

/// <summary>
///     Well-known values for gen_ai.token.type (OTel v1.39 metrics)
/// </summary>
internal static class GenAiTokenType
{
    public const string Input = "input";
    public const string Output = "output";
}

/// <summary>
///     Well-known values for error.type
/// </summary>
internal static class ErrorType
{
    public const string Other = "_OTHER";
}

// =============================================================================
// EVENT NAMES (OTel v1.39)
// =============================================================================

/// <summary>
///     Semantic names for GenAI Events (structured logs).
/// </summary>
internal static class GenAiEventNames
{
    /// <summary>Event containing full input/output details for an inference operation.</summary>
    public const string OperationDetails = "gen_ai.client.inference.operation.details";

    /// <summary>Event containing evaluation results.</summary>
    public const string EvaluationResult = "gen_ai.evaluation.result";
}

// =============================================================================
// METRIC NAMES (OTel v1.39)
// =============================================================================

/// <summary>
///     Semantic names for GenAI Metrics.
/// </summary>
internal static class GenAiMetricNames
{
    /// <summary>Histogram: Number of input and output tokens used.</summary>
    public const string TokenUsage = "gen_ai.client.token.usage";

    /// <summary>Histogram: GenAI operation duration in seconds.</summary>
    public const string OperationDuration = "gen_ai.client.operation.duration";

    /// <summary>Histogram: Server time per output token (decode phase) in seconds.</summary>
    public const string ServerTimePerOutputToken = "gen_ai.server.time_per_output_token";

    /// <summary>Histogram: Server time to first token (prefill phase) in seconds.</summary>
    public const string ServerTimeToFirstToken = "gen_ai.server.time_to_first_token";
}

// =============================================================================
// PROVIDER NAMES (OTel v1.39)
// =============================================================================

/// <summary>
///     Provider identifiers per OTel 1.39 gen_ai.provider.name values.
/// </summary>
internal static class OtlpGenAiProviders
{
    // OTel v1.39 canonical provider names
    public const string Anthropic = "anthropic";
    public const string OpenAi = "openai";
    public const string AzureAiOpenAi = "azure.ai.openai";
    public const string AzureAiInference = "azure.ai.inference";
    public const string AwsBedrock = "aws.bedrock";
    public const string GcpGemini = "gcp.gemini";
    public const string GcpVertexAi = "gcp.vertex_ai";
    public const string GcpGenAi = "gcp.gen_ai";
    public const string Cohere = "cohere";
    public const string DeepSeek = "deepseek";
    public const string Groq = "groq";
    public const string MistralAi = "mistral_ai";
    public const string Perplexity = "perplexity";
    public const string XAi = "x_ai";
    public const string IbmWatsonxAi = "ibm.watsonx.ai";

    private static readonly FrozenDictionary<string, string> HostToProvider =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["api.openai.com"] = OpenAi,
            ["api.anthropic.com"] = Anthropic,
            ["generativelanguage.googleapis.com"] = GcpGemini,
            ["aiplatform.googleapis.com"] = GcpVertexAi,
            ["openai.azure.com"] = AzureAiOpenAi,
            ["api.cohere.ai"] = Cohere,
            ["api.cohere.com"] = Cohere,
            ["api.mistral.ai"] = MistralAi,
            ["api.deepseek.com"] = DeepSeek,
            ["api.groq.com"] = Groq,
            ["api.perplexity.ai"] = Perplexity,
            ["api.x.ai"] = XAi
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // UTF-8 spans for zero-allocation parsing
    public static ReadOnlySpan<byte> OpenAiUtf8 => "openai"u8;
    public static ReadOnlySpan<byte> AnthropicUtf8 => "anthropic"u8;
    public static ReadOnlySpan<byte> GcpGeminiUtf8 => "gcp.gemini"u8;
    public static ReadOnlySpan<byte> AwsBedrockUtf8 => "aws.bedrock"u8;
    public static ReadOnlySpan<byte> AzureAiOpenAiUtf8 => "azure.ai.openai"u8;
    public static ReadOnlySpan<byte> CohereUtf8 => "cohere"u8;
    public static ReadOnlySpan<byte> MistralAiUtf8 => "mistral_ai"u8;
    public static ReadOnlySpan<byte> DeepSeekUtf8 => "deepseek"u8;
    public static ReadOnlySpan<byte> GroqUtf8 => "groq"u8;
    public static ReadOnlySpan<byte> PerplexityUtf8 => "perplexity"u8;
    public static ReadOnlySpan<byte> XAiUtf8 => "x_ai"u8;

    public static bool TryDetectFromHost(ReadOnlySpan<char> host, [NotNullWhen(true)] out string? provider)
    {
        var hostStr = host.ToString();
        if (HostToProvider.TryGetValue(hostStr, out provider)) return true;

        // Partial match for AWS Bedrock
        if (host.Contains("bedrock-runtime", StringComparison.OrdinalIgnoreCase))
        {
            provider = AwsBedrock;
            return true;
        }

        // Partial match for IBM watsonx
        if (host.Contains("watsonx", StringComparison.OrdinalIgnoreCase))
        {
            provider = IbmWatsonxAi;
            return true;
        }

        provider = null;
        return false;
    }
}

// =============================================================================
// SCHEMA NORMALIZER (Deprecated → Current)
// =============================================================================

/// <summary>
///     Single source of truth for deprecated OTel attribute mappings (1.39 migration).
/// </summary>
public static class SchemaNormalizer
{
#pragma warning disable QYL0002 // Intentionally references deprecated attributes for migration
    /// <summary>
    ///     Complete mapping of deprecated attribute names to their 1.39 equivalents.
    /// </summary>
    public static readonly FrozenDictionary<string, string> DeprecatedMappings =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // GenAI deprecated (pre-1.38)
            ["gen_ai.system"] = "gen_ai.provider.name",
            ["gen_ai.prompt"] = "gen_ai.input.messages",
            ["gen_ai.completion"] = "gen_ai.output.messages",
            ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
            ["gen_ai.usage.completion_tokens"] = "gen_ai.usage.output_tokens",
            ["gen_ai.openai.request.seed"] = "gen_ai.request.seed",

            // OpenAI-specific moved out of gen_ai namespace
            ["gen_ai.openai.request.service_tier"] = "openai.request.service_tier",
            ["gen_ai.openai.response.service_tier"] = "openai.response.service_tier",
            ["gen_ai.openai.response.system_fingerprint"] = "openai.response.system_fingerprint",

            // Legacy agents.* prefix → gen_ai.agent.* / gen_ai.tool.*
            ["agents.agent.id"] = "gen_ai.agent.id",
            ["agents.agent.name"] = "gen_ai.agent.name",
            ["agents.agent.description"] = "gen_ai.agent.description",
            ["agents.tool.name"] = "gen_ai.tool.name",
            ["agents.tool.call_id"] = "gen_ai.tool.call.id",

            // Code attributes
            ["code.function"] = "code.function.name",
            ["code.filepath"] = "code.file.path",
            ["code.lineno"] = "code.line.number",

            // DB attributes
            ["db.system"] = "db.system.name"
        }.ToFrozenDictionary(StringComparer.Ordinal);
#pragma warning restore QYL0002

    public static Dictionary<string, object?> NormalizeAttributes(IDictionary<string, object?> attributes)
    {
        Throw.IfNull(attributes);

        var result = new Dictionary<string, object?>(attributes.Count, StringComparer.Ordinal);

        foreach (var (key, value) in attributes)
        {
            var normalizedKey = Normalize(key);
            result[normalizedKey] = value;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Normalize(string attributeName) =>
        DeprecatedMappings.GetValueOrDefault(attributeName, attributeName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDeprecated(string attributeName) => DeprecatedMappings.ContainsKey(attributeName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetCurrentName(string deprecatedKey, [NotNullWhen(true)] out string? currentKey) =>
        DeprecatedMappings.TryGetValue(deprecatedKey, out currentKey);
}

// =============================================================================
// OTLP JSON DTOs
// =============================================================================

public sealed record OtlpExportTraceServiceRequest
{
    public List<OtlpResourceSpans>? ResourceSpans { get; init; }
}

public sealed record OtlpResourceSpans
{
    public OtlpResource? Resource { get; init; }
    public List<OtlpScopeSpans>? ScopeSpans { get; init; }
}

public sealed record OtlpResource
{
    public List<OtlpKeyValue>? Attributes { get; init; }
}

public sealed record OtlpScopeSpans
{
    public List<OtlpSpan>? Spans { get; init; }
}

public sealed record OtlpSpan
{
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? Name { get; init; }
    public int? Kind { get; init; }

    /// <summary>Start time as unsigned 64-bit nanoseconds (OTel fixed64 wire format).</summary>
    public ulong StartTimeUnixNano { get; init; }

    /// <summary>End time as unsigned 64-bit nanoseconds (OTel fixed64 wire format).</summary>
    public ulong EndTimeUnixNano { get; init; }

    public OtlpStatus? Status { get; init; }
    public List<OtlpKeyValue>? Attributes { get; init; }
}

public sealed record OtlpStatus
{
    public int? Code { get; init; }
    public string? Message { get; init; }
}

public sealed record OtlpKeyValue
{
    public string? Key { get; init; }
    public OtlpAnyValue? Value { get; init; }
}

public sealed record OtlpAnyValue
{
    public string? StringValue { get; init; }
    public long? IntValue { get; init; }
    public double? DoubleValue { get; init; }
    public bool? BoolValue { get; init; }
}

// =============================================================================
// PARSED SPAN
// =============================================================================

/// <summary>
///     Parsed span data with promoted GenAI attributes.
/// </summary>
public sealed class ParsedSpan
{
    public TraceId TraceId { get; set; }
    public SpanId SpanId { get; set; }
    public SpanId ParentSpanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpanKind Kind { get; set; }
    public UnixNano StartTime { get; set; }
    public UnixNano EndTime { get; set; }
    public StatusCode Status { get; set; }
    public string? StatusMessage { get; set; }

    // GenAI-specific extracted attributes (OTel 1.39)
    public string? ProviderName { get; set; }
    public string? RequestModel { get; set; }
    public string? ResponseModel { get; set; }
    public string? OperationName { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public double? Temperature { get; set; }
    public long? MaxTokens { get; set; }

    // Session tracking
    public SessionId? SessionId { get; set; }

    // Raw attributes for non-promoted fields
    public List<KeyValuePair<string, object?>>? Attributes { get; set; }

    /// <summary>
    ///     Duration as TimeSpan. Returns TimeSpan.Zero if EndTime is before StartTime (clock skew protection).
    /// </summary>
    public TimeSpan Duration
    {
        get
        {
            // Guard against clock skew where EndTime < StartTime
            if (EndTime.Value < StartTime.Value)
                return TimeSpan.Zero;
            // Safe: duration in nanoseconds / 100 = ticks, fits in long for any reasonable duration
            return TimeSpan.FromTicks((long)((EndTime.Value - StartTime.Value) / 100));
        }
    }

    public long TotalTokens => InputTokens + OutputTokens;
    public bool IsGenAiSpan => ProviderName is not null || RequestModel is not null;
}

// =============================================================================
// ENUMS
// =============================================================================

/// <summary>
///     OTel span kind enumeration.
/// </summary>
public enum SpanKind : byte
{
    Unspecified = 0,
    Internal = 1,
    Server = 2,
    Client = 3,
    Producer = 4,
    Consumer = 5
}

/// <summary>
///     OTel span status code enumeration.
/// </summary>
public enum StatusCode : byte
{
    Unset = 0,
    Ok = 1,
    Error = 2
}

// =============================================================================
// OTLP LOGS JSON DTOs
// =============================================================================

public sealed record OtlpExportLogsServiceRequest
{
    public List<OtlpResourceLogs>? ResourceLogs { get; init; }
}

public sealed record OtlpResourceLogs
{
    public OtlpResource? Resource { get; init; }
    public List<OtlpScopeLogs>? ScopeLogs { get; init; }
}

public sealed record OtlpScopeLogs
{
    public List<OtlpLogRecord>? LogRecords { get; init; }
}

public sealed record OtlpLogRecord
{
    public ulong TimeUnixNano { get; init; }
    public ulong ObservedTimeUnixNano { get; init; }
    public int? SeverityNumber { get; init; }
    public string? SeverityText { get; init; }
    public OtlpAnyValue? Body { get; init; }
    public List<OtlpKeyValue>? Attributes { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
}
