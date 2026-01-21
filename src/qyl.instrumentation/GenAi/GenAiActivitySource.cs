// =============================================================================
// qyl GenAI Instrumentation - ActivitySource for AI/LLM operations
// =============================================================================
// OTel 1.39 semantic conventions for gen_ai.* attributes
// Manual instrumentation helpers - works with ANY AI SDK
// =============================================================================

namespace qyl.instrumentation.GenAi;

/// <summary>
/// ActivitySource for GenAI operations following OTel 1.39 semantic conventions.
/// Use this to instrument AI/LLM operations from any SDK.
/// </summary>
/// <example>
/// <code>
/// using var activity = GenAiActivitySource.StartChat("openai", "gpt-4o");
/// activity?.SetRequestTemperature(0.7);
///
/// var response = await openAiClient.CompleteChatAsync(messages);
///
/// activity?.SetResponseModel(response.Model);
/// activity?.SetTokenUsage(response.Usage.PromptTokens, response.Usage.CompletionTokens);
/// activity?.SetFinishReason(response.FinishReason);
/// </code>
/// </example>
public static class GenAiActivitySource
{
    /// <summary>
    /// The ActivitySource for all GenAI operations.
    /// Add this source to your TracerProvider: <c>.AddSource(GenAiActivitySource.Name)</c>
    /// </summary>
    public static readonly ActivitySource Source = new("qyl.genai", "1.0.0");

    /// <summary>ActivitySource name for registration.</summary>
    public const string Name = "qyl.genai";

    // =========================================================================
    // Activity Starters - Create spans for GenAI operations
    // =========================================================================

    /// <summary>
    /// Start a chat/completion activity.
    /// </summary>
    /// <param name="providerName">Provider name (openai, anthropic, google, azure_openai, etc.)</param>
    /// <param name="requestModel">Model ID being requested (gpt-4o, claude-3-opus, gemini-pro, etc.)</param>
    /// <returns>Activity if listeners are registered, null otherwise.</returns>
    public static Activity? StartChat(string providerName, string requestModel)
    {
        var activity = Source.StartActivity($"gen_ai.chat {providerName}/{requestModel}", ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag(Attributes.ProviderName, providerName);
        activity.SetTag(Attributes.OperationName, "chat");
        activity.SetTag(Attributes.RequestModel, requestModel);

        return activity;
    }

    /// <summary>
    /// Start an embeddings activity.
    /// </summary>
    public static Activity? StartEmbeddings(string providerName, string requestModel)
    {
        var activity = Source.StartActivity($"gen_ai.embeddings {providerName}/{requestModel}", ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag(Attributes.ProviderName, providerName);
        activity.SetTag(Attributes.OperationName, "embeddings");
        activity.SetTag(Attributes.RequestModel, requestModel);

        return activity;
    }

    /// <summary>
    /// Start an image generation activity.
    /// </summary>
    public static Activity? StartImageGeneration(string providerName, string requestModel)
    {
        var activity = Source.StartActivity($"gen_ai.image_generation {providerName}/{requestModel}", ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag(Attributes.ProviderName, providerName);
        activity.SetTag(Attributes.OperationName, "image_generation");
        activity.SetTag(Attributes.RequestModel, requestModel);

        return activity;
    }

    /// <summary>
    /// Start a tool execution activity (for agent tool calls).
    /// </summary>
    public static Activity? StartToolExecution(string toolName, string? toolCallId = null)
    {
        var activity = Source.StartActivity($"gen_ai.tool {toolName}", ActivityKind.Internal);
        if (activity is null) return null;

        activity.SetTag(Attributes.OperationName, "tool_execution");
        activity.SetTag(Attributes.ToolName, toolName);
        if (toolCallId is not null)
            activity.SetTag(Attributes.ToolCallId, toolCallId);

        return activity;
    }

    /// <summary>
    /// Start a generic GenAI operation activity.
    /// </summary>
    public static Activity? StartOperation(string providerName, string operationName, string requestModel)
    {
        var activity = Source.StartActivity($"gen_ai.{operationName} {providerName}/{requestModel}", ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag(Attributes.ProviderName, providerName);
        activity.SetTag(Attributes.OperationName, operationName);
        activity.SetTag(Attributes.RequestModel, requestModel);

        return activity;
    }

    // =========================================================================
    // OTel 1.39 gen_ai.* Attribute Names
    // =========================================================================

    /// <summary>
    /// Attribute names following OTel 1.39 semantic conventions.
    /// </summary>
    public static class Attributes
    {
        // Provider & System
        public const string ProviderName = "gen_ai.provider.name";
        public const string OperationName = "gen_ai.operation.name";

        // Request
        public const string RequestModel = "gen_ai.request.model";
        public const string RequestTemperature = "gen_ai.request.temperature";
        public const string RequestTopP = "gen_ai.request.top_p";
        public const string RequestTopK = "gen_ai.request.top_k";
        public const string RequestMaxTokens = "gen_ai.request.max_tokens";
        public const string RequestStopSequences = "gen_ai.request.stop_sequences";
        public const string RequestFrequencyPenalty = "gen_ai.request.frequency_penalty";
        public const string RequestPresencePenalty = "gen_ai.request.presence_penalty";
        public const string RequestSeed = "gen_ai.request.seed";

        // Response
        public const string ResponseModel = "gen_ai.response.model";
        public const string ResponseId = "gen_ai.response.id";
        public const string ResponseFinishReasons = "gen_ai.response.finish_reasons";

        // Token Usage
        public const string UsageInputTokens = "gen_ai.usage.input_tokens";
        public const string UsageOutputTokens = "gen_ai.usage.output_tokens";
        public const string UsageInputTokensCached = "gen_ai.usage.input_tokens.cached";
        public const string UsageOutputTokensReasoning = "gen_ai.usage.output_tokens.reasoning";

        // Conversation & Tools
        public const string ConversationId = "gen_ai.conversation.id";
        public const string ToolName = "gen_ai.tool.name";
        public const string ToolCallId = "gen_ai.tool.call.id";

        // Agent (OTel 1.38+)
        public const string AgentId = "gen_ai.agent.id";
        public const string AgentName = "gen_ai.agent.name";
        public const string AgentDescription = "gen_ai.agent.description";

        // Error
        public const string ErrorType = "error.type";

        // qyl Extensions
        public const string CostUsd = "qyl.cost.usd";
        public const string SessionId = "qyl.session.id";
    }

    // =========================================================================
    // Provider Constants
    // =========================================================================

    /// <summary>
    /// Standard provider names for gen_ai.provider.name attribute.
    /// </summary>
    public static class Providers
    {
        public const string OpenAi = "openai";
        public const string Anthropic = "anthropic";
        public const string Google = "google";
        public const string AzureOpenAi = "azure_openai";
        public const string AwsBedrock = "aws_bedrock";
        public const string Cohere = "cohere";
        public const string Mistral = "mistral";
        public const string Meta = "meta";
        public const string Groq = "groq";
        public const string Together = "together_ai";
        public const string Fireworks = "fireworks";
        public const string Local = "local";
    }
}

/// <summary>
/// Extension methods for setting GenAI attributes on Activity.
/// </summary>
public static class GenAiActivityExtensions
{
    // =========================================================================
    // Request Attributes
    // =========================================================================

    public static Activity SetRequestTemperature(this Activity activity, double temperature)
    {
        activity.SetTag(GenAiActivitySource.Attributes.RequestTemperature, temperature);
        return activity;
    }

    public static Activity SetRequestMaxTokens(this Activity activity, int maxTokens)
    {
        activity.SetTag(GenAiActivitySource.Attributes.RequestMaxTokens, maxTokens);
        return activity;
    }

    public static Activity SetRequestTopP(this Activity activity, double topP)
    {
        activity.SetTag(GenAiActivitySource.Attributes.RequestTopP, topP);
        return activity;
    }

    public static Activity SetRequestTopK(this Activity activity, int topK)
    {
        activity.SetTag(GenAiActivitySource.Attributes.RequestTopK, topK);
        return activity;
    }

    public static Activity SetRequestSeed(this Activity activity, long seed)
    {
        activity.SetTag(GenAiActivitySource.Attributes.RequestSeed, seed);
        return activity;
    }

    // =========================================================================
    // Response Attributes
    // =========================================================================

    public static Activity SetResponseModel(this Activity activity, string? model)
    {
        if (model is not null)
            activity.SetTag(GenAiActivitySource.Attributes.ResponseModel, model);
        return activity;
    }

    public static Activity SetResponseId(this Activity activity, string? responseId)
    {
        if (responseId is not null)
            activity.SetTag(GenAiActivitySource.Attributes.ResponseId, responseId);
        return activity;
    }

    public static Activity SetFinishReason(this Activity activity, string? finishReason)
    {
        if (finishReason is not null)
            activity.SetTag(GenAiActivitySource.Attributes.ResponseFinishReasons, finishReason);
        return activity;
    }

    // =========================================================================
    // Token Usage
    // =========================================================================

    public static Activity SetTokenUsage(this Activity activity, int inputTokens, int outputTokens)
    {
        activity.SetTag(GenAiActivitySource.Attributes.UsageInputTokens, inputTokens);
        activity.SetTag(GenAiActivitySource.Attributes.UsageOutputTokens, outputTokens);
        return activity;
    }

    public static Activity SetInputTokens(this Activity activity, int tokens)
    {
        activity.SetTag(GenAiActivitySource.Attributes.UsageInputTokens, tokens);
        return activity;
    }

    public static Activity SetOutputTokens(this Activity activity, int tokens)
    {
        activity.SetTag(GenAiActivitySource.Attributes.UsageOutputTokens, tokens);
        return activity;
    }

    public static Activity SetCachedInputTokens(this Activity activity, int tokens)
    {
        activity.SetTag(GenAiActivitySource.Attributes.UsageInputTokensCached, tokens);
        return activity;
    }

    public static Activity SetReasoningTokens(this Activity activity, int tokens)
    {
        activity.SetTag(GenAiActivitySource.Attributes.UsageOutputTokensReasoning, tokens);
        return activity;
    }

    // =========================================================================
    // Conversation & Tools
    // =========================================================================

    public static Activity SetConversationId(this Activity activity, string conversationId)
    {
        activity.SetTag(GenAiActivitySource.Attributes.ConversationId, conversationId);
        return activity;
    }

    public static Activity SetToolCall(this Activity activity, string toolName, string toolCallId)
    {
        activity.SetTag(GenAiActivitySource.Attributes.ToolName, toolName);
        activity.SetTag(GenAiActivitySource.Attributes.ToolCallId, toolCallId);
        return activity;
    }

    // =========================================================================
    // Agent
    // =========================================================================

    public static Activity SetAgent(this Activity activity, string agentId, string? agentName = null)
    {
        activity.SetTag(GenAiActivitySource.Attributes.AgentId, agentId);
        if (agentName is not null)
            activity.SetTag(GenAiActivitySource.Attributes.AgentName, agentName);
        return activity;
    }

    // =========================================================================
    // qyl Extensions
    // =========================================================================

    public static Activity SetCostUsd(this Activity activity, double costUsd)
    {
        activity.SetTag(GenAiActivitySource.Attributes.CostUsd, costUsd);
        return activity;
    }

    public static Activity SetSessionId(this Activity activity, string sessionId)
    {
        activity.SetTag(GenAiActivitySource.Attributes.SessionId, sessionId);
        return activity;
    }

    // =========================================================================
    // Error Handling
    // =========================================================================

    public static Activity SetGenAiError(this Activity activity, string errorType, Exception? ex = null)
    {
        activity.SetTag(GenAiActivitySource.Attributes.ErrorType, errorType);
        activity.SetStatus(ActivityStatusCode.Error, ex?.Message ?? errorType);
        if (ex is not null)
            activity.AddException(ex);
        return activity;
    }
}
