
using System.Runtime.CompilerServices;
using ANcpLua.Agents.Instrumentation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;
using ErrorAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Error.ErrorAttributes;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

public readonly record struct TokenUsage(int InputTokens, int OutputTokens);

public static class GenAiInstrumentation
{
    private static readonly IServiceProvider s_defaultServices = new ServiceCollection()
        .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
        .BuildServiceProvider();

    public static IChatClient WithQylTelemetry(
        this IChatClient client,
        string? sourceName = null,
        bool? enableSensitiveData = null)
    {
        Guard.NotNull(client);

        switch (client)
        {
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
            sourceName ?? GenAiConstants.SourceName,
            enableSensitiveData.HasValue
                ? openTelemetryChatClient => openTelemetryChatClient.EnableSensitiveData = enableSensitiveData.Value
                : null);

        return builder.Build(s_defaultServices);
    }

    public static AIFunction WrapTool(AIFunction inner) =>
        inner is TracedAIFunction ? inner : new TracedAIFunction(inner, ActivitySources.GenAiSource);

    public static ChatClientBuilder UseQylTelemetry(
        this ChatClientBuilder builder,
        string? sourceName = null,
        Action<OpenTelemetryChatClient>? configure = null)
    {
        Guard.NotNull(builder);

        builder.UseOpenTelemetry(
            sourceName: sourceName ?? GenAiConstants.SourceName,
            configure: configure);
        builder.Use(static (inner, services) =>
        {
            var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            return new LoggingChatClient(inner, loggerFactory.CreateLogger(nameof(GenAiInstrumentation)));
        });
        builder.Use(static inner => new ToolDecoratingChatClient(inner, WrapTool));
        return builder;
    }

    public static AIAgentBuilder UseQylAgentTelemetry(
        this AIAgentBuilder builder,
        string sourceName = "qyl.agent") =>
        Guard.NotNull(builder)
            .UseOpenTelemetry(sourceName)
            .UseLogging();

    public static Activity? StartToolExecutionSpan(
        string toolName,
        string? callId = null,
        string? toolType = "function")
    {
        var activity = ActivitySources.GenAiSource.StartActivity(
            $"{GenAiAttributes.OperationNameValues.ExecuteTool} {toolName}");

        if (activity is not null)
        {
            activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.OperationNameValues.ExecuteTool);
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

    public static void RecordToolResult(Activity? activity, bool success, string? error = null)
    {
        if (activity is null) return;

        if (!success && error is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error, error);
            activity.SetTag(ErrorAttributes.Type, "tool_execution_error");
        }
    }

    public static void RecordException(Activity? activity, Exception exception) =>
        ActivityExceptionTelemetry.Record(activity, exception);

    #region Execute Methods (for source generator interception)


    private static Histogram<long> s_tokenUsageHistogram =>
        field ??= ActivitySources.GenAiMeter.CreateHistogram<long>(
            "gen_ai.client.token.usage", "{token}", "Token usage");

    private static Histogram<double> s_operationDurationHistogram =>
        field ??= ActivitySources.GenAiMeter.CreateHistogram<double>(
            "gen_ai.client.operation.duration", "s", "Operation duration");

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
                outputTokens++;
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
            s_tokenUsageHistogram.Record(outputTokens,
                new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
                new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider),
                new KeyValuePair<string, object?>(GenAiAttributes.TokenType, GenAiAttributes.TokenTypeValues.Output));
        }

        s_operationDurationHistogram.Record(duration,
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
            s_tokenUsageHistogram.Record(inputTokens,
                new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
                new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider),
                new KeyValuePair<string, object?>(GenAiAttributes.TokenType, GenAiAttributes.TokenTypeValues.Input));
        }

        if (outputTokens > 0)
        {
            activity.SetTag(GenAiAttributes.UsageOutputTokens, outputTokens);
            s_tokenUsageHistogram.Record(outputTokens,
                new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
                new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider),
                new KeyValuePair<string, object?>(GenAiAttributes.TokenType, GenAiAttributes.TokenTypeValues.Output));
        }

        s_operationDurationHistogram.Record(durationSeconds,
            new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
            new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider));
    }

    private static void RecordDuration(string provider, string operation, double durationSeconds) =>
        s_operationDurationHistogram.Record(durationSeconds,
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

        s_operationDurationHistogram.Record(durationSeconds,
            new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
            new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider),
            new KeyValuePair<string, object?>(ErrorAttributes.Type, errorType));
    }

    #endregion
}
