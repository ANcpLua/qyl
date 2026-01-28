using System.Diagnostics;

namespace Qyl.ServiceDefaults.Instrumentation.GenAi;

/// <summary>
/// Instrumentation helpers for GenAI SDK calls (OpenAI, Anthropic, Ollama).
/// </summary>
/// <remarks>
/// Called by generated interceptors to wrap GenAI API calls with OpenTelemetry spans.
/// </remarks>
public static class GenAiInstrumentation
{
    /// <summary>
    /// Executes an async GenAI operation with instrumentation.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="provider">The GenAI provider (e.g., "openai", "anthropic").</param>
    /// <param name="operation">The operation name (e.g., "chat", "embeddings").</param>
    /// <param name="model">The model name, if known.</param>
    /// <param name="execute">The function that executes the actual API call.</param>
    /// <param name="extractUsage">Optional function to extract token usage from the response.</param>
    /// <returns>The response from the GenAI API.</returns>
    public static async Task<TResponse> ExecuteAsync<TResponse>(
        string provider,
        string operation,
        string? model,
        Func<Task<TResponse>> execute,
        Func<TResponse, TokenUsage>? extractUsage = null)
    {
        using var activity = ActivitySources.GenAi.StartActivity(
            $"{operation} {provider}",
            ActivityKind.Client);

        if (activity is null)
            return await execute();

        SetRequestTags(activity, provider, operation, model);

        try
        {
            var response = await execute();
            SetResponseTags(activity, response, extractUsage);
            return response;
        }
        catch (Exception ex)
        {
            SetErrorStatus(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a sync GenAI operation with instrumentation.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="provider">The GenAI provider (e.g., "openai", "anthropic").</param>
    /// <param name="operation">The operation name (e.g., "chat", "embeddings").</param>
    /// <param name="model">The model name, if known.</param>
    /// <param name="execute">The function that executes the actual API call.</param>
    /// <param name="extractUsage">Optional function to extract token usage from the response.</param>
    /// <returns>The response from the GenAI API.</returns>
    public static TResponse Execute<TResponse>(
        string provider,
        string operation,
        string? model,
        Func<TResponse> execute,
        Func<TResponse, TokenUsage>? extractUsage = null)
    {
        using var activity = ActivitySources.GenAi.StartActivity(
            $"{operation} {provider}",
            ActivityKind.Client);

        if (activity is null)
            return execute();

        SetRequestTags(activity, provider, operation, model);

        try
        {
            var response = execute();
            SetResponseTags(activity, response, extractUsage);
            return response;
        }
        catch (Exception ex)
        {
            SetErrorStatus(activity, ex);
            throw;
        }
    }

    private static void SetRequestTags(Activity activity, string provider, string operation, string? model)
    {
        activity.SetTag(GenAiProviderAttributes.Name, provider);
        activity.SetTag(GenAiOperationAttributes.Name, operation);

        if (model is { Length: > 0 })
            activity.SetTag(GenAiRequestAttributes.Model, model);
    }

    private static void SetResponseTags<TResponse>(
        Activity activity,
        TResponse response,
        Func<TResponse, TokenUsage>? extractUsage)
    {
        if (extractUsage is null)
            return;

        try
        {
            var usage = extractUsage(response);
            activity.SetTag(GenAiUsageAttributes.InputTokens, usage.InputTokens);
            activity.SetTag(GenAiUsageAttributes.OutputTokens, usage.OutputTokens);
        }
        catch (Exception ex)
        {
            activity.AddEvent(new ActivityEvent("gen_ai.usage.extraction_failed",
                tags: new ActivityTagsCollection { ["exception.message"] = ex.Message }));
        }
    }

    private static void SetErrorStatus(Activity activity, Exception ex)
    {
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddException(ex);
    }
}
