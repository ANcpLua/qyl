

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi;

public static class GenAiAttributes
{
    public const string AgentDescription = "gen_ai.agent.description";

    public const string AgentId = "gen_ai.agent.id";

    public const string AgentName = "gen_ai.agent.name";

    public const string AgentVersion = "gen_ai.agent.version";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string Completion = "gen_ai.completion";

    public const string ConversationId = "gen_ai.conversation.id";

    public const string DataSourceId = "gen_ai.data_source.id";

    public const string EmbeddingsDimensionCount = "gen_ai.embeddings.dimension.count";

    public const string EvaluationExplanation = "gen_ai.evaluation.explanation";

    public const string EvaluationName = "gen_ai.evaluation.name";

    public const string EvaluationScoreLabel = "gen_ai.evaluation.score.label";

    public const string EvaluationScoreValue = "gen_ai.evaluation.score.value";

    public const string InputMessages = "gen_ai.input.messages";

    [global::System.Obsolete("Replaced by gen_ai.output.type.", false)]
    public const string OpenaiRequestResponseFormat = "gen_ai.openai.request.response_format";

    public static class OpenaiRequestResponseFormatValues
    {
        public const string JsonObject = "json_object";

        public const string JsonSchema = "json_schema";

        public const string Text = "text";
    }

    [global::System.Obsolete("Replaced by gen_ai.request.seed.", false)]
    public const string OpenaiRequestSeed = "gen_ai.openai.request.seed";

    [global::System.Obsolete("Replaced by openai.request.service_tier.", false)]
    public const string OpenaiRequestServiceTier = "gen_ai.openai.request.service_tier";

    public static class OpenaiRequestServiceTierValues
    {
        public const string Auto = "auto";

        public const string Default = "default";
    }

    [global::System.Obsolete("Replaced by openai.response.service_tier.", false)]
    public const string OpenaiResponseServiceTier = "gen_ai.openai.response.service_tier";

    [global::System.Obsolete("Replaced by openai.response.system_fingerprint.", false)]
    public const string OpenaiResponseSystemFingerprint = "gen_ai.openai.response.system_fingerprint";

    public const string OperationName = "gen_ai.operation.name";

    public static class OperationNameValues
    {
        public const string Chat = "chat";

        public const string CreateAgent = "create_agent";

        public const string Embeddings = "embeddings";

        public const string ExecuteTool = "execute_tool";

        public const string GenerateContent = "generate_content";

        public const string InvokeAgent = "invoke_agent";

        public const string InvokeWorkflow = "invoke_workflow";

        public const string Retrieval = "retrieval";

        public const string TextCompletion = "text_completion";
    }

    public const string OutputMessages = "gen_ai.output.messages";

    public const string OutputType = "gen_ai.output.type";

    public static class OutputTypeValues
    {
        public const string Image = "image";

        public const string Json = "json";

        public const string Speech = "speech";

        public const string Text = "text";
    }

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string Prompt = "gen_ai.prompt";

    public const string PromptName = "gen_ai.prompt.name";

    public const string ProviderName = "gen_ai.provider.name";

    public static class ProviderNameValues
    {
        public const string Anthropic = "anthropic";

        public const string AwsBedrock = "aws.bedrock";

        public const string AzureAiInference = "azure.ai.inference";

        public const string AzureAiOpenai = "azure.ai.openai";

        public const string Cohere = "cohere";

        public const string Deepseek = "deepseek";

        public const string GcpGemini = "gcp.gemini";

        public const string GcpGenAi = "gcp.gen_ai";

        public const string GcpVertexAi = "gcp.vertex_ai";

        public const string Groq = "groq";

        public const string IbmWatsonxAi = "ibm.watsonx.ai";

        public const string MistralAi = "mistral_ai";

        public const string Openai = "openai";

        public const string Perplexity = "perplexity";

        public const string XAi = "x_ai";
    }

    public const string RequestChoiceCount = "gen_ai.request.choice.count";

    public const string RequestEncodingFormats = "gen_ai.request.encoding_formats";

    public const string RequestFrequencyPenalty = "gen_ai.request.frequency_penalty";

    public const string RequestMaxTokens = "gen_ai.request.max_tokens";

    public const string RequestModel = "gen_ai.request.model";

    public const string RequestPresencePenalty = "gen_ai.request.presence_penalty";

    public const string RequestSeed = "gen_ai.request.seed";

    public const string RequestStopSequences = "gen_ai.request.stop_sequences";

    public const string RequestStream = "gen_ai.request.stream";

    public const string RequestTemperature = "gen_ai.request.temperature";

    public const string RequestTopK = "gen_ai.request.top_k";

    public const string RequestTopP = "gen_ai.request.top_p";

    public const string ResponseFinishReasons = "gen_ai.response.finish_reasons";

    public const string ResponseId = "gen_ai.response.id";

    public const string ResponseModel = "gen_ai.response.model";

    public const string ResponseTimeToFirstChunk = "gen_ai.response.time_to_first_chunk";

    public const string RetrievalDocuments = "gen_ai.retrieval.documents";

    public const string RetrievalQueryText = "gen_ai.retrieval.query.text";

    [global::System.Obsolete("Replaced by gen_ai.provider.name.", false)]
    public const string System = "gen_ai.system";

    public static class SystemValues
    {
        public const string Anthropic = "anthropic";

        public const string AwsBedrock = "aws.bedrock";

        [global::System.Obsolete("{\"note\": \"Replaced by `azure.ai.inference`.\", \"reason\": \"renamed\", \"renamed_to\": \"azure.ai.inference\"}", false)]
        public const string AzAiInference = "az.ai.inference";

        [global::System.Obsolete("{\"note\": \"Replaced by `azure.ai.openai`.\", \"reason\": \"renamed\", \"renamed_to\": \"azure.ai.openai\"}", false)]
        public const string AzAiOpenai = "az.ai.openai";

        public const string AzureAiInference = "azure.ai.inference";

        public const string AzureAiOpenai = "azure.ai.openai";

        public const string Cohere = "cohere";

        public const string Deepseek = "deepseek";

        public const string GcpGemini = "gcp.gemini";

        public const string GcpGenAi = "gcp.gen_ai";

        public const string GcpVertexAi = "gcp.vertex_ai";

        [global::System.Obsolete("{\"note\": \"Replaced by `gcp.gemini`.\", \"reason\": \"renamed\", \"renamed_to\": \"gcp.gemini\"}", false)]
        public const string Gemini = "gemini";

        public const string Groq = "groq";

        public const string IbmWatsonxAi = "ibm.watsonx.ai";

        public const string MistralAi = "mistral_ai";

        public const string Openai = "openai";

        public const string Perplexity = "perplexity";

        [global::System.Obsolete("{\"note\": \"Replaced by `gcp.vertex_ai`.\", \"reason\": \"renamed\", \"renamed_to\": \"gcp.vertex_ai\"}", false)]
        public const string VertexAi = "vertex_ai";

        public const string Xai = "xai";
    }

    public const string SystemInstructions = "gen_ai.system_instructions";

    public const string TokenType = "gen_ai.token.type";

    public static class TokenTypeValues
    {
        public const string Input = "input";

        [global::System.Obsolete("{\"note\": \"Replaced by `output`.\", \"reason\": \"renamed\", \"renamed_to\": \"output\"}", false)]
        public const string Completion = "output";

        public const string Output = "output";
    }

    public const string ToolCallArguments = "gen_ai.tool.call.arguments";

    public const string ToolCallId = "gen_ai.tool.call.id";

    public const string ToolCallResult = "gen_ai.tool.call.result";

    public const string ToolDefinitions = "gen_ai.tool.definitions";

    public const string ToolDescription = "gen_ai.tool.description";

    public const string ToolName = "gen_ai.tool.name";

    public const string ToolType = "gen_ai.tool.type";

    public const string UsageCacheCreationInputTokens = "gen_ai.usage.cache_creation.input_tokens";

    public const string UsageCacheReadInputTokens = "gen_ai.usage.cache_read.input_tokens";

    [global::System.Obsolete("Replaced by gen_ai.usage.output_tokens.", false)]
    public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";

    public const string UsageInputTokens = "gen_ai.usage.input_tokens";

    public const string UsageOutputTokens = "gen_ai.usage.output_tokens";

    [global::System.Obsolete("Replaced by gen_ai.usage.input_tokens.", false)]
    public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";

    public const string UsageReasoningOutputTokens = "gen_ai.usage.reasoning.output_tokens";

    public const string WorkflowName = "gen_ai.workflow.name";
}
