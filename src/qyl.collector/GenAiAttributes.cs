using System.Diagnostics.CodeAnalysis;

namespace qyl.collector;

public static class GenAiAttributes
{
    public const string SourceName = "qyl.agents.ai";

    public const string StabilityEnvVar = "OTEL_SEMCONV_STABILITY_OPT_IN";

    public const string StabilityOptIn = "gen_ai_latest_experimental";

    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";

    private const string _prefix = "gen_ai";

    public const string ProviderName = $"{_prefix}.provider.name";

    public const string OperationName = $"{_prefix}.operation.name";

    public const string RequestModel = $"{_prefix}.request.model";

    public const string RequestTemperature = $"{_prefix}.request.temperature";

    public const string RequestTopK = $"{_prefix}.request.top_k";

    public const string RequestTopP = $"{_prefix}.request.top_p";

    public const string RequestPresencePenalty = $"{_prefix}.request.presence_penalty";

    public const string RequestFrequencyPenalty = $"{_prefix}.request.frequency_penalty";

    public const string RequestMaxTokens = $"{_prefix}.request.max_tokens";

    public const string RequestStopSequences = $"{_prefix}.request.stop_sequences";

    public const string RequestChoiceCount = $"{_prefix}.request.choice.count";

    public const string RequestSeed = $"{_prefix}.request.seed";

    public const string RequestEncodingFormats = $"{_prefix}.request.encoding_formats";

    public const string ResponseId = $"{_prefix}.response.id";

    public const string ResponseModel = $"{_prefix}.response.model";

    public const string ResponseFinishReasons = $"{_prefix}.response.finish_reasons";

    public const string UsageInputTokens = $"{_prefix}.usage.input_tokens";

    public const string UsageOutputTokens = $"{_prefix}.usage.output_tokens";

    public const string UsageTotalTokens = $"{_prefix}.usage.total_tokens";

    public const string AgentId = $"{_prefix}.agent.id";

    public const string AgentName = $"{_prefix}.agent.name";

    public const string AgentDescription = $"{_prefix}.agent.description";

    public const string ConversationId = $"{_prefix}.conversation.id";

    public const string SystemInstructions = $"{_prefix}.system_instructions";

    public const string InputMessages = $"{_prefix}.input.messages";

    public const string OutputMessages = $"{_prefix}.output.messages";

    public const string OutputType = $"{_prefix}.output.type";

    public const string ToolDefinitions = $"{_prefix}.tool.definitions";

    public const string ToolName = $"{_prefix}.tool.name";

    public const string ToolDescription = $"{_prefix}.tool.description";

    public const string ToolType = $"{_prefix}.tool.type";

    public const string ToolCallId = $"{_prefix}.tool.call.id";

    public const string ToolCallArguments = $"{_prefix}.tool.call.arguments";

    public const string ToolCallResult = $"{_prefix}.tool.call.result";

    public const string DataSourceId = $"{_prefix}.data_source.id";

    public const string EmbeddingsDimensionCount = $"{_prefix}.embeddings.dimension.count";

    public const string TokenType = $"{_prefix}.token.type";

    public const string EvaluationName = $"{_prefix}.evaluation.name";

    public const string EvaluationScoreValue = $"{_prefix}.evaluation.score.value";

    public const string EvaluationScoreLabel = $"{_prefix}.evaluation.score.label";

    public const string EvaluationExplanation = $"{_prefix}.evaluation.explanation";

    public const string ErrorType = "error.type";

    public const string ErrorMessage = "error.message";

    public const string InvokeAgent = Operations.InvokeAgent;
    public const string ExecuteTool = Operations.ExecuteTool;

    public static void EnsureLatestSemantics()
    {
        var current = Environment.GetEnvironmentVariable(StabilityEnvVar);
        if (string.IsNullOrEmpty(current) || !current.Contains(StabilityOptIn, StringComparison.Ordinal))
            Environment.SetEnvironmentVariable(StabilityEnvVar, StabilityOptIn);
    }

    public static bool IsLatestSemanticsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(StabilityEnvVar);
        return value?.Contains(StabilityOptIn, StringComparison.Ordinal) == true;
    }

#pragma warning disable CA1034
    public static class Operations
#pragma warning restore CA1034
    {
        public const string Chat = "chat";
        public const string GenerateContent = "generate_content";
        public const string TextCompletion = "text_completion";
        public const string Embeddings = "embeddings";
        public const string InvokeAgent = "invoke_agent";
        public const string ExecuteTool = "execute_tool";
        public const string CreateAgent = "create_agent";
    }

    [SuppressMessage("Design", "CA1034:Nested types should not be visible",
        Justification = "Intentionally nested to group deprecated constants")]
    public static class Deprecated
    {
        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(ProviderName)} instead (semconv 1.38)")]
        public const string System = "gen_ai.system";

        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(InputMessages)} instead (semconv 1.38)")]
        public const string Prompt = "gen_ai.prompt";

        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(OutputMessages)} instead (semconv 1.38)")]
        public const string Completion = "gen_ai.completion";

        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(UsageInputTokens)} instead (semconv 1.38)")]
        public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";

        [Obsolete($"Use {nameof(GenAiAttributes)}.{nameof(UsageOutputTokens)} instead (semconv 1.38)")]
        public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";
    }

    internal static class TrackingNames
    {
        public const string Provider = nameof(ProviderName);
        public const string Operation = nameof(OperationName);
        public const string Request = nameof(RequestModel);
        public const string Response = nameof(ResponseModel);
        public const string Usage = nameof(UsageInputTokens);
        public const string Agent = nameof(AgentName);
        public const string Tool = nameof(ToolName);
        public const string Error = nameof(ErrorType);
    }
}