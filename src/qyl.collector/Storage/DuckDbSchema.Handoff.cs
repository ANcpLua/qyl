namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string AgentHandoffsDdl = """
                                           CREATE TABLE IF NOT EXISTS agent_handoffs (
                                               handoff_id VARCHAR PRIMARY KEY,
                                               run_id VARCHAR NOT NULL,
                                               agent_type VARCHAR NOT NULL,
                                               status VARCHAR DEFAULT 'pending',
                                               context_json VARCHAR,
                                               result_json VARCHAR,
                                               error_message VARCHAR,
                                               accepted_at TIMESTAMP,
                                               submitted_at TIMESTAMP,
                                               failed_at TIMESTAMP,
                                               timeout_at TIMESTAMP,
                                               created_at TIMESTAMP DEFAULT now()
                                           );
                                           CREATE INDEX IF NOT EXISTS idx_handoffs_run ON agent_handoffs(run_id);
                                           CREATE INDEX IF NOT EXISTS idx_handoffs_status ON agent_handoffs(status);
                                           CREATE INDEX IF NOT EXISTS idx_handoffs_agent ON agent_handoffs(agent_type);
                                           """;
}
