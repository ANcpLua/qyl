// =============================================================================
// Manual schema extensions for autofix fix_runs persistence.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string FixRunsDdl = """
                                     CREATE TABLE IF NOT EXISTS fix_runs (
                                         run_id VARCHAR PRIMARY KEY,
                                         issue_id VARCHAR NOT NULL,
                                         execution_id VARCHAR,
                                         status VARCHAR DEFAULT 'pending',
                                         policy VARCHAR DEFAULT 'require_review',
                                         fix_description VARCHAR,
                                         confidence_score DOUBLE,
                                         changes_json VARCHAR,
                                         created_at TIMESTAMP DEFAULT now(),
                                         completed_at TIMESTAMP
                                     );
                                     CREATE INDEX IF NOT EXISTS idx_fix_runs_issue ON fix_runs(issue_id);
                                     CREATE INDEX IF NOT EXISTS idx_fix_runs_status ON fix_runs(status);
                                     CREATE INDEX IF NOT EXISTS idx_fix_runs_created ON fix_runs(created_at DESC);
                                     """;
}
