namespace Qyl.Collector.Storage;

public static partial class DuckDbSchema
{
    public const string SchemaPromotionsDdl = """
                                              CREATE TABLE IF NOT EXISTS schema_promotions (
                                                  id VARCHAR PRIMARY KEY,
                                                  profile_id VARCHAR,
                                                  source_attribute VARCHAR NOT NULL,
                                                  target_column VARCHAR,
                                                  target_type VARCHAR,
                                                  target_table VARCHAR NOT NULL,
                                                  status VARCHAR DEFAULT 'pending',
                                                  applied_at TIMESTAMP,
                                                  created_at TIMESTAMP DEFAULT now(),
                                                  sql_statements VARCHAR NOT NULL DEFAULT ''
                                              );
                                              ALTER TABLE schema_promotions
                                                  ADD COLUMN IF NOT EXISTS sql_statements VARCHAR;
                                              UPDATE schema_promotions
                                                  SET sql_statements = ''
                                                  WHERE sql_statements IS NULL;
                                              """;
}
