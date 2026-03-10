// =============================================================================
// Manual schema extensions for provisioning (config selections).
// The base DuckDbSchema.g.cs is auto-generated from TypeSpec; this partial
// adds tables not yet in the TypeSpec model.
// GenerationJobsDdl moved to DuckDbSchema.g.cs.
// =============================================================================

namespace Qyl.Collector.Storage;

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
}
