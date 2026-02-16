// =============================================================================
// Manual schema extensions for agent run and tool call persistence.
// The base DuckDbSchema.g.cs is auto-generated from TypeSpec; this partial
// adds tables not yet in the TypeSpec model.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string AgentRunsDdl = """
                                       CREATE TABLE IF NOT EXISTS agent_runs (
                                           run_id VARCHAR PRIMARY KEY,
                                           trace_id VARCHAR,
                                           parent_run_id VARCHAR,
                                           agent_name VARCHAR,
                                           agent_type VARCHAR,
                                           model VARCHAR,
                                           provider VARCHAR,
                                           status VARCHAR DEFAULT 'running',
                                           input_tokens BIGINT DEFAULT 0,
                                           output_tokens BIGINT DEFAULT 0,
                                           total_cost DOUBLE DEFAULT 0,
                                           tool_call_count INTEGER DEFAULT 0,
                                           start_time UBIGINT,
                                           end_time UBIGINT,
                                           duration_ns BIGINT,
                                           error_message VARCHAR,
                                           metadata_json VARCHAR
                                       );
                                       CREATE INDEX IF NOT EXISTS idx_agent_runs_trace_id ON agent_runs(trace_id);
                                       CREATE INDEX IF NOT EXISTS idx_agent_runs_agent_name ON agent_runs(agent_name);
                                       """;

    public const string ToolCallsDdl = """
                                       CREATE TABLE IF NOT EXISTS tool_calls (
                                           call_id VARCHAR PRIMARY KEY,
                                           run_id VARCHAR,
                                           trace_id VARCHAR,
                                           span_id VARCHAR,
                                           tool_name VARCHAR,
                                           tool_type VARCHAR,
                                           arguments_json VARCHAR,
                                           result_json VARCHAR,
                                           status VARCHAR DEFAULT 'running',
                                           start_time UBIGINT,
                                           end_time UBIGINT,
                                           duration_ns BIGINT,
                                           error_message VARCHAR,
                                           sequence_number INTEGER DEFAULT 0
                                       );
                                       CREATE INDEX IF NOT EXISTS idx_tool_calls_run_id ON tool_calls(run_id);
                                       CREATE INDEX IF NOT EXISTS idx_tool_calls_trace_id ON tool_calls(trace_id);
                                       CREATE INDEX IF NOT EXISTS idx_tool_calls_tool_name ON tool_calls(tool_name);
                                       """;
}
