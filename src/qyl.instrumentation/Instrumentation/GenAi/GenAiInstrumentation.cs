// =============================================================================
// qyl.instrumentation - GenAI Instrumentation
// Leverages Microsoft.Extensions.AI.OpenTelemetryChatClient for OTel compliance
// Uses qyl.contracts.Attributes for OTel 1.40 semantic conventions
// =============================================================================

using System.Runtime.CompilerServices;
using ANcpLua.Agents.Instrumentation;
using Microsoft.Extensions.AI;
using qyl.contracts.Attributes;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

/// <summary>
///     Token usage data for GenAI operations.
/// </summary>
/// <param name="InputTokens">Number of input/prompt tokens.</param>
/// <param name="OutputTokens">Number of output/completion tokens.</param>
public readonly record struct TokenUsage(int InputTokens, int OutputTokens);

/// <summary>
///     GenAI instrumentation that leverages Microsoft.Extensions.AI.OpenTelemetryChatClient.
///     Provides OTel Semantic Conventions v1.40 compliance automatically.
/// </summary>
public static class GenAiInstrumentation
{
    /// <summary>
    ///     Wraps an IChatClient with OpenTelemetry instrumentation.
    ///     Uses M.E.AI.OpenTelemetryChatClient which is fully OTel GenAI SemConv compliant.
    /// </summary>
    /// <param name="client">The chat client to wrap.</param>
    /// <param name="sourceName">Optional custom activity source name.</param>
    /// <param name="enableSensitiveData">
    ///     Whether to capture message content.
    ///     Can also be set via OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT env var.
    /// </param>
    /// <returns>An instrumented chat client.</returns>
    public static IChatClient WithQylTelemetry(
        this IChatClient client,
        string? sourceName = null,
        bool? enableSensitiveData = null)
    {
        Guard.NotNull(client);

        switch (client)
        {
            // Don't double-wrap.
            case ToolDecoratingChatClient:
                return client;
            case OpenTelemetryChatClient existingOpenTelemetryClient:
            {
                if (enableSensitiveData.HasValue)
                {
                    existingOpenTelemetryClient.EnableSensitiveData = enableSensitiveData.Value;
                }

                return new ToolDecoratingChatClient(existingOpenTelemetryClient, WrapTool);
            }
        }

        var builder = new ChatClientBuilder(client);

        builder.UseQylTelemetry(
            sourceName: sourceName ?? GenAiConstants.SourceName,
            configure: enableSensitiveData.HasValue
                ? openTelemetryChatClient => openTelemetryChatClient.EnableSensitiveData = enableSensitiveData.Value
                : null);

        return builder.Build();
    }

    /// <summary>Wraps an <see cref="AIFunction"/> with qyl-tagged tool-execution telemetry.</summary>
    public static AIFunction WrapTool(AIFunction inner) =>
        inner is TracedAIFunction ? inner : new TracedAIFunction(inner, ActivitySources.GenAiSource);

    /// <summary>
    ///     Extension for ChatClientBuilder pipeline - preferred approach.
    /// </summary>
    public static ChatClientBuilder UseQylTelemetry(
        this ChatClientBuilder builder,
        string? sourceName = null,
        Action<OpenTelemetryChatClient>? configure = null)
    {
        Guard.NotNull(builder);

        builder.UseOpenTelemetry(
            sourceName: sourceName ?? GenAiConstants.SourceName,
            configure: configure);
        builder.Use(static inner => new ToolDecoratingChatClient(inner, WrapTool));
        return builder;
    }

    /// <summary>
    ///     Creates a span for tool execution (execute_tool operation).
    /// </summary>
    public static Activity? StartToolExecutionSpan(
        string toolName,
        string? callId = null,
        string? toolType = GenAiAttributes.ToolTypes.Function)
    {
        var activity = ActivitySources.GenAiSource.StartActivity(
            $"{GenAiAttributes.Operations.ExecuteTool} {toolName}");

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
    ///     Records tool execution result on the current activity.
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
    ///     Records an exception on the activity with culture-invariant formatting.
    /// </summary>
    public static void RecordException(Activity? activity, Exception exception) =>
        ActivityExceptionTelemetry.Record(activity, exception);

    #region Execute Methods (for source generator interception)

    // Lazy-initialized metrics

    private static Histogram<long> TokenUsageHistogram =>
        field ??= ActivitySources.GenAiMeter.CreateHistogram<long>(
            GenAiAttributes.Metrics.ClientTokenUsage, "{token}", "Token usage");

    private static Histogram<double> OperationDurationHistogram =>
        field ??= ActivitySources.GenAiMeter.CreateHistogram<double>(
            GenAiAttributes.Metrics.ClientOperationDuration, "s", "Operation duration");

    /// <summary>
    ///     Executes an async GenAI operation with full OTel instrumentation.
    ///     Called by generated interceptor code for direct SDK calls.
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
            ApplyDefaultOutputType(activity, operation);
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
                        RecordUsageAndDuration(activity, provider, operation, usage.InputTokens, usage.OutputTokens,
                            duration);
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
    ///     Wraps a streaming GenAI operation with OTel instrumentation.
    ///     Creates a span that covers the entire enumeration, tracking token usage as items are yielded.
    ///     Called by generated interceptor code for streaming SDK calls.
    /// </summary>
    /// <typeparam name="T">The element type of the stream.</typeparam>
    /// <param name="provider">Provider identifier (e.g., "openai", "anthropic").</param>
    /// <param name="operation">Operation name (e.g., "chat").</param>
    /// <param name="model">Optional model name.</param>
    /// <param name="streamFactory">Factory that creates the async enumerable stream.</param>
    /// <param name="cancellationToken">Cancellation token for the enumeration.</param>
    /// <returns>An instrumented async enumerable.</returns>
    public static async IAsyncEnumerable<T> ExecuteStreamingAsync<T>(
        string provider,
        string operation,
        string? model,
        Func<IAsyncEnumerable<T>> streamFactory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var spanName = model is not null ? $"{operation} {model}" : operation;
        using var activity = ActivitySources.GenAiSource.StartActivity(spanName, ActivityKind.Client);

        var sw = Stopwatch.StartNew();
        var outputTokens = 0;

        if (activity is not null)
        {
            activity.SetTag(GenAiAttributes.OperationName, operation);
            activity.SetTag(GenAiAttributes.ProviderName, provider);
            if (model is not null)
                activity.SetTag(GenAiAttributes.RequestModel, model);
            ApplyDefaultOutputType(activity, operation);
        }

        IAsyncEnumerable<T> stream;
        try
        {
            stream = streamFactory();
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (activity is not null)
                RecordError(activity, ex, provider, operation, sw.Elapsed.TotalSeconds);
            throw;
        }

        Exception? caughtException = null;

        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            T current;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    break;
                current = enumerator.Current;
                outputTokens++; // Count each yielded item as a token approximation
            }
            catch (Exception ex)
            {
                caughtException = ex;
                break;
            }

            yield return current;
        }

        sw.Stop();
        var duration = sw.Elapsed.TotalSeconds;

        if (caughtException is not null)
        {
            if (activity is not null)
                RecordError(activity, caughtException, provider, operation, duration);
            throw caughtException;
        }

        if (activity is not null && outputTokens > 0)
        {
            activity.SetTag(GenAiAttributes.UsageOutputTokens, outputTokens);
            TokenUsageHistogram.Record(outputTokens,
                new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
                new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider),
                new KeyValuePair<string, object?>(GenAiAttributes.TokenType, GenAiAttributes.TokenTypes.Output));
        }

        OperationDurationHistogram.Record(duration,
            new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
            new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider));
    }

    private static void RecordUsageAndDuration(
        Activity activity,
        string provider,
        string operation,
        int inputTokens,
        int outputTokens,
        double durationSeconds)
    {
        if (inputTokens > 0)
        {
            activity.SetTag(GenAiAttributes.UsageInputTokens, inputTokens);
            TokenUsageHistogram.Record(inputTokens,
                new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
                new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider),
                new KeyValuePair<string, object?>(GenAiAttributes.TokenType, GenAiAttributes.TokenTypes.Input));
        }

        if (outputTokens > 0)
        {
            activity.SetTag(GenAiAttributes.UsageOutputTokens, outputTokens);
            TokenUsageHistogram.Record(outputTokens,
                new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
                new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider),
                new KeyValuePair<string, object?>(GenAiAttributes.TokenType, GenAiAttributes.TokenTypes.Output));
        }

        OperationDurationHistogram.Record(durationSeconds,
            new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
            new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider));
    }

    private static void RecordDuration(string provider, string operation, double durationSeconds) =>
        OperationDurationHistogram.Record(durationSeconds,
            new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
            new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider));

    private static void ApplyDefaultOutputType(Activity activity, string operation)
    {
        var outputType = GenAiConstants.TryGetDefaultOutputType(operation);
        if (outputType is not null)
        {
            activity.SetTag(GenAiAttributes.OutputType, outputType);
        }
    }

    private static void RecordError(
        Activity activity,
        Exception ex,
        string provider,
        string operation,
        double durationSeconds)
    {
        var errorType = ex is HttpRequestException { StatusCode: { } code }
            ? ((int)code).ToString()
            : ex.GetType().Name;

        ActivityExceptionTelemetry.Record(activity, ex, errorType);

        OperationDurationHistogram.Record(durationSeconds,
            new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
            new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider),
            new KeyValuePair<string, object?>(GenAiAttributes.ErrorType, errorType));
    }

    #endregion
}
