// =============================================================================
// Manual schema extensions for triage pipeline persistence.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string TriageResultsDdl = """
                                           CREATE TABLE IF NOT EXISTS triage_results (
                                               triage_id VARCHAR PRIMARY KEY,
                                               issue_id VARCHAR NOT NULL,
                                               fixability_score DOUBLE NOT NULL,
                                               automation_level VARCHAR NOT NULL DEFAULT 'skip',
                                               ai_summary VARCHAR,
                                               root_cause_hypothesis VARCHAR,
                                               triggered_by VARCHAR NOT NULL DEFAULT 'new_issue',
                                               fix_run_id VARCHAR,
                                               scoring_method VARCHAR NOT NULL DEFAULT 'heuristic',
                                               created_at TIMESTAMP DEFAULT now()
                                           );
                                           CREATE INDEX IF NOT EXISTS idx_triage_issue ON triage_results(issue_id);
                                           CREATE INDEX IF NOT EXISTS idx_triage_automation ON triage_results(automation_level);
                                           CREATE INDEX IF NOT EXISTS idx_triage_created ON triage_results(created_at DESC);
                                           """;
}
