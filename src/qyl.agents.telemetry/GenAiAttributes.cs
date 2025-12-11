using System.Diagnostics.CodeAnalysis;

namespace qyl.agents.telemetry;

public static class GenAiAttributes
{
    public const string SourceName = "qyl.agents.ai";

    public const string StabilityEnvVar = "OTEL_SEMCONV_STABILITY_OPT_IN";

    public const string StabilityOptIn = "gen_ai_latest_experimental";

    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";

    private const string Prefix = "gen_ai";

    public const string ProviderName = $"{Prefix}.provider.name";

    public const string OperationName = $"{Prefix}.operation.name";

    public const string RequestModel = $"{Prefix}.request.model";

    public const string RequestTemperature = $"{Prefix}.request.temperature";

    public const string RequestTopK = $"{Prefix}.request.top_k";

    public const string RequestTopP = $"{Prefix}.request.top_p";

    public const string RequestPresencePenalty = $"{Prefix}.request.presence_penalty";

    public const string RequestFrequencyPenalty = $"{Prefix}.request.frequency_penalty";

    public const string RequestMaxTokens = $"{Prefix}.request.max_tokens";

    public const string RequestStopSequences = $"{Prefix}.request.stop_sequences";

    public const string RequestChoiceCount = $"{Prefix}.request.choice.count";

    public const string RequestSeed = $"{Prefix}.request.seed";

    public const string RequestEncodingFormats = $"{Prefix}.request.encoding_formats";

    public const string ResponseId = $"{Prefix}.response.id";

    public const string ResponseModel = $"{Prefix}.response.model";

    public const string ResponseFinishReasons = $"{Prefix}.response.finish_reasons";

    public const string UsageInputTokens = $"{Prefix}.usage.input_tokens";

    public const string UsageOutputTokens = $"{Prefix}.usage.output_tokens";

    public const string UsageTotalTokens = $"{Prefix}.usage.total_tokens";

    public const string AgentId = $"{Prefix}.agent.id";

    public const string AgentName = $"{Prefix}.agent.name";

    public const string AgentDescription = $"{Prefix}.agent.description";

    public const string ConversationId = $"{Prefix}.conversation.id";

    public const string SystemInstructions = $"{Prefix}.system_instructions";

    public const string InputMessages = $"{Prefix}.input.messages";

    public const string OutputMessages = $"{Prefix}.output.messages";

    public const string OutputType = $"{Prefix}.output.type";

    public const string ToolDefinitions = $"{Prefix}.tool.definitions";

    public const string ToolName = $"{Prefix}.tool.name";

    public const string ToolDescription = $"{Prefix}.tool.description";

    public const string ToolType = $"{Prefix}.tool.type";

    public const string ToolCallId = $"{Prefix}.tool.call.id";

    public const string ToolCallArguments = $"{Prefix}.tool.call.arguments";

    public const string ToolCallResult = $"{Prefix}.tool.call.result";

    public const string DataSourceId = $"{Prefix}.data_source.id";

    public const string EmbeddingsDimensionCount = $"{Prefix}.embeddings.dimension.count";

    public const string TokenType = $"{Prefix}.token.type";

    public const string EvaluationName = $"{Prefix}.evaluation.name";

    public const string EvaluationScoreValue = $"{Prefix}.evaluation.score.value";

    public const string EvaluationScoreLabel = $"{Prefix}.evaluation.score.label";

    public const string EvaluationExplanation = $"{Prefix}.evaluation.explanation";

    public const string ErrorType = "error.type";

    public const string ErrorMessage = "error.message";

    public const string InvokeAgent = Operations.InvokeAgent;
    public const string ExecuteTool = Operations.ExecuteTool;

    public static void EnsureLatestSemantics()
    {
        string? current = Environment.GetEnvironmentVariable(StabilityEnvVar);
        if (string.IsNullOrEmpty(current) || !current.Contains(StabilityOptIn, StringComparison.Ordinal))
            Environment.SetEnvironmentVariable(StabilityEnvVar, StabilityOptIn);
    }

    public static bool IsLatestSemanticsEnabled()
    {
        string? value = Environment.GetEnvironmentVariable(StabilityEnvVar);
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
