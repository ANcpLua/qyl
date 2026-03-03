// =============================================================================
// Manual schema extensions for autofix step tracking within fix runs.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string AutofixStepsDdl = """
                                          CREATE TABLE IF NOT EXISTS autofix_steps (
                                              step_id VARCHAR PRIMARY KEY,
                                              run_id VARCHAR NOT NULL,
                                              step_number INTEGER NOT NULL,
                                              step_name VARCHAR NOT NULL,
                                              status VARCHAR DEFAULT 'pending',
                                              input_json VARCHAR,
                                              output_json VARCHAR,
                                              error_message VARCHAR,
                                              started_at TIMESTAMP,
                                              completed_at TIMESTAMP,
                                              created_at TIMESTAMP DEFAULT now()
                                          );
                                          CREATE INDEX IF NOT EXISTS idx_autofix_steps_run ON autofix_steps(run_id);
                                          CREATE INDEX IF NOT EXISTS idx_autofix_steps_status ON autofix_steps(status);
                                          """;
}
