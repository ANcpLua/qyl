// =============================================================================
// qyl.servicedefaults - GenAI Instrumentation
// Leverages Microsoft.Extensions.AI.OpenTelemetryChatClient for OTel compliance
// Uses qyl.protocol.Attributes for OTel 1.39 semantic conventions
// =============================================================================

using Microsoft.Extensions.AI;
using qyl.protocol.Attributes;

namespace Qyl.ServiceDefaults.Instrumentation.GenAi;

/// <summary>
/// Token usage data for GenAI operations.
/// </summary>
/// <param name="InputTokens">Number of input/prompt tokens.</param>
/// <param name="OutputTokens">Number of output/completion tokens.</param>
public readonly record struct TokenUsage(long InputTokens, long OutputTokens);

/// <summary>
/// GenAI instrumentation that leverages Microsoft.Extensions.AI.OpenTelemetryChatClient.
/// Provides OTel Semantic Conventions v1.39 compliance automatically.
/// </summary>
public static class GenAiInstrumentation
{
    /// <summary>
    /// Wraps an IChatClient with OpenTelemetry instrumentation.
    /// Uses M.E.AI.OpenTelemetryChatClient which is fully OTel GenAI SemConv compliant.
    /// </summary>
    /// <param name="client">The chat client to wrap.</param>
    /// <param name="sourceName">Optional custom activity source name.</param>
    /// <param name="enableSensitiveData">
    /// Whether to capture message content.
    /// Can also be set via OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT env var.
    /// </param>
    /// <returns>An instrumented chat client.</returns>
    public static IChatClient WithQylTelemetry(
        this IChatClient client,
        string? sourceName = null,
        bool? enableSensitiveData = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        // Don't double-wrap
        if (client is OpenTelemetryChatClient)
        {
            return client;
        }

        var otelClient = new OpenTelemetryChatClient(
            client,
            sourceName: sourceName ?? GenAiConstants.SourceName);

        // Configure sensitive data capture
        if (enableSensitiveData.HasValue)
        {
            otelClient.EnableSensitiveData = enableSensitiveData.Value;
        }
        // else: OpenTelemetryChatClient respects OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT

        return otelClient;
    }

    /// <summary>
    /// Extension for ChatClientBuilder pipeline - preferred approach.
    /// </summary>
    public static ChatClientBuilder UseQylTelemetry(
        this ChatClientBuilder builder,
        string? sourceName = null,
        Action<OpenTelemetryChatClient>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseOpenTelemetry(
            sourceName: sourceName ?? GenAiConstants.SourceName,
            configure: configure);
    }

    /// <summary>
    /// Creates a span for tool execution (execute_tool operation).
    /// </summary>
    public static Activity? StartToolExecutionSpan(
        string toolName,
        string? callId = null,
        string? toolType = GenAiAttributes.ToolTypes.Function)
    {
        var activity = ActivitySources.GenAiSource.StartActivity(
            $"{GenAiAttributes.Operations.ExecuteTool} {toolName}",
            ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.ExecuteTool);
            activity.SetTag(GenAiAttributes.ToolName, toolName);

            if (callId is not null)
            {
                activity.SetTag(GenAiAttributes.ToolCallId, callId);
            }

            if (toolType is not null)
            {
                activity.SetTag(GenAiAttributes.ToolType, toolType);
            }
        }

        return activity;
    }

    /// <summary>
    /// Records tool execution result on the current activity.
    /// </summary>
    public static void RecordToolResult(Activity? activity, bool success, string? error = null)
    {
        if (activity is null) return;

        if (!success && error is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error, error);
            activity.SetTag(GenAiAttributes.ErrorType, "tool_execution_error");
        }
    }

    /// <summary>
    /// Records an exception on the activity with culture-invariant formatting.
    /// </summary>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);

        // Use culture-invariant exception string for consistent telemetry
        activity.SetTag(GenAiAttributes.ExceptionType, exception.GetType().FullName);
        activity.SetTag(GenAiAttributes.ExceptionMessage, exception.Message);
        activity.AddException(exception);
    }

    #region Execute Methods (for source generator interception)

    // Lazy-initialized metrics
    private static Histogram<long>? s_tokenUsage;
    private static Histogram<double>? s_operationDuration;

    private static Histogram<long> TokenUsageHistogram =>
        s_tokenUsage ??= ActivitySources.GenAiMeter.CreateHistogram<long>(
            GenAiAttributes.Metrics.ClientTokenUsage, "{token}", "Token usage");

    private static Histogram<double> OperationDurationHistogram =>
        s_operationDuration ??= ActivitySources.GenAiMeter.CreateHistogram<double>(
            GenAiAttributes.Metrics.ClientOperationDuration, "s", "Operation duration");

    /// <summary>
    /// Executes an async GenAI operation with full OTel instrumentation.
    /// Called by generated interceptor code for direct SDK calls.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="provider">Provider identifier (e.g., "openai", "anthropic").</param>
    /// <param name="operation">Operation name (e.g., "chat", "embeddings").</param>
    /// <param name="model">Optional model name.</param>
    /// <param name="execute">The async operation to execute.</param>
    /// <param name="extractUsage">Optional function to extract token usage from result.</param>
    /// <returns>The operation result.</returns>
    public static async Task<T> ExecuteAsync<T>(
        string provider,
        string operation,
        string? model,
        Func<Task<T>> execute,
        Func<T, TokenUsage>? extractUsage = null)
    {
        var spanName = model is not null ? $"{operation} {model}" : operation;
        using var activity = ActivitySources.GenAiSource.StartActivity(spanName, ActivityKind.Client);

        var sw = Stopwatch.StartNew();

        if (activity is not null)
        {
            activity.SetTag(GenAiAttributes.OperationName, operation);
            activity.SetTag(GenAiAttributes.ProviderName, provider);
            if (model is not null)
                activity.SetTag(GenAiAttributes.RequestModel, model);
        }

        try
        {
            var result = await execute().ConfigureAwait(false);

            sw.Stop();
            var duration = sw.Elapsed.TotalSeconds;

            if (activity is not null)
            {
                if (extractUsage is not null)
                {
                    try
                    {
                        var usage = extractUsage(result);
                        RecordUsageAndDuration(activity, provider, operation, usage.InputTokens, usage.OutputTokens, duration);
                    }
                    catch
                    {
                        // Ignore usage extraction failures
                        RecordDuration(provider, operation, duration);
                    }
                }
                else
                {
                    RecordDuration(provider, operation, duration);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (activity is not null)
                RecordError(activity, ex, provider, operation, sw.Elapsed.TotalSeconds);
            throw;
        }
    }

    /// <summary>
    /// Executes an async GenAI operation with full OTel instrumentation (no usage extraction).
    /// </summary>
    public static Task<T> ExecuteAsync<T>(
        string provider,
        string operation,
        string? model,
        Func<Task<T>> execute) =>
        ExecuteAsync(provider, operation, model, execute, extractUsage: null);

    /// <summary>
    /// Executes a synchronous GenAI operation with full OTel instrumentation.
    /// Called by generated interceptor code for direct SDK calls.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="provider">Provider identifier (e.g., "openai", "anthropic").</param>
    /// <param name="operation">Operation name (e.g., "chat", "embeddings").</param>
    /// <param name="model">Optional model name.</param>
    /// <param name="execute">The operation to execute.</param>
    /// <param name="extractUsage">Optional function to extract token usage from result.</param>
    /// <returns>The operation result.</returns>
    public static T Execute<T>(
        string provider,
        string operation,
        string? model,
        Func<T> execute,
        Func<T, TokenUsage>? extractUsage = null)
    {
        var spanName = model is not null ? $"{operation} {model}" : operation;
        using var activity = ActivitySources.GenAiSource.StartActivity(spanName, ActivityKind.Client);

        var sw = Stopwatch.StartNew();

        if (activity is not null)
        {
            activity.SetTag(GenAiAttributes.OperationName, operation);
            activity.SetTag(GenAiAttributes.ProviderName, provider);
            if (model is not null)
                activity.SetTag(GenAiAttributes.RequestModel, model);
        }

        try
        {
            var result = execute();

            sw.Stop();
            var duration = sw.Elapsed.TotalSeconds;

            if (activity is not null)
            {
                if (extractUsage is not null)
                {
                    try
                    {
                        var usage = extractUsage(result);
                        RecordUsageAndDuration(activity, provider, operation, usage.InputTokens, usage.OutputTokens, duration);
                    }
                    catch
                    {
                        // Ignore usage extraction failures
                        RecordDuration(provider, operation, duration);
                    }
                }
                else
                {
                    RecordDuration(provider, operation, duration);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (activity is not null)
                RecordError(activity, ex, provider, operation, sw.Elapsed.TotalSeconds);
            throw;
        }
    }

    /// <summary>
    /// Executes a synchronous GenAI operation with full OTel instrumentation (no usage extraction).
    /// </summary>
    public static T Execute<T>(
        string provider,
        string operation,
        string? model,
        Func<T> execute) =>
        Execute(provider, operation, model, execute, extractUsage: null);

    private static void RecordUsageAndDuration(
        Activity activity,
        string provider,
        string operation,
        long inputTokens,
        long outputTokens,
        double durationSeconds)
    {
        if (inputTokens > 0)
        {
            activity.SetTag(GenAiAttributes.UsageInputTokens, inputTokens);
            TokenUsageHistogram.Record(inputTokens,
                new(GenAiAttributes.OperationName, operation),
                new(GenAiAttributes.ProviderName, provider),
                new(GenAiAttributes.TokenType, GenAiAttributes.TokenTypes.Input));
        }

        if (outputTokens > 0)
        {
            activity.SetTag(GenAiAttributes.UsageOutputTokens, outputTokens);
            TokenUsageHistogram.Record(outputTokens,
                new(GenAiAttributes.OperationName, operation),
                new(GenAiAttributes.ProviderName, provider),
                new(GenAiAttributes.TokenType, GenAiAttributes.TokenTypes.Output));
        }

        OperationDurationHistogram.Record(durationSeconds,
            new(GenAiAttributes.OperationName, operation),
            new(GenAiAttributes.ProviderName, provider));
    }

    private static void RecordDuration(string provider, string operation, double durationSeconds)
    {
        OperationDurationHistogram.Record(durationSeconds,
            new(GenAiAttributes.OperationName, operation),
            new(GenAiAttributes.ProviderName, provider));
    }

    private static void RecordError(
        Activity activity,
        Exception ex,
        string provider,
        string operation,
        double durationSeconds)
    {
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);

        var errorType = ex is HttpRequestException { StatusCode: { } code }
            ? ((int)code).ToString()
            : ex.GetType().Name;

        activity.SetTag(GenAiAttributes.ErrorType, errorType);
        activity.SetTag(GenAiAttributes.ExceptionType, ex.GetType().FullName);
        activity.SetTag(GenAiAttributes.ExceptionMessage, ex.Message);
        activity.AddException(ex);

        OperationDurationHistogram.Record(durationSeconds,
            new(GenAiAttributes.OperationName, operation),
            new(GenAiAttributes.ProviderName, provider),
            new(GenAiAttributes.ErrorType, errorType));
    }

    #endregion
}
