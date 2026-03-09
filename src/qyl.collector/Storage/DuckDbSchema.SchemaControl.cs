namespace qyl.collector.Storage;

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
            created_at TIMESTAMP DEFAULT now()
        );
        """;
}
