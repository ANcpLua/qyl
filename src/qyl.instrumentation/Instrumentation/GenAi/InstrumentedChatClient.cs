// =============================================================================
// qyl.instrumentation - Instrumented Chat Client
// DelegatingChatClient wrapper that instruments any IChatClient with qyl's
// OTel 1.40 GenAI semantic conventions (ActivitySource + histograms).
// Usage:
//   var client = new InstrumentedChatClient(innerClient, agentName: "MyAgent");
//   services.AddChatClient(client);
// =============================================================================

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using qyl.contracts.Attributes;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

/// <summary>
///     Wraps any <see cref="IChatClient" /> with qyl OpenTelemetry instrumentation.
///     Records OTel 1.40 GenAI semantic convention attributes on every chat request.
/// </summary>
/// <remarks>
///     Use this when you have a raw <see cref="IChatClient" /> and want
///     span enrichment with OTel GenAI semantic conventions.
/// </remarks>
public sealed class InstrumentedChatClient : DelegatingChatClient
{
    private readonly string? _agentName;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Creates a new <see cref="InstrumentedChatClient" />.
    /// </summary>
    /// <param name="inner">The <see cref="IChatClient" /> to delegate to.</param>
    /// <param name="agentName">Optional agent name recorded as <c>gen_ai.agent.name</c>.</param>
    /// <param name="timeProvider">Time provider for measuring operation duration. Defaults to <c>TimeProvider.System</c>.</param>
    public InstrumentedChatClient(
        IChatClient inner,
        string? agentName = null,
        TimeProvider? timeProvider = null)
        : base(Guard.NotNull(inner))
    {
        _agentName = agentName;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Lazy-initialized histograms matching GenAiInstrumentation's pattern
    private static Histogram<long> TokenUsageHistogram =>
        field ??= ActivitySources.GenAiMeter.CreateHistogram<long>(
            GenAiAttributes.Metrics.ClientTokenUsage, "{token}", "Token usage");

    private static Histogram<double> OperationDurationHistogram =>
        field ??= ActivitySources.GenAiMeter.CreateHistogram<double>(
            GenAiAttributes.Metrics.ClientOperationDuration, "s", "Operation duration");

    // -- GetResponseAsync -----------------------------------------------------

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var meta = GetService(typeof(ChatClientMetadata)) as ChatClientMetadata;
        var model = options?.ModelId ?? meta?.DefaultModelId ?? "unknown";
        var provider = meta?.ProviderName ?? "unknown";

        using var activity = StartChatActivity(model, provider);
        var startTime = _timeProvider.GetUtcNow();

        EnrichRequestAttributes(activity, options);

        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);

            EnrichResponseAttributes(activity, response, startTime, provider);
            return response;
        }
        catch (HttpRequestException ex)
        {
            RecordError(activity, ex); // transport failure (network, HTTP 4xx/5xx)
            throw;
        }
        catch (JsonException ex)
        {
            RecordError(activity, ex); // provider returned unparseable response
            throw;
        }
    }

    // -- GetStreamingResponseAsync --------------------------------------------

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var meta = GetService(typeof(ChatClientMetadata)) as ChatClientMetadata;
        var model = options?.ModelId ?? meta?.DefaultModelId ?? "unknown";
        var provider = meta?.ProviderName ?? "unknown";

        using var activity = StartChatActivity(model, provider);
        var startTime = _timeProvider.GetUtcNow();

        EnrichRequestAttributes(activity, options);

        var responses = new List<ChatResponseUpdate>();

        var enumerator = base
            .GetStreamingResponseAsync(messages, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                ChatResponseUpdate? current;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        EnrichStreamingResponseAttributes(activity, responses, startTime, provider);
                        activity?.SetStatus(ActivityStatusCode.Ok);
                        yield break;
                    }

                    current = enumerator.Current;
                    responses.Add(current);
                }
                catch (HttpRequestException ex)
                {
                    RecordError(activity, ex); // transport failure
                    throw;
                }
                catch (JsonException ex)
                {
                    RecordError(activity, ex); // unparseable response
                    throw;
                }

                yield return current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    // -- Span helpers ---------------------------------------------------------

    private Activity? StartChatActivity(string model, string provider)
    {
        var activity = ActivitySources.GenAiSource
            .StartActivity($"{GenAiAttributes.Operations.Chat} {model}", ActivityKind.Client);

        if (activity is null) return null;

        activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.Chat);
        activity.SetTag(GenAiAttributes.ProviderName, provider);
        activity.SetTag(GenAiAttributes.RequestModel, model);
        activity.SetTag(GenAiAttributes.OutputType, GenAiAttributes.OutputTypes.Text);

        if (_agentName is not null)
        {
            activity.SetTag("gen_ai.agent.name", _agentName);
        }

        return activity;
    }

    private static void EnrichRequestAttributes(Activity? activity, ChatOptions? options)
    {
        if (activity is null || options is null) return;

        if (options.Temperature is { } temperature)
            activity.SetTag(GenAiAttributes.RequestTemperature, (double)temperature);
        if (options.MaxOutputTokens is { } maxTokens)
            activity.SetTag(GenAiAttributes.RequestMaxTokens, maxTokens);
        if (options.TopP is { } topP)
            activity.SetTag(GenAiAttributes.RequestTopP, (double)topP);
        if (options.FrequencyPenalty is { } freqPenalty)
            activity.SetTag(GenAiAttributes.RequestFrequencyPenalty, (double)freqPenalty);
        if (options.PresencePenalty is { } presencePenalty)
            activity.SetTag(GenAiAttributes.RequestPresencePenalty, (double)presencePenalty);
    }

    private void EnrichResponseAttributes(
        Activity? activity,
        ChatResponse response,
        DateTimeOffset startTime,
        string provider)
    {
        if (activity is null) return;

        if (response.ModelId is { } responseModel)
            activity.SetTag(GenAiAttributes.ResponseModel, responseModel);

        if (response.FinishReason is { } finishReason)
            activity.SetTag(GenAiAttributes.ResponseFinishReasons, new[] { MapFinishReason(finishReason) });

        if (response.Usage is { } usage)
        {
            if (usage.InputTokenCount is { } inputTokens)
            {
                activity.SetTag(GenAiAttributes.UsageInputTokens, (int)inputTokens);
                RecordTokenUsageMetric(inputTokens, provider, GenAiAttributes.TokenTypes.Input);
            }

            if (usage.OutputTokenCount is { } outputTokens)
            {
                activity.SetTag(GenAiAttributes.UsageOutputTokens, (int)outputTokens);
                RecordTokenUsageMetric(outputTokens, provider, GenAiAttributes.TokenTypes.Output);
            }
        }

        var duration = (_timeProvider.GetUtcNow() - startTime).TotalSeconds;
        RecordDurationMetric(duration, provider, GenAiAttributes.Operations.Chat);

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    private void EnrichStreamingResponseAttributes(
        Activity? activity,
        List<ChatResponseUpdate> responses,
        DateTimeOffset startTime,
        string provider)
    {
        if (activity is null) return;

        long inputTokens = 0;
        long outputTokens = 0;
        string? responseModel = null;
        string? finishReason = null;

        foreach (var update in responses)
        {
            if (update.ModelId is { } mid)
                responseModel = mid;

            if (update.FinishReason is { } fr)
                finishReason = MapFinishReason(fr);

            foreach (var content in update.Contents)
            {
                if (content is UsageContent { Details: { } details })
                {
                    inputTokens += details.InputTokenCount ?? 0;
                    outputTokens += details.OutputTokenCount ?? 0;
                }
            }
        }

        if (responseModel is not null)
            activity.SetTag(GenAiAttributes.ResponseModel, responseModel);

        if (finishReason is not null)
            activity.SetTag(GenAiAttributes.ResponseFinishReasons, new[] { finishReason });

        if (inputTokens > 0)
        {
            activity.SetTag(GenAiAttributes.UsageInputTokens, (int)inputTokens);
            RecordTokenUsageMetric(inputTokens, provider, GenAiAttributes.TokenTypes.Input);
        }

        if (outputTokens > 0)
        {
            activity.SetTag(GenAiAttributes.UsageOutputTokens, (int)outputTokens);
            RecordTokenUsageMetric(outputTokens, provider, GenAiAttributes.TokenTypes.Output);
        }

        var duration = (_timeProvider.GetUtcNow() - startTime).TotalSeconds;
        RecordDurationMetric(duration, provider, GenAiAttributes.Operations.Chat);
    }

    // -- Metric recording (replaces CopilotMetrics calls) ---------------------

    private static void RecordTokenUsageMetric(long value, string provider, string tokenType) =>
        TokenUsageHistogram.Record(value,
            new KeyValuePair<string, object?>(GenAiAttributes.OperationName, GenAiAttributes.Operations.Chat),
            new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider),
            new KeyValuePair<string, object?>(GenAiAttributes.TokenType, tokenType));

    private static void RecordDurationMetric(double durationSeconds, string provider, string operation) =>
        OperationDurationHistogram.Record(durationSeconds,
            new KeyValuePair<string, object?>(GenAiAttributes.OperationName, operation),
            new KeyValuePair<string, object?>(GenAiAttributes.ProviderName, provider));

    // -- Error recording ------------------------------------------------------

    private static void RecordError(Activity? activity, Exception ex)
    {
        if (activity is null) return;

        var errorType = ex switch
        {
            OperationCanceledException => "cancelled",
            TimeoutException => "timeout",
            HttpRequestException { StatusCode: { } code } => ((int)code).ToString(),
            _ => ex.GetType().Name
        };

        activity.SetTag(GenAiAttributes.ErrorType, errorType);
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddEvent(new ActivityEvent("exception",
            tags: new ActivityTagsCollection
            {
                { GenAiAttributes.ExceptionType, ex.GetType().FullName ?? ex.GetType().Name },
                { GenAiAttributes.ExceptionMessage, ex.Message }
            }));
    }

    private static string MapFinishReason(ChatFinishReason reason) =>
        reason == ChatFinishReason.Stop ? "stop"
        : reason == ChatFinishReason.Length ? "max_tokens"
        : reason == ChatFinishReason.ToolCalls ? "tool_calls"
        : reason == ChatFinishReason.ContentFilter ? "content_filter"
        : reason.Value;
}
