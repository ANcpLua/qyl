
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
