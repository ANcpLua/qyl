// =============================================================================
// qyl.protocol - GenAI & MCP Semantic Convention Attributes
// OTel 1.39+ gen_ai.* and mcp.* attribute constants
// 100% OTel semconv adherent - no custom extensions
// Owner: qyl.protocol | Consumers: collector, mcp
// =============================================================================

namespace qyl.protocol.Attributes;

/// <summary>
///     OTel 1.39+ GenAI semantic convention attribute keys.
///     Status: Development
///     https://opentelemetry.io/docs/specs/semconv/gen-ai/
/// </summary>
public static class GenAiAttributes
{
    // ═══════════════════════════════════════════════════════════════════════
    // Schema & Instrumentation
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>OTel 1.39 schema URL.</summary>
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.39.0";

    /// <summary>ActivitySource name for GenAI instrumentation.</summary>
    public const string SourceName = "OpenTelemetry.Instrumentation.GenAI";

    // ═══════════════════════════════════════════════════════════════════════
    // Provider & Operation (Required)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.provider.name - The Generative AI provider as identified by the client or server instrumentation.</summary>
    public const string ProviderName = "gen_ai.provider.name";

    /// <summary>gen_ai.operation.name - The name of the operation being performed.</summary>
    public const string OperationName = "gen_ai.operation.name";

    // ═══════════════════════════════════════════════════════════════════════
    // Request attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.request.model - The name of the GenAI model a request is being made to.</summary>
    public const string RequestModel = "gen_ai.request.model";

    /// <summary>gen_ai.request.temperature - The temperature setting for the GenAI request.</summary>
    public const string RequestTemperature = "gen_ai.request.temperature";

    /// <summary>gen_ai.request.max_tokens - The maximum number of tokens the model generates for a request.</summary>
    public const string RequestMaxTokens = "gen_ai.request.max_tokens";

    /// <summary>gen_ai.request.top_p - The top_p sampling setting for the GenAI request.</summary>
    public const string RequestTopP = "gen_ai.request.top_p";

    /// <summary>gen_ai.request.top_k - The top_k sampling setting for the GenAI request.</summary>
    public const string RequestTopK = "gen_ai.request.top_k";

    /// <summary>gen_ai.request.stop_sequences - List of sequences that the model will use to stop generating further tokens.</summary>
    public const string RequestStopSequences = "gen_ai.request.stop_sequences";

    /// <summary>gen_ai.request.frequency_penalty - The frequency penalty setting for the GenAI request.</summary>
    public const string RequestFrequencyPenalty = "gen_ai.request.frequency_penalty";

    /// <summary>gen_ai.request.presence_penalty - The presence penalty setting for the GenAI request.</summary>
    public const string RequestPresencePenalty = "gen_ai.request.presence_penalty";

    /// <summary>gen_ai.request.choice.count - The target number of candidate completions to return.</summary>
    public const string RequestChoiceCount = "gen_ai.request.choice.count";

    /// <summary>gen_ai.request.seed - Requests with same seed value more likely to return same result.</summary>
    public const string RequestSeed = "gen_ai.request.seed";

    /// <summary>gen_ai.request.encoding_formats - The encoding formats requested in an embeddings operation.</summary>
    public const string RequestEncodingFormats = "gen_ai.request.encoding_formats";

    // ═══════════════════════════════════════════════════════════════════════
    // Response attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.response.model - The name of the model that generated the response.</summary>
    public const string ResponseModel = "gen_ai.response.model";

    /// <summary>gen_ai.response.finish_reasons - Array of reasons the model stopped generating tokens.</summary>
    public const string ResponseFinishReasons = "gen_ai.response.finish_reasons";

    /// <summary>gen_ai.response.id - The unique identifier for the completion.</summary>
    public const string ResponseId = "gen_ai.response.id";

    // ═══════════════════════════════════════════════════════════════════════
    // Usage/Token attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.usage.input_tokens - The number of tokens used in the GenAI input (prompt).</summary>
    public const string UsageInputTokens = "gen_ai.usage.input_tokens";

    /// <summary>gen_ai.usage.output_tokens - The number of tokens used in the GenAI response (completion).</summary>
    public const string UsageOutputTokens = "gen_ai.usage.output_tokens";

    /// <summary>gen_ai.token.type - The type of token being counted.</summary>
    public const string TokenType = "gen_ai.token.type";

    // ═══════════════════════════════════════════════════════════════════════
    // Tool/Function calling
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.tool.name - Name of the tool utilized by the agent.</summary>
    public const string ToolName = "gen_ai.tool.name";

    /// <summary>gen_ai.tool.call.id - The tool call identifier.</summary>
    public const string ToolCallId = "gen_ai.tool.call.id";

    /// <summary>gen_ai.tool.description - The tool description.</summary>
    public const string ToolDescription = "gen_ai.tool.description";

    /// <summary>gen_ai.tool.type - Type of the tool utilized by the agent (function, extension, datastore).</summary>
    public const string ToolType = "gen_ai.tool.type";

    /// <summary>gen_ai.tool.call.arguments - Parameters passed to the tool call (Opt-In, sensitive).</summary>
    public const string ToolCallArguments = "gen_ai.tool.call.arguments";

    /// <summary>gen_ai.tool.call.result - The result returned by the tool call (Opt-In, sensitive).</summary>
    public const string ToolCallResult = "gen_ai.tool.call.result";

    /// <summary>gen_ai.tool.definitions - The list of source system tool definitions (Opt-In).</summary>
    public const string ToolDefinitions = "gen_ai.tool.definitions";

    // ═══════════════════════════════════════════════════════════════════════
    // Input/Output content (Opt-In, sensitive)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.input.messages - The chat history provided to the model as an input (Opt-In, sensitive).</summary>
    public const string InputMessages = "gen_ai.input.messages";

    /// <summary>gen_ai.output.messages - Messages returned by the model (Opt-In, sensitive).</summary>
    public const string OutputMessages = "gen_ai.output.messages";

    /// <summary>gen_ai.output.type - Represents the content type requested by the client.</summary>
    public const string OutputType = "gen_ai.output.type";

    /// <summary>gen_ai.system_instructions - The system message or instructions provided to the model (Opt-In, sensitive).</summary>
    public const string SystemInstructions = "gen_ai.system_instructions";

    // ═══════════════════════════════════════════════════════════════════════
    // Conversation/Session
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.conversation.id - The unique identifier for a conversation (session, thread).</summary>
    public const string ConversationId = "gen_ai.conversation.id";

    /// <summary>session.id - Session identifier (standard OTel attribute).</summary>
    public const string SessionId = "session.id";

    // ═══════════════════════════════════════════════════════════════════════
    // Prompt attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.prompt.name - The name of the prompt or prompt template provided.</summary>
    public const string PromptName = "gen_ai.prompt.name";

    // ═══════════════════════════════════════════════════════════════════════
    // Embeddings attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.embeddings.dimension.count - The number of dimensions the resulting output embeddings should have.</summary>
    public const string EmbeddingsDimensionCount = "gen_ai.embeddings.dimension.count";

    // ═══════════════════════════════════════════════════════════════════════
    // Evaluation attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>gen_ai.evaluation.name - The name of the evaluation metric used for the GenAI response.</summary>
    public const string EvaluationName = "gen_ai.evaluation.name";

    /// <summary>gen_ai.evaluation.score.value - The evaluation score returned by the evaluator.</summary>
    public const string EvaluationScoreValue = "gen_ai.evaluation.score.value";

    /// <summary>gen_ai.evaluation.score.label - Human readable label for evaluation.</summary>
    public const string EvaluationScoreLabel = "gen_ai.evaluation.score.label";

    /// <summary>gen_ai.evaluation.explanation - A free-form explanation for the assigned score.</summary>
    public const string EvaluationExplanation = "gen_ai.evaluation.explanation";

    // ═══════════════════════════════════════════════════════════════════════
    // Error attributes (standard OTel)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>error.type - Describes a class of error the operation ended with.</summary>
    public const string ErrorType = "error.type";

    // ═══════════════════════════════════════════════════════════════════════
    // Exception attributes (standard OTel)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>exception.type - The type of the exception (its fully-qualified class name).</summary>
    public const string ExceptionType = "exception.type";

    /// <summary>exception.message - The exception message.</summary>
    public const string ExceptionMessage = "exception.message";

    /// <summary>exception.stacktrace - A stacktrace as a string.</summary>
    public const string ExceptionStacktrace = "exception.stacktrace";

    // ═══════════════════════════════════════════════════════════════════════
    // Server/Client attributes (standard OTel)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>server.address - GenAI server address.</summary>
    public const string ServerAddress = "server.address";

    /// <summary>server.port - GenAI server port.</summary>
    public const string ServerPort = "server.port";

    /// <summary>client.address - Client address.</summary>
    public const string ClientAddress = "client.address";

    /// <summary>client.port - Client port number.</summary>
    public const string ClientPort = "client.port";

    // ═══════════════════════════════════════════════════════════════════════
    // Operation name values (gen_ai.operation.name)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Well-known operation name values.</summary>
    public static class Operations
    {
        /// <summary>chat - Chat completion operation such as OpenAI Chat API.</summary>
        public const string Chat = "chat";

        /// <summary>create_agent - Create GenAI agent.</summary>
        public const string CreateAgent = "create_agent";

        /// <summary>embeddings - Embeddings operation such as OpenAI Create embeddings API.</summary>
        public const string Embeddings = "embeddings";

        /// <summary>execute_tool - Execute a tool.</summary>
        public const string ExecuteTool = "execute_tool";

        /// <summary>generate_content - Multimodal content generation operation such as Gemini Generate Content.</summary>
        public const string GenerateContent = "generate_content";

        /// <summary>invoke_agent - Invoke GenAI agent.</summary>
        public const string InvokeAgent = "invoke_agent";

        /// <summary>text_completion - Text completions operation such as OpenAI Completions API (Legacy).</summary>
        public const string TextCompletion = "text_completion";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Provider name values (gen_ai.provider.name)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Well-known provider name values.</summary>
    public static class Providers
    {
        /// <summary>anthropic - Anthropic.</summary>
        public const string Anthropic = "anthropic";

        /// <summary>aws.bedrock - AWS Bedrock.</summary>
        public const string AwsBedrock = "aws.bedrock";

        /// <summary>azure.ai.inference - Azure AI Inference.</summary>
        public const string AzureAiInference = "azure.ai.inference";

        /// <summary>azure.ai.openai - Azure OpenAI.</summary>
        public const string AzureOpenAi = "azure.ai.openai";

        /// <summary>cohere - Cohere.</summary>
        public const string Cohere = "cohere";

        /// <summary>deepseek - DeepSeek.</summary>
        public const string DeepSeek = "deepseek";

        /// <summary>gcp.gemini - Gemini (AI Studio API).</summary>
        public const string GcpGemini = "gcp.gemini";

        /// <summary>gcp.gen_ai - Any Google generative AI endpoint.</summary>
        public const string GcpGenAi = "gcp.gen_ai";

        /// <summary>gcp.vertex_ai - Vertex AI.</summary>
        public const string GcpVertexAi = "gcp.vertex_ai";

        /// <summary>groq - Groq.</summary>
        public const string Groq = "groq";

        /// <summary>github_copilot - GitHub Copilot.</summary>
        public const string GitHubCopilot = "github_copilot";

        /// <summary>ibm.watsonx.ai - IBM Watsonx AI.</summary>
        public const string IbmWatsonxAi = "ibm.watsonx.ai";

        /// <summary>microsoft_agents - Microsoft Agent Framework.</summary>
        public const string MicrosoftAgents = "microsoft_agents";

        /// <summary>mistral_ai - Mistral AI.</summary>
        public const string MistralAi = "mistral_ai";

        /// <summary>openai - OpenAI.</summary>
        public const string OpenAi = "openai";

        /// <summary>perplexity - Perplexity.</summary>
        public const string Perplexity = "perplexity";

        /// <summary>x_ai - xAI.</summary>
        public const string XAi = "x_ai";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Token type values (gen_ai.token.type)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Well-known token type values.</summary>
    public static class TokenTypes
    {
        /// <summary>input - Input tokens (prompt, input, etc.).</summary>
        public const string Input = "input";

        /// <summary>output - Output tokens (completion, response, etc.).</summary>
        public const string Output = "output";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Output type values (gen_ai.output.type)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Well-known output type values.</summary>
    public static class OutputTypes
    {
        /// <summary>image - Image.</summary>
        public const string Image = "image";

        /// <summary>json - JSON object with known or unknown schema.</summary>
        public const string Json = "json";

        /// <summary>speech - Speech.</summary>
        public const string Speech = "speech";

        /// <summary>text - Plain text.</summary>
        public const string Text = "text";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tool type values (gen_ai.tool.type)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Well-known tool type values.</summary>
    public static class ToolTypes
    {
        /// <summary>function - A tool executed on the client-side.</summary>
        public const string Function = "function";

        /// <summary>extension - A tool executed on the agent-side to directly call external APIs.</summary>
        public const string Extension = "extension";

        /// <summary>datastore - A tool used by the agent to access and query external data.</summary>
        public const string Datastore = "datastore";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Metrics names
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>GenAI metrics names.</summary>
    public static class Metrics
    {
        /// <summary>gen_ai.client.token.usage - Number of input and output tokens used.</summary>
        public const string ClientTokenUsage = "gen_ai.client.token.usage";

        /// <summary>gen_ai.client.operation.duration - GenAI operation duration.</summary>
        public const string ClientOperationDuration = "gen_ai.client.operation.duration";

        /// <summary>gen_ai.server.request.duration - Generative AI server request duration.</summary>
        public const string ServerRequestDuration = "gen_ai.server.request.duration";

        /// <summary>gen_ai.server.time_per_output_token - Time per output token generated after the first token.</summary>
        public const string ServerTimePerOutputToken = "gen_ai.server.time_per_output_token";

        /// <summary>gen_ai.server.time_to_first_token - Time to generate first token for successful responses.</summary>
        public const string ServerTimeToFirstToken = "gen_ai.server.time_to_first_token";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Event names
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>GenAI event names.</summary>
    public static class Events
    {
        /// <summary>gen_ai.client.inference.operation.details - Describes the details of a GenAI completion request.</summary>
        public const string ClientInferenceOperationDetails = "gen_ai.client.inference.operation.details";

        /// <summary>gen_ai.evaluation.result - Captures the result of evaluating GenAI output.</summary>
        public const string EvaluationResult = "gen_ai.evaluation.result";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Deprecated attributes (for migration from v1.36.0)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Deprecated attribute names for backward compatibility.</summary>
    public static class Deprecated
    {
        /// <summary>gen_ai.system - Deprecated, use gen_ai.provider.name.</summary>
        public const string System = "gen_ai.system";

        /// <summary>gen_ai.usage.prompt_tokens - Deprecated, use gen_ai.usage.input_tokens.</summary>
        public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";

        /// <summary>gen_ai.usage.completion_tokens - Deprecated, use gen_ai.usage.output_tokens.</summary>
        public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";
    }
}

