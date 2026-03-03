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
                                           metadata_json VARCHAR,
                                           track_mode VARCHAR DEFAULT 'auto',
                                           approval_status VARCHAR DEFAULT 'not_required',
                                           evidence_count INTEGER DEFAULT 0
                                       );
                                       ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS track_mode VARCHAR DEFAULT 'auto';
                                       ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS approval_status VARCHAR DEFAULT 'not_required';
                                       ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS evidence_count INTEGER DEFAULT 0;
                                       CREATE INDEX IF NOT EXISTS idx_agent_runs_trace_id ON agent_runs(trace_id);
                                       CREATE INDEX IF NOT EXISTS idx_agent_runs_agent_name ON agent_runs(agent_name);
                                       CREATE INDEX IF NOT EXISTS idx_agent_runs_track_mode ON agent_runs(track_mode);
                                       CREATE INDEX IF NOT EXISTS idx_agent_runs_approval_status ON agent_runs(approval_status);
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

    public const string AgentDecisionsDdl = """
                                            CREATE TABLE IF NOT EXISTS agent_decisions (
                                                decision_id VARCHAR PRIMARY KEY,
                                                run_id VARCHAR,
                                                trace_id VARCHAR,
                                                decision_type VARCHAR,
                                                outcome VARCHAR,
                                                requires_approval BOOLEAN DEFAULT FALSE,
                                                approval_status VARCHAR DEFAULT 'not_required',
                                                reason VARCHAR,
                                                evidence_json VARCHAR,
                                                metadata_json VARCHAR,
                                                created_at_unix_nano UBIGINT
                                            );
                                            CREATE INDEX IF NOT EXISTS idx_agent_decisions_run_id ON agent_decisions(run_id);
                                            CREATE INDEX IF NOT EXISTS idx_agent_decisions_trace_id ON agent_decisions(trace_id);
                                            CREATE INDEX IF NOT EXISTS idx_agent_decisions_type ON agent_decisions(decision_type);
                                            CREATE INDEX IF NOT EXISTS idx_agent_decisions_approval_status ON agent_decisions(approval_status);
                                            CREATE INDEX IF NOT EXISTS idx_agent_decisions_created_at ON agent_decisions(created_at_unix_nano DESC);
                                            """;
}
