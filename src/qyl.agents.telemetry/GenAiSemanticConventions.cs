namespace qyl.agents.telemetry;

/// <summary>
/// OpenTelemetry Semantic Conventions for Generative AI (GenAI) v1.38.0.
/// Single source of truth for all GenAI-related attribute names.
/// <see href="https://opentelemetry.io/docs/specs/semconv/gen-ai/"/>
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible - intentional for semantic convention grouping
public static class GenAiSemanticConventions
{
    /// <summary>Default ActivitySource name for agent instrumentation.</summary>
    public const string SourceName = "qyl.agents.ai";

    /// <summary>Convenience constants for common operation values.</summary>
    public const string InvokeAgent = "invoke_agent";
    public const string ExecuteTool = "execute_tool";

#pragma warning disable CA1716 // Identifiers should not match keywords - matches semantic convention naming
    public static class Error
    {
        public const string Type = "error.type";
        public const string Message = "error.message";
    }
#pragma warning restore CA1716

    public static class Provider
    {
        public const string Name = "gen_ai.provider.name";
    }

    public static class Operation
    {
        public const string Name = "gen_ai.operation.name";

        public static class Values
        {
            public const string Chat = "chat";
            public const string GenerateContent = "generate_content";
            public const string TextCompletion = "text_completion";
            public const string Embeddings = "embeddings";
            public const string InvokeAgent = "invoke_agent";
            public const string ExecuteTool = "execute_tool";
            public const string CreateAgent = "create_agent";
        }
    }

    public static class Agent
    {
        public const string Id = "gen_ai.agent.id";
        public const string Name = "gen_ai.agent.name";
        public const string Description = "gen_ai.agent.description";
    }

    public static class Conversation
    {
        public const string Id = "gen_ai.conversation.id";
    }

    public static class Messages
    {
        public const string SystemInstructions = "gen_ai.system_instructions";
        public const string InputMessages = "gen_ai.input.messages";
        public const string OutputMessages = "gen_ai.output.messages";
        public const string OutputType = "gen_ai.output.type";
    }

    public static class Request
    {
        public const string Model = "gen_ai.request.model";
        public const string Temperature = "gen_ai.request.temperature";
        public const string TopK = "gen_ai.request.top_k";
        public const string TopP = "gen_ai.request.top_p";
        public const string PresencePenalty = "gen_ai.request.presence_penalty";
        public const string FrequencyPenalty = "gen_ai.request.frequency_penalty";
        public const string MaxTokens = "gen_ai.request.max_tokens";
        public const string StopSequences = "gen_ai.request.stop_sequences";
        public const string ChoiceCount = "gen_ai.request.choice.count";
        public const string Seed = "gen_ai.request.seed";
        public const string EncodingFormats = "gen_ai.request.encoding_formats";
    }

    public static class Response
    {
        public const string Id = "gen_ai.response.id";
        public const string Model = "gen_ai.response.model";
        public const string FinishReasons = "gen_ai.response.finish_reasons";
    }

    public static class Usage
    {
        public const string InputTokens = "gen_ai.usage.input_tokens";
        public const string OutputTokens = "gen_ai.usage.output_tokens";
    }

    public static class Tool
    {
        public const string Definitions = "gen_ai.tool.definitions";
        public const string Name = "gen_ai.tool.name";
        public const string Description = "gen_ai.tool.description";
        public const string Type = "gen_ai.tool.type";

#pragma warning disable CA1716 // Identifiers should not match keywords - matches semantic convention naming
        public static class Call
        {
            public const string Id = "gen_ai.tool.call.id";
            public const string Arguments = "gen_ai.tool.call.arguments";
            public const string Result = "gen_ai.tool.call.result";
        }
#pragma warning restore CA1716
    }

    public static class DataSource
    {
        public const string Id = "gen_ai.data_source.id";
    }

    public static class Embeddings
    {
        public const string DimensionCount = "gen_ai.embeddings.dimension.count";
    }

    public static class Token
    {
        public const string Type = "gen_ai.token.type";
    }

    public static class Evaluation
    {
        public const string Name = "gen_ai.evaluation.name";
        public const string ScoreValue = "gen_ai.evaluation.score.value";
        public const string ScoreLabel = "gen_ai.evaluation.score.label";
        public const string Explanation = "gen_ai.evaluation.explanation";
    }

    /// <summary>
    /// GenAI attributes that are deprecated as of Semconv 1.38.0.
    /// These should trigger warnings when encountered.
    /// </summary>
    public static class Deprecated
    {
        public const string System = "gen_ai.system";
        public const string Prompt = "gen_ai.prompt";
        public const string Completion = "gen_ai.completion";
        public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";
        public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";
    }
}
#pragma warning restore CA1034
