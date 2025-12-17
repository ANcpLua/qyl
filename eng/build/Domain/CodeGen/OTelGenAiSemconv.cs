// =============================================================================
// OTel GenAI Semantic Conventions v1.38.0 - Schema Extension
// Extends QylSchema with comprehensive v1.38 definitions
// =============================================================================

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Domain.CodeGen;

/// <summary>
///     OTel GenAI Semantic Conventions v1.38.0 schema extension.
///     Provides complete definitions for code generation.
/// </summary>
public static class OTelGenAiSemconv
{
    // ════════════════════════════════════════════════════════════════════════
    // Attribute Requirement Levels (per OTel spec)
    // ════════════════════════════════════════════════════════════════════════

    public enum AttributeRequirement
    {
        Required,
        ConditionallyRequired,
        Recommended,
        OptIn
    }

    public enum AttributeValueType
    {
        String,
        Int,
        Long,
        Double,
        Boolean,
        StringArray,
        Any
    }

    public const string Version = "1.38.0";
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";

    // ════════════════════════════════════════════════════════════════════════
    // COMPLETE ATTRIBUTE DEFINITIONS (OTel v1.38)
    // ════════════════════════════════════════════════════════════════════════

    public static FrozenDictionary<string, GenAiAttributeSpec> Attributes { get; } = BuildAttributes();

    // ════════════════════════════════════════════════════════════════════════
    // WELL-KNOWN VALUES
    // ════════════════════════════════════════════════════════════════════════

    public static FrozenDictionary<string, ImmutableArray<WellKnownValueSpec>> WellKnownValues { get; } =
        BuildWellKnownValues();

    // ════════════════════════════════════════════════════════════════════════
    // METRICS
    // ════════════════════════════════════════════════════════════════════════

    public static ImmutableArray<MetricSpec> Metrics { get; } =
    [
        new("gen_ai.client.token.usage", "ClientTokenUsage", "Histogram", "{token}",
            "Number of input and output tokens used",
            [1, 4, 16, 64, 256, 1024, 4096, 16384, 65536, 262144, 1048576, 4194304, 16777216, 67108864]),

        new("gen_ai.client.operation.duration", "ClientOperationDuration", "Histogram", "s",
            "GenAI operation duration",
            [0.01, 0.02, 0.04, 0.08, 0.16, 0.32, 0.64, 1.28, 2.56, 5.12, 10.24, 20.48, 40.96, 81.92]),

        new("gen_ai.server.request.duration", "ServerRequestDuration", "Histogram", "s",
            "Generative AI server request duration such as time-to-last byte",
            [0.01, 0.02, 0.04, 0.08, 0.16, 0.32, 0.64, 1.28, 2.56, 5.12, 10.24, 20.48, 40.96, 81.92]),

        new("gen_ai.server.time_to_first_token", "ServerTimeToFirstToken", "Histogram", "s",
            "Time to generate first token for successful responses",
            [0.001, 0.005, 0.01, 0.02, 0.04, 0.06, 0.08, 0.1, 0.25, 0.5, 0.75, 1.0, 2.5, 5.0, 7.5, 10.0]),

        new("gen_ai.server.time_per_output_token", "ServerTimePerOutputToken", "Histogram", "s",
            "Time per output token generated after the first token",
            [0.01, 0.025, 0.05, 0.075, 0.1, 0.15, 0.2, 0.3, 0.4, 0.5, 0.75, 1.0, 2.5])
    ];

    // ════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ════════════════════════════════════════════════════════════════════════

    public static ImmutableArray<EventSpec> Events { get; } =
    [
        new("gen_ai.client.inference.operation.details", "ClientInferenceOperationDetails",
            "Describes the details of a GenAI completion request including chat history and parameters"),
        new("gen_ai.evaluation.result", "EvaluationResult",
            "Captures the result of evaluating GenAI output for quality, accuracy, or other characteristics")
    ];

    // ════════════════════════════════════════════════════════════════════════
    // PREFIX PATTERNS (for SearchValues<string>)
    // ════════════════════════════════════════════════════════════════════════

    public static ImmutableArray<string> GenAiPrefixes { get; } =
    [
        "gen_ai.",
        "gen_ai.agent.",
        "gen_ai.tool.",
        "gen_ai.request.",
        "gen_ai.response.",
        "gen_ai.usage.",
        "gen_ai.evaluation.",
        "gen_ai.embeddings.",
        "gen_ai.data_source.",
        "gen_ai.input.",
        "gen_ai.output."
    ];

    public static ImmutableArray<string> AgentToolPrefixes { get; } =
    [
        "gen_ai.agent."
    ];

    static FrozenDictionary<string, GenAiAttributeSpec> BuildAttributes()
    {
        var attrs = new Dictionary<string, GenAiAttributeSpec>();

        void Add(GenAiAttributeSpec spec)
        {
            attrs[spec.Key] = spec;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CORE (Required on all spans)
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.operation.name", "OperationName", AttributeValueType.String,
            AttributeRequirement.Required, "The name of the operation being performed",
            Group: "core"));

        Add(new GenAiAttributeSpec("gen_ai.provider.name", "ProviderName", AttributeValueType.String,
            AttributeRequirement.Required,
            "The Generative AI provider as identified by the client or server instrumentation",
            Group: "core"));

        Add(new GenAiAttributeSpec("error.type", "ErrorType", AttributeValueType.String,
            AttributeRequirement.ConditionallyRequired, "Describes a class of error the operation ended with",
            Group: "core"));

        // ─────────────────────────────────────────────────────────────────────
        // REQUEST PARAMETERS
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.request.model", "RequestModel", AttributeValueType.String,
            AttributeRequirement.ConditionallyRequired, "The name of the GenAI model a request is being made to",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.max_tokens", "RequestMaxTokens", AttributeValueType.Int,
            AttributeRequirement.Recommended, "The maximum number of tokens the model generates for a request",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.temperature", "RequestTemperature", AttributeValueType.Double,
            AttributeRequirement.Recommended, "The temperature setting for the GenAI request",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.top_p", "RequestTopP", AttributeValueType.Double,
            AttributeRequirement.Recommended, "The top_p sampling setting for the GenAI request",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.top_k", "RequestTopK", AttributeValueType.Double,
            AttributeRequirement.Recommended, "The top_k sampling setting for the GenAI request",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.frequency_penalty", "RequestFrequencyPenalty",
            AttributeValueType.Double,
            AttributeRequirement.Recommended, "The frequency penalty setting for the GenAI request",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.presence_penalty", "RequestPresencePenalty",
            AttributeValueType.Double,
            AttributeRequirement.Recommended, "The presence penalty setting for the GenAI request",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.stop_sequences", "RequestStopSequences",
            AttributeValueType.StringArray,
            AttributeRequirement.Recommended,
            "List of sequences that the model will use to stop generating further tokens",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.seed", "RequestSeed", AttributeValueType.Int,
            AttributeRequirement.ConditionallyRequired,
            "Requests with same seed value more likely to return same result",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.choice.count", "RequestChoiceCount", AttributeValueType.Int,
            AttributeRequirement.ConditionallyRequired, "The target number of candidate completions to return",
            Group: "request"));

        Add(new GenAiAttributeSpec("gen_ai.request.encoding_formats", "RequestEncodingFormats",
            AttributeValueType.StringArray,
            AttributeRequirement.Recommended, "The encoding formats requested in an embeddings operation",
            Group: "request"));

        // ─────────────────────────────────────────────────────────────────────
        // RESPONSE
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.response.model", "ResponseModel", AttributeValueType.String,
            AttributeRequirement.Recommended, "The name of the model that generated the response",
            Group: "response"));

        Add(new GenAiAttributeSpec("gen_ai.response.id", "ResponseId", AttributeValueType.String,
            AttributeRequirement.Recommended, "The unique identifier for the completion",
            Group: "response"));

        Add(new GenAiAttributeSpec("gen_ai.response.finish_reasons", "ResponseFinishReasons",
            AttributeValueType.StringArray,
            AttributeRequirement.Recommended, "Array of reasons the model stopped generating tokens",
            Group: "response"));

        // ─────────────────────────────────────────────────────────────────────
        // USAGE
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.usage.input_tokens", "UsageInputTokens", AttributeValueType.Int,
            AttributeRequirement.Recommended, "The number of tokens used in the GenAI input (prompt)",
            Group: "usage"));

        Add(new GenAiAttributeSpec("gen_ai.usage.output_tokens", "UsageOutputTokens", AttributeValueType.Int,
            AttributeRequirement.Recommended, "The number of tokens used in the GenAI response (completion)",
            Group: "usage"));

        Add(new GenAiAttributeSpec("gen_ai.token.type", "TokenType", AttributeValueType.String,
            AttributeRequirement.Required, "The type of token being counted (for metrics)",
            Group: "usage"));

        // ─────────────────────────────────────────────────────────────────────
        // OUTPUT
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.output.type", "OutputType", AttributeValueType.String,
            AttributeRequirement.ConditionallyRequired, "Represents the content type requested by the client",
            Group: "output"));

        // ─────────────────────────────────────────────────────────────────────
        // CONVERSATION/SESSION
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.conversation.id", "ConversationId", AttributeValueType.String,
            AttributeRequirement.ConditionallyRequired, "The unique identifier for a conversation (session, thread)",
            Group: "conversation"));

        // ─────────────────────────────────────────────────────────────────────
        // AGENT
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.agent.id", "AgentId", AttributeValueType.String,
            AttributeRequirement.ConditionallyRequired, "The unique identifier of the GenAI agent",
            Group: "agent"));

        Add(new GenAiAttributeSpec("gen_ai.agent.name", "AgentName", AttributeValueType.String,
            AttributeRequirement.ConditionallyRequired, "Human-readable name of the GenAI agent",
            Group: "agent"));

        Add(new GenAiAttributeSpec("gen_ai.agent.description", "AgentDescription", AttributeValueType.String,
            AttributeRequirement.ConditionallyRequired, "Free-form description of the GenAI agent",
            Group: "agent"));

        // ─────────────────────────────────────────────────────────────────────
        // TOOL
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.tool.name", "ToolName", AttributeValueType.String,
            AttributeRequirement.Recommended, "Name of the tool utilized by the agent",
            Group: "tool"));

        Add(new GenAiAttributeSpec("gen_ai.tool.call.id", "ToolCallId", AttributeValueType.String,
            AttributeRequirement.Recommended, "The tool call identifier",
            Group: "tool"));

        Add(new GenAiAttributeSpec("gen_ai.tool.type", "ToolType", AttributeValueType.String,
            AttributeRequirement.Recommended, "Type of the tool utilized by the agent",
            Group: "tool"));

        Add(new GenAiAttributeSpec("gen_ai.tool.description", "ToolDescription", AttributeValueType.String,
            AttributeRequirement.Recommended, "The tool description",
            Group: "tool"));

        Add(new GenAiAttributeSpec("gen_ai.tool.call.arguments", "ToolCallArguments", AttributeValueType.Any,
            AttributeRequirement.OptIn, "Parameters passed to the tool call",
            IsSensitive: true, Group: "tool"));

        Add(new GenAiAttributeSpec("gen_ai.tool.call.result", "ToolCallResult", AttributeValueType.Any,
            AttributeRequirement.OptIn, "The result returned by the tool call",
            IsSensitive: true, Group: "tool"));

        Add(new GenAiAttributeSpec("gen_ai.tool.definitions", "ToolDefinitions", AttributeValueType.Any,
            AttributeRequirement.OptIn, "The list of source system tool definitions available",
            Group: "tool"));

        // ─────────────────────────────────────────────────────────────────────
        // DATA SOURCE (RAG)
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.data_source.id", "DataSourceId", AttributeValueType.String,
            AttributeRequirement.ConditionallyRequired, "The data source identifier (RAG)",
            Group: "data_source"));

        // ─────────────────────────────────────────────────────────────────────
        // CONTENT (Opt-In, Sensitive)
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.system_instructions", "SystemInstructions", AttributeValueType.Any,
            AttributeRequirement.OptIn, "The system message or instructions provided to the GenAI model",
            IsSensitive: true, Group: "content"));

        Add(new GenAiAttributeSpec("gen_ai.input.messages", "InputMessages", AttributeValueType.Any,
            AttributeRequirement.OptIn, "The chat history provided to the model as an input",
            IsSensitive: true, Group: "content"));

        Add(new GenAiAttributeSpec("gen_ai.output.messages", "OutputMessages", AttributeValueType.Any,
            AttributeRequirement.OptIn, "Messages returned by the model",
            IsSensitive: true, Group: "content"));

        // ─────────────────────────────────────────────────────────────────────
        // EMBEDDINGS
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.embeddings.dimension.count", "EmbeddingsDimensionCount",
            AttributeValueType.Int,
            AttributeRequirement.Recommended, "The number of dimensions the resulting output embeddings should have",
            Group: "embeddings"));

        // ─────────────────────────────────────────────────────────────────────
        // EVALUATION
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.evaluation.name", "EvaluationName", AttributeValueType.String,
            AttributeRequirement.Required, "The name of the evaluation metric used",
            Group: "evaluation"));

        Add(new GenAiAttributeSpec("gen_ai.evaluation.score.value", "EvaluationScoreValue", AttributeValueType.Double,
            AttributeRequirement.ConditionallyRequired, "The evaluation score returned by the evaluator",
            Group: "evaluation"));

        Add(new GenAiAttributeSpec("gen_ai.evaluation.score.label", "EvaluationScoreLabel", AttributeValueType.String,
            AttributeRequirement.ConditionallyRequired, "Human readable label for evaluation",
            Group: "evaluation"));

        Add(new GenAiAttributeSpec("gen_ai.evaluation.explanation", "EvaluationExplanation", AttributeValueType.String,
            AttributeRequirement.Recommended, "A free-form explanation for the assigned score",
            Group: "evaluation"));

        // ─────────────────────────────────────────────────────────────────────
        // SERVER
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("server.address", "ServerAddress", AttributeValueType.String,
            AttributeRequirement.Recommended, "GenAI server address",
            Group: "server"));

        Add(new GenAiAttributeSpec("server.port", "ServerPort", AttributeValueType.Int,
            AttributeRequirement.ConditionallyRequired, "GenAI server port",
            Group: "server"));

        // ─────────────────────────────────────────────────────────────────────
        // DEPRECATED (v1.36 and earlier)
        // ─────────────────────────────────────────────────────────────────────

        Add(new GenAiAttributeSpec("gen_ai.system", "DeprecatedSystem", AttributeValueType.String,
            AttributeRequirement.Required, "DEPRECATED: The Generative AI product as identified by the client",
            IsDeprecated: true, ReplacedBy: "gen_ai.provider.name", Group: "deprecated"));

        Add(new GenAiAttributeSpec("gen_ai.usage.prompt_tokens", "DeprecatedPromptTokens", AttributeValueType.Int,
            AttributeRequirement.Recommended, "DEPRECATED: Input token count",
            IsDeprecated: true, ReplacedBy: "gen_ai.usage.input_tokens", Group: "deprecated"));

        Add(new GenAiAttributeSpec("gen_ai.usage.completion_tokens", "DeprecatedCompletionTokens",
            AttributeValueType.Int,
            AttributeRequirement.Recommended, "DEPRECATED: Output token count",
            IsDeprecated: true, ReplacedBy: "gen_ai.usage.output_tokens", Group: "deprecated"));

        Add(new GenAiAttributeSpec("gen_ai.prompt", "DeprecatedPrompt", AttributeValueType.String,
            AttributeRequirement.OptIn, "DEPRECATED: Use gen_ai.input.messages",
            IsDeprecated: true, ReplacedBy: "gen_ai.input.messages", Group: "deprecated"));

        Add(new GenAiAttributeSpec("gen_ai.completion", "DeprecatedCompletion", AttributeValueType.String,
            AttributeRequirement.OptIn, "DEPRECATED: Use gen_ai.output.messages",
            IsDeprecated: true, ReplacedBy: "gen_ai.output.messages", Group: "deprecated"));

        Add(new GenAiAttributeSpec("gen_ai.openai.request.seed", "DeprecatedOpenAiRequestSeed", AttributeValueType.Int,
            AttributeRequirement.Recommended, "DEPRECATED: Use gen_ai.request.seed",
            IsDeprecated: true, ReplacedBy: "gen_ai.request.seed", Group: "deprecated"));

        Add(new GenAiAttributeSpec("gen_ai.openai.request.service_tier", "DeprecatedOpenAiRequestServiceTier",
            AttributeValueType.String,
            AttributeRequirement.Recommended, "DEPRECATED: Use openai.request.service_tier",
            IsDeprecated: true, ReplacedBy: "openai.request.service_tier", Group: "deprecated"));

        Add(new GenAiAttributeSpec("gen_ai.openai.response.service_tier", "DeprecatedOpenAiResponseServiceTier",
            AttributeValueType.String,
            AttributeRequirement.Recommended, "DEPRECATED: Use openai.response.service_tier",
            IsDeprecated: true, ReplacedBy: "openai.response.service_tier", Group: "deprecated"));

        // Legacy agents.* prefix
        Add(new GenAiAttributeSpec("agents.agent.id", "LegacyAgentsAgentId", AttributeValueType.String,
            AttributeRequirement.Recommended, "LEGACY: Use gen_ai.agent.id",
            IsDeprecated: true, ReplacedBy: "gen_ai.agent.id", Group: "deprecated"));

        Add(new GenAiAttributeSpec("agents.agent.name", "LegacyAgentsAgentName", AttributeValueType.String,
            AttributeRequirement.Recommended, "LEGACY: Use gen_ai.agent.name",
            IsDeprecated: true, ReplacedBy: "gen_ai.agent.name", Group: "deprecated"));

        Add(new GenAiAttributeSpec("agents.tool.name", "LegacyAgentsToolName", AttributeValueType.String,
            AttributeRequirement.Recommended, "LEGACY: Use gen_ai.tool.name",
            IsDeprecated: true, ReplacedBy: "gen_ai.tool.name", Group: "deprecated"));

        return attrs.ToFrozenDictionary();
    }

    static FrozenDictionary<string, ImmutableArray<WellKnownValueSpec>> BuildWellKnownValues()
    {
        var values = new Dictionary<string, ImmutableArray<WellKnownValueSpec>>();

        // gen_ai.operation.name
        values["gen_ai.operation.name"] =
        [
            new("gen_ai.operation.name", "chat", "Chat", "Chat completion operation such as OpenAI Chat API"),
            new("gen_ai.operation.name", "text_completion", "TextCompletion",
                "Text completions operation such as OpenAI Completions API (Legacy)"),
            new("gen_ai.operation.name", "generate_content", "GenerateContent",
                "Multimodal content generation operation such as Gemini Generate Content"),
            new("gen_ai.operation.name", "embeddings", "Embeddings",
                "Embeddings operation such as OpenAI Create embeddings API"),
            new("gen_ai.operation.name", "create_agent", "CreateAgent", "Create GenAI agent"),
            new("gen_ai.operation.name", "invoke_agent", "InvokeAgent", "Invoke GenAI agent"),
            new("gen_ai.operation.name", "execute_tool", "ExecuteTool", "Execute a tool")
        ];

        // gen_ai.provider.name
        values["gen_ai.provider.name"] =
        [
            new("gen_ai.provider.name", "anthropic", "Anthropic", "Anthropic"),
            new("gen_ai.provider.name", "openai", "OpenAi", "OpenAI"),
            new("gen_ai.provider.name", "azure.ai.openai", "AzureOpenAi", "Azure OpenAI"),
            new("gen_ai.provider.name", "azure.ai.inference", "AzureAiInference", "Azure AI Inference"),
            new("gen_ai.provider.name", "aws.bedrock", "AwsBedrock", "AWS Bedrock"),
            new("gen_ai.provider.name", "gcp.gemini", "GcpGemini", "Gemini (AI Studio API)"),
            new("gen_ai.provider.name", "gcp.vertex_ai", "GcpVertexAi", "Vertex AI"),
            new("gen_ai.provider.name", "gcp.gen_ai", "GcpGenAi", "Any Google generative AI endpoint"),
            new("gen_ai.provider.name", "cohere", "Cohere", "Cohere"),
            new("gen_ai.provider.name", "deepseek", "DeepSeek", "DeepSeek"),
            new("gen_ai.provider.name", "groq", "Groq", "Groq"),
            new("gen_ai.provider.name", "mistral_ai", "MistralAi", "Mistral AI"),
            new("gen_ai.provider.name", "perplexity", "Perplexity", "Perplexity"),
            new("gen_ai.provider.name", "x_ai", "XAi", "xAI"),
            new("gen_ai.provider.name", "ibm.watsonx.ai", "IbmWatsonxAi", "IBM Watsonx AI")
        ];

        // gen_ai.output.type
        values["gen_ai.output.type"] =
        [
            new("gen_ai.output.type", "text", "Text", "Plain text"),
            new("gen_ai.output.type", "json", "Json", "JSON object with known or unknown schema"),
            new("gen_ai.output.type", "image", "Image", "Image"),
            new("gen_ai.output.type", "speech", "Speech", "Speech")
        ];

        // gen_ai.tool.type
        values["gen_ai.tool.type"] =
        [
            new("gen_ai.tool.type", "function", "Function", "A tool executed on the client-side"),
            new("gen_ai.tool.type", "extension", "Extension",
                "A tool executed on the agent-side to directly call external APIs"),
            new("gen_ai.tool.type", "datastore", "Datastore",
                "A tool used by the agent to access and query external data")
        ];

        // gen_ai.token.type
        values["gen_ai.token.type"] =
        [
            new("gen_ai.token.type", "input", "Input", "Input tokens (prompt, input, etc.)"),
            new("gen_ai.token.type", "output", "Output", "Output tokens (completion, response, etc.)")
        ];

        // error.type
        values["error.type"] =
        [
            new("error.type", "_OTHER", "Other",
                "A fallback error value to be used when the instrumentation doesn't define a custom value")
        ];

        return values.ToFrozenDictionary();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Enhanced Attribute Definition
    // ════════════════════════════════════════════════════════════════════════

    public sealed record GenAiAttributeSpec(
        string Key,
        string ConstantName,
        AttributeValueType ValueType,
        AttributeRequirement Requirement,
        string Description,
        ImmutableArray<string>? WellKnownValues = null,
        bool IsSensitive = false,
        bool IsDeprecated = false,
        string? ReplacedBy = null,
        string? Group = null);

    // ════════════════════════════════════════════════════════════════════════
    // Well-Known Value Definition
    // ════════════════════════════════════════════════════════════════════════

    public sealed record WellKnownValueSpec(
        string AttributeKey,
        string Value,
        string ConstantName,
        string Description);

    // ════════════════════════════════════════════════════════════════════════
    // Metric Definition
    // ════════════════════════════════════════════════════════════════════════

    public sealed record MetricSpec(
        string Name,
        string ConstantName,
        string InstrumentType,
        string Unit,
        string Description,
        double[]? ExplicitBucketBoundaries = null);

    // ════════════════════════════════════════════════════════════════════════
    // Event Definition
    // ════════════════════════════════════════════════════════════════════════

    public sealed record EventSpec(
        string Name,
        string ConstantName,
        string Description);
}