// =============================================================================
// Manual schema extensions for provisioning (config selections + generation jobs).
// The base DuckDbSchema.g.cs is auto-generated from TypeSpec; this partial
// adds tables not yet in the TypeSpec model.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string ConfigSelectionsDdl = """
                                              CREATE TABLE IF NOT EXISTS config_selections (
                                                  workspace_id VARCHAR PRIMARY KEY,
                                                  profile_id VARCHAR NOT NULL,
                                                  custom_overrides VARCHAR,
                                                  updated_at TIMESTAMP DEFAULT now()
                                              );
                                              """;

    public const string GenerationJobsDdl = """
                                            CREATE TABLE IF NOT EXISTS generation_jobs (
                                                job_id VARCHAR PRIMARY KEY,
                                                workspace_id VARCHAR,
                                                profile_id VARCHAR,
                                                status VARCHAR DEFAULT 'pending',
                                                output_url VARCHAR,
                                                error_message VARCHAR,
                                                created_at TIMESTAMP DEFAULT now(),
                                                completed_at TIMESTAMP
                                            );
                                            CREATE INDEX IF NOT EXISTS idx_generation_jobs_workspace ON generation_jobs(workspace_id);
                                            """;
}
