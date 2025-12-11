using qyl.agents.telemetry;
using qyl.grpc.Models;

namespace qyl.grpc.Extraction;

public static class GenAiExtractor
{
    public static GenAiSpanData? Extract(SpanModel span)
    {
        IReadOnlyDictionary<string, AttributeValue> attrs = span.Attributes;

        bool hasProvider = attrs.ContainsKey(GenAiAttributes.ProviderName) ||
                           attrs.ContainsKey(V136.System);
        bool hasModel = attrs.ContainsKey(GenAiAttributes.RequestModel);

        if (!hasProvider && !hasModel)
            return null;

        return new()
        {
            System = GetString(attrs, GenAiAttributes.ProviderName)
                     ?? GetString(attrs, V136.System),

            RequestModel = GetString(attrs, GenAiAttributes.RequestModel),
            ResponseModel = GetString(attrs, GenAiAttributes.ResponseModel),

            InputTokens = GetLong(attrs, GenAiAttributes.UsageInputTokens)
                          ?? GetLong(attrs, V136.UsagePromptTokens),
            OutputTokens = GetLong(attrs, GenAiAttributes.UsageOutputTokens)
                           ?? GetLong(attrs, V136.UsageCompletionTokens),
            TotalTokens = GetLong(attrs, GenAiAttributes.UsageTotalTokens),

            Temperature = GetDouble(attrs, GenAiAttributes.RequestTemperature),
            MaxTokens = GetLong(attrs, GenAiAttributes.RequestMaxTokens),
            StopReason = GetString(attrs, GenAiAttributes.ResponseFinishReasons),

            CostUsd = GetDecimal(attrs, QylAttributes.CostUsd),
            SessionId = GetString(attrs, QylAttributes.SessionId)
                        ?? GetString(attrs, GenAiAttributes.ConversationId),

            ToolName = GetString(attrs, GenAiAttributes.ToolName),
            ToolCallId = GetString(attrs, GenAiAttributes.ToolCallId)
        };
    }

    public static EnrichedSpan Enrich(SpanModel span)
    {
        GenAiSpanData? genAi = Extract(span);
        return new(span, genAi);
    }

    public static bool UsesDeprecatedAttributes(SpanModel span)
    {
        IReadOnlyDictionary<string, AttributeValue> attrs = span.Attributes;
        return attrs.ContainsKey(V136.System) ||
               attrs.ContainsKey(V136.UsagePromptTokens) ||
               attrs.ContainsKey(V136.UsageCompletionTokens) ||
               attrs.ContainsKey(V136.Prompt) ||
               attrs.ContainsKey(V136.Completion);
    }

    private static string? GetString(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
        attrs.TryGetValue(key, out AttributeValue? v) && v is StringValue sv ? sv.Value : null;

    private static long? GetLong(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
        attrs.TryGetValue(key, out AttributeValue? v)
            ? v switch
            {
                IntValue iv => iv.Value,
                DoubleValue dv => (long)dv.Value,
                StringValue sv when long.TryParse(sv.Value, out long l) => l,
                _ => null
            }
            : null;

    private static double? GetDouble(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
        attrs.TryGetValue(key, out AttributeValue? v)
            ? v switch
            {
                DoubleValue dv => dv.Value,
                IntValue iv => iv.Value,
                StringValue sv when double.TryParse(sv.Value, out double d) => d,
                _ => null
            }
            : null;

    private static decimal? GetDecimal(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
        attrs.TryGetValue(key, out AttributeValue? v)
            ? v switch
            {
                DoubleValue dv => (decimal)dv.Value,
                IntValue iv => iv.Value,
                StringValue sv when decimal.TryParse(sv.Value, out decimal d) => d,
                _ => null
            }
            : null;

    private static class V136
    {
        public const string System = "gen_ai.system";
        public const string Prompt = "gen_ai.prompt";
        public const string Completion = "gen_ai.completion";
        public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";
        public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";
    }
}

public sealed record GenAiSpanData
{
    public string? System { get; init; }

    public string? RequestModel { get; init; }

    public string? ResponseModel { get; init; }

    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public long? TotalTokens { get; init; }

    public double? Temperature { get; init; }

    public long? MaxTokens { get; init; }

    public string? StopReason { get; init; }

    public decimal? CostUsd { get; init; }

    public string? SessionId { get; init; }

    public string? ToolName { get; init; }

    public string? ToolCallId { get; init; }

    public long ComputedTotalTokens => TotalTokens ?? (InputTokens ?? 0) + (OutputTokens ?? 0);

    public bool IsToolCall => ToolName is not null || ToolCallId is not null;

    public string? Model => ResponseModel ?? RequestModel;
}

public sealed record EnrichedSpan(SpanModel Span, GenAiSpanData? GenAi)
{
    public string TraceId => Span.TraceId;
    public string SpanId => Span.SpanId;
    public string Name => Span.Name;
    public DateTimeOffset StartTime => Span.StartTime;
    public double DurationMs => Span.DurationMs;
    public SpanStatus Status => Span.Status;
    public string ServiceName => Span.Resource.ServiceName;

    public string? GenAiSystem => GenAi?.System;
    public string? GenAiModel => GenAi?.Model;
    public long GenAiInputTokens => GenAi?.InputTokens ?? 0;
    public long GenAiOutputTokens => GenAi?.OutputTokens ?? 0;
    public decimal GenAiCostUsd => GenAi?.CostUsd ?? 0;
    public string? SessionId => GenAi?.SessionId;
    public bool IsGenAiSpan => GenAi is not null;
}
