namespace Qyl.Collector.Storage;

public static partial class DuckDbSchema
{
    /// <summary>
    ///     Per-span projection used by the conversation thread view (GET /api/v1/conversations/{sessionId}).
    ///     Filter is intentionally broad: any span that participates in a GenAI exchange — LLM call, tool
    ///     invocation, resource read, prompt fetch — qualifies. Capture-gate-controlled payload attributes
    ///     (tool arguments, tool results, prompt arguments, message content) ride on attributes_json and are
    ///     filtered out by the endpoint when the OTel / qyl capture flags are off.
    /// </summary>
    public const string ConversationsViewDdl = """
                                                CREATE OR REPLACE VIEW qyl_conversations AS
                                                SELECT
                                                    session_id,
                                                    trace_id,
                                                    span_id,
                                                    parent_span_id,
                                                    name,
                                                    start_time_unix_nano,
                                                    end_time_unix_nano,
                                                    duration_ns,
                                                    service_name,
                                                    gen_ai_provider_name,
                                                    gen_ai_request_model,
                                                    gen_ai_response_model,
                                                    gen_ai_input_tokens,
                                                    gen_ai_output_tokens,
                                                    gen_ai_tool_name,
                                                    gen_ai_tool_call_id,
                                                    gen_ai_cost_usd,
                                                    status_code,
                                                    status_message,
                                                    attributes_json
                                                FROM spans
                                                WHERE session_id IS NOT NULL
                                                  AND (
                                                    gen_ai_request_model IS NOT NULL
                                                    OR gen_ai_tool_name IS NOT NULL
                                                    OR name LIKE 'execute_tool%'
                                                    OR name LIKE 'mcp.tool.%'
                                                    OR name LIKE 'mcp.resource.%'
                                                    OR name LIKE 'mcp.prompt.%'
                                                  );
                                                """;
}
