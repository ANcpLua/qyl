// =============================================================================
// Manual schema extensions for coding agent runs and Loom settings persistence.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string CodingAgentRunsDdl = """
                                             CREATE TABLE IF NOT EXISTS coding_agent_runs (
                                                 id VARCHAR PRIMARY KEY,
                                                 fix_run_id VARCHAR NOT NULL,
                                                 provider VARCHAR NOT NULL,
                                                 status VARCHAR NOT NULL DEFAULT 'pending',
                                                 agent_url VARCHAR,
                                                 pr_url VARCHAR,
                                                 repo_full_name VARCHAR,
                                                 created_at TIMESTAMP DEFAULT now(),
                                                 completed_at TIMESTAMP
                                             );
                                             CREATE INDEX IF NOT EXISTS idx_car_fix_run ON coding_agent_runs(fix_run_id);
                                             CREATE INDEX IF NOT EXISTS idx_car_status ON coding_agent_runs(status);
                                             CREATE INDEX IF NOT EXISTS idx_car_created ON coding_agent_runs(created_at DESC);
                                             """;

    public const string LoomSettingsDdl = """
                                          CREATE TABLE IF NOT EXISTS Loom_settings (
                                              id VARCHAR PRIMARY KEY DEFAULT 'default',
                                              default_coding_agent VARCHAR DEFAULT 'Loom',
                                              default_coding_agent_integration_id VARCHAR,
                                              automation_tuning VARCHAR DEFAULT 'medium',
                                              updated_at TIMESTAMP DEFAULT now()
                                          );
                                          """;
}
