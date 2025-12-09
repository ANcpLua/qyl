    using qyl.Grpc.Models;

    namespace qyl.Grpc.Extraction;

    /// <summary>
    /// Extracts gen_ai.* semantic convention attributes into typed fields.
    /// Handles the denormalization from attribute bags to queryable columns.
    /// </summary>
    public static class GenAiExtractor
    {
        /// <summary>
        /// Extract gen_ai.* attributes from a span into a typed record.
        /// Returns null if span has no gen_ai attributes (not an LLM call).
        /// </summary>
        public static GenAiSpanData? Extract(SpanModel span)
        {
            var attrs = span.Attributes;

            // Check if this is a gen_ai span
            if (!attrs.ContainsKey(GenAiAttributes.System) &&
                !attrs.ContainsKey(GenAiAttributes.RequestModel))
                return null;

            return new GenAiSpanData
            {
                System = GetString(attrs, GenAiAttributes.System),
                RequestModel = GetString(attrs, GenAiAttributes.RequestModel),
                ResponseModel = GetString(attrs, GenAiAttributes.ResponseModel),
                InputTokens = GetLong(attrs, GenAiAttributes.UsageInputTokens)
                           ?? GetLong(attrs, GenAiAttributes.UsagePromptTokens),
                OutputTokens = GetLong(attrs, GenAiAttributes.UsageOutputTokens)
                            ?? GetLong(attrs, GenAiAttributes.UsageCompletionTokens),
                TotalTokens = GetLong(attrs, GenAiAttributes.UsageTotalTokens),
                Temperature = GetDouble(attrs, GenAiAttributes.RequestTemperature),
                MaxTokens = GetLong(attrs, GenAiAttributes.RequestMaxTokens),
                StopReason = GetString(attrs, GenAiAttributes.ResponseFinishReason),
                CostUsd = GetDecimal(attrs, QylAttributes.CostUsd),
                SessionId = GetString(attrs, QylAttributes.SessionId)
                         ?? GetString(attrs, GenAiAttributes.ConversationId),
                ToolName = GetString(attrs, GenAiAttributes.ToolName),
                ToolCallId = GetString(attrs, GenAiAttributes.ToolCallId),
            };
        }

        /// <summary>
        /// Enrich a SpanModel with extracted gen_ai data for storage.
        /// Returns a new record with denormalized fields populated.
        /// </summary>
        public static EnrichedSpan Enrich(SpanModel span)
        {
            var genAi = Extract(span);
            return new EnrichedSpan(span, genAi);
        }

        private static string? GetString(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
            attrs.TryGetValue(key, out var v) && v is StringValue sv ? sv.Value : null;

        private static long? GetLong(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
            attrs.TryGetValue(key, out var v) ? v switch
            {
                IntValue iv => iv.Value,
                DoubleValue dv => (long)dv.Value,
                StringValue sv when long.TryParse(sv.Value, out var l) => l,
                _ => null
            } : null;

        private static double? GetDouble(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
            attrs.TryGetValue(key, out var v) ? v switch
            {
                DoubleValue dv => dv.Value,
                IntValue iv => iv.Value,
                StringValue sv when double.TryParse(sv.Value, out var d) => d,
                _ => null
            } : null;

        private static decimal? GetDecimal(IReadOnlyDictionary<string, AttributeValue> attrs, string key) =>
            attrs.TryGetValue(key, out var v) ? v switch
            {
                DoubleValue dv => (decimal)dv.Value,
                IntValue iv => iv.Value,
                StringValue sv when decimal.TryParse(sv.Value, out var d) => d,
                _ => null
            } : null;
    }

    /// <summary>
    /// Typed gen_ai.* data extracted from span attributes.
    /// Maps to OTel semantic conventions: https://opentelemetry.io/docs/specs/semconv/gen-ai/
    /// </summary>
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
    }

    /// <summary>
    /// SpanModel enriched with extracted gen_ai data.
    /// </summary>
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
        public string? GenAiModel => GenAi?.RequestModel ?? GenAi?.ResponseModel;
        public long GenAiInputTokens => GenAi?.InputTokens ?? 0;
        public long GenAiOutputTokens => GenAi?.OutputTokens ?? 0;
        public decimal GenAiCostUsd => GenAi?.CostUsd ?? 0;
        public string? SessionId => GenAi?.SessionId;
        public bool IsGenAiSpan => GenAi is not null;
    }

    /// <summary>
    /// gen_ai.* semantic convention attribute keys.
    /// https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/
    /// </summary>
    public static class GenAiAttributes
    {
        public const string System = "gen_ai.system";
        public const string RequestModel = "gen_ai.request.model";
        public const string RequestTemperature = "gen_ai.request.temperature";
        public const string RequestMaxTokens = "gen_ai.request.max_tokens";
        public const string RequestTopP = "gen_ai.request.top_p";
        public const string RequestStopSequences = "gen_ai.request.stop_sequences";
        public const string ResponseModel = "gen_ai.response.model";
        public const string ResponseFinishReason = "gen_ai.response.finish_reason";
        public const string ResponseId = "gen_ai.response.id";
        public const string UsageInputTokens = "gen_ai.usage.input_tokens";
        public const string UsageOutputTokens = "gen_ai.usage.output_tokens";
        public const string UsageTotalTokens = "gen_ai.usage.total_tokens";
        public const string UsagePromptTokens = "gen_ai.usage.prompt_tokens";
        public const string UsageCompletionTokens = "gen_ai.usage.completion_tokens";
        public const string ConversationId = "gen_ai.conversation.id";
        public const string ToolName = "gen_ai.tool.name";
        public const string ToolCallId = "gen_ai.tool.call.id";
        public const string ToolDescription = "gen_ai.tool.description";
        public const string Prompt = "gen_ai.prompt";
        public const string Completion = "gen_ai.completion";
    }

    /// <summary>
    /// qyl-specific extension attributes.
    /// </summary>
    public static class QylAttributes
    {
        public const string CostUsd = "qyl.cost.usd";
        public const string CostCurrency = "qyl.cost.currency";
        public const string SessionId = "qyl.session.id";
        public const string SessionName = "qyl.session.name";
        public const string FeedbackScore = "qyl.feedback.score";
        public const string FeedbackComment = "qyl.feedback.comment";
        public const string AgentId = "qyl.agent.id";
        public const string AgentName = "qyl.agent.name";
        public const string AgentRole = "qyl.agent.role";
    }
