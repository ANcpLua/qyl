namespace Qyl.ServiceDefaults.Instrumentation.GenAi;

/// <summary>
/// Extension methods for setting GenAI semantic convention attributes on Activities.
/// </summary>
/// <remarks>
/// Provides fluent API for OTel 1.39 GenAI semantic conventions.
/// </remarks>
public static class GenAiActivityExtensions
{
    // Not yet in OTel semconv v1.39.0
    private const string UsageInputTokensCached = "gen_ai.usage.input_tokens.cached";
    private const string UsageOutputTokensReasoning = "gen_ai.usage.output_tokens.reasoning";

    /// <summary>
    /// Sets GenAI request attributes on the activity.
    /// </summary>
    public static Activity SetGenAiRequest(
        this Activity activity,
        string? model = null,
        double? temperature = null,
        int? maxTokens = null,
        double? topP = null,
        int? topK = null)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (model is { Length: > 0 })
            activity.SetTag(GenAiRequestAttributes.Model, model);

        if (temperature.HasValue)
            activity.SetTag(GenAiRequestAttributes.Temperature, temperature.Value);

        if (maxTokens.HasValue)
            activity.SetTag(GenAiRequestAttributes.MaxTokens, maxTokens.Value);

        if (topP.HasValue)
            activity.SetTag(GenAiRequestAttributes.TopP, topP.Value);

        if (topK.HasValue)
            activity.SetTag(GenAiRequestAttributes.TopK, topK.Value);

        return activity;
    }

    /// <summary>
    /// Sets GenAI token usage attributes on the activity.
    /// </summary>
    public static Activity SetGenAiUsage(
        this Activity activity,
        long? inputTokens = null,
        long? outputTokens = null,
        long? cachedTokens = null,
        long? reasoningTokens = null)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (inputTokens.HasValue)
            activity.SetTag(GenAiUsageAttributes.InputTokens, inputTokens.Value);

        if (outputTokens.HasValue)
            activity.SetTag(GenAiUsageAttributes.OutputTokens, outputTokens.Value);

        if (cachedTokens.HasValue)
            activity.SetTag(UsageInputTokensCached, cachedTokens.Value);

        if (reasoningTokens.HasValue)
            activity.SetTag(UsageOutputTokensReasoning, reasoningTokens.Value);

        return activity;
    }

    /// <summary>
    /// Sets GenAI response attributes on the activity.
    /// </summary>
    public static Activity SetGenAiResponse(
        this Activity activity,
        string? model = null,
        string? responseId = null,
        string[]? finishReasons = null)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (model is { Length: > 0 })
            activity.SetTag(GenAiResponseAttributes.Model, model);

        if (responseId is { Length: > 0 })
            activity.SetTag(GenAiResponseAttributes.Id, responseId);

        if (finishReasons is { Length: > 0 })
            activity.SetTag(GenAiResponseAttributes.FinishReasons, finishReasons);

        return activity;
    }

    /// <summary>
    /// Sets GenAI agent attributes on the activity (OTel 1.38+).
    /// </summary>
    public static Activity SetGenAiAgent(
        this Activity activity,
        string? agentId = null,
        string? agentName = null,
        string? agentDescription = null)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (agentId is { Length: > 0 })
            activity.SetTag(GenAiAgentAttributes.Id, agentId);

        if (agentName is { Length: > 0 })
            activity.SetTag(GenAiAgentAttributes.Name, agentName);

        if (agentDescription is { Length: > 0 })
            activity.SetTag(GenAiAgentAttributes.Description, agentDescription);

        return activity;
    }

    /// <summary>
    /// Sets GenAI tool attributes on the activity (OTel 1.39).
    /// </summary>
    public static Activity SetGenAiTool(
        this Activity activity,
        string? toolName = null,
        string? toolCallId = null,
        string? conversationId = null)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (toolName is { Length: > 0 })
            activity.SetTag(GenAiToolAttributes.Name, toolName);

        if (toolCallId is { Length: > 0 })
            activity.SetTag(GenAiToolAttributes.CallId, toolCallId);

        if (conversationId is { Length: > 0 })
            activity.SetTag(GenAiConversationAttributes.Id, conversationId);

        return activity;
    }
}
