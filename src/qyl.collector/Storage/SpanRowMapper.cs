// =============================================================================
// SpanRowMapper - Unified span row mapping utilities
// Single source of truth for DbDataReader -> SpanStorageRow conversion
// =============================================================================

namespace qyl.collector.Storage;

/// <summary>
///     Shared mapper for SpanStorageRow from DbDataReader.
///     Provides both ordinal-based (performance) and name-based (flexibility) variants.
/// </summary>
/// <remarks>
///     <para>Use <see cref="MapByOrdinal" /> in DuckDbStore where column order is guaranteed by SELECT.</para>
///     <para>Use <see cref="MapByName" /> in SessionQueryService where SpanQueryBuilder generates dynamic SELECT.</para>
/// </remarks>
public static class SpanRowMapper
{
    /// <summary>
    ///     Maps a span row using ordinal-based column access.
    ///     Use when column order is guaranteed (e.g., SELECT with fixed column order).
    /// </summary>
    /// <remarks>
    ///     Expected column order matches SelectSpanColumns in DuckDbStore:
    ///     span_id(0), trace_id(1), parent_span_id(2), session_id(3),
    ///     name(4), kind(5), start_time_unix_nano(6), end_time_unix_nano(7), duration_ns(8),
    ///     status_code(9), status_message(10), service_name(11),
    ///     gen_ai_system(12), gen_ai_request_model(13), gen_ai_response_model(14),
    ///     gen_ai_input_tokens(15), gen_ai_output_tokens(16), gen_ai_temperature(17),
    ///     gen_ai_stop_reason(18), gen_ai_tool_name(19), gen_ai_tool_call_id(20),
    ///     gen_ai_cost_usd(21), attributes_json(22), resource_json(23), created_at(24)
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanStorageRow MapByOrdinal(DbDataReader reader) =>
        new()
        {
            SpanId = reader.GetString(0),
            TraceId = reader.GetString(1),
            ParentSpanId = reader.Col(2).AsString,
            SessionId = reader.Col(3).AsString,
            Name = reader.GetString(4),
            Kind = reader.Col(5).GetByte(0),
            StartTimeUnixNano = reader.Col(6).GetUInt64(0),
            EndTimeUnixNano = reader.Col(7).GetUInt64(0),
            DurationNs = reader.Col(8).GetUInt64(0),
            StatusCode = reader.Col(9).GetByte(0),
            StatusMessage = reader.Col(10).AsString,
            ServiceName = reader.Col(11).AsString,
            GenAiSystem = reader.Col(12).AsString,
            GenAiRequestModel = reader.Col(13).AsString,
            GenAiResponseModel = reader.Col(14).AsString,
            GenAiInputTokens = reader.Col(15).AsInt64,
            GenAiOutputTokens = reader.Col(16).AsInt64,
            GenAiTemperature = reader.Col(17).AsDouble,
            GenAiStopReason = reader.Col(18).AsString,
            GenAiToolName = reader.Col(19).AsString,
            GenAiToolCallId = reader.Col(20).AsString,
            GenAiCostUsd = reader.Col(21).AsDouble,
            AttributesJson = reader.Col(22).AsString,
            ResourceJson = reader.Col(23).AsString,
            CreatedAt = reader.Col(24).AsDateTimeOffset
        };

    /// <summary>
    ///     Maps a span row using name-based column access.
    ///     Use when column order may vary (e.g., SpanQueryBuilder dynamic SELECT).
    /// </summary>
    /// <remarks>
    ///     Slightly slower than ordinal-based due to GetOrdinal lookups,
    ///     but more robust against schema evolution.
    /// </remarks>
    public static SpanStorageRow MapByName(DbDataReader reader) =>
        new()
        {
            SpanId = reader.GetString(reader.GetOrdinal("span_id")),
            TraceId = reader.GetString(reader.GetOrdinal("trace_id")),
            ParentSpanId = reader.Col("parent_span_id").AsString,
            SessionId = reader.Col("session_id").AsString,
            Name = reader.GetString(reader.GetOrdinal("name")),
            Kind = reader.Col("kind").GetByte(0),
            StartTimeUnixNano = reader.Col("start_time_unix_nano").GetUInt64(0),
            EndTimeUnixNano = reader.Col("end_time_unix_nano").GetUInt64(0),
            DurationNs = reader.Col("duration_ns").GetUInt64(0),
            StatusCode = reader.Col("status_code").GetByte(0),
            StatusMessage = reader.Col("status_message").AsString,
            ServiceName = reader.Col("service_name").AsString,
            GenAiSystem = reader.Col("gen_ai_system").AsString,
            GenAiRequestModel = reader.Col("gen_ai_request_model").AsString,
            GenAiResponseModel = reader.Col("gen_ai_response_model").AsString,
            GenAiInputTokens = reader.Col("gen_ai_input_tokens").AsInt64,
            GenAiOutputTokens = reader.Col("gen_ai_output_tokens").AsInt64,
            GenAiTemperature = reader.Col("gen_ai_temperature").AsDouble,
            GenAiStopReason = reader.Col("gen_ai_stop_reason").AsString,
            GenAiToolName = reader.Col("gen_ai_tool_name").AsString,
            GenAiToolCallId = reader.Col("gen_ai_tool_call_id").AsString,
            GenAiCostUsd = reader.Col("gen_ai_cost_usd").AsDouble,
            AttributesJson = reader.Col("attributes_json").AsString,
            ResourceJson = reader.Col("resource_json").AsString
        };
}
