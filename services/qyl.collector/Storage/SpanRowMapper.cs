
namespace Qyl.Collector.Storage;

public static class SpanRowMapper
{
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
            GenAiProviderName = reader.Col("gen_ai_provider_name").AsString,
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
            ResourceJson = reader.Col("resource_json").AsString,
            BaggageJson = reader.Col("baggage_json").AsString,
            SchemaUrl = reader.Col("schema_url").AsString
        };
}
