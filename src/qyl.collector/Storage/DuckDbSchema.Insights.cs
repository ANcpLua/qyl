// =============================================================================
// Manual schema extensions for materialized insights persistence.
// The base DuckDbSchema.g.cs is auto-generated from TypeSpec; this partial
// adds tables not yet in the TypeSpec model.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string MaterializedInsightsDdl = """
        CREATE TABLE IF NOT EXISTS materialized_insights (
            tier VARCHAR NOT NULL PRIMARY KEY,
            content_markdown VARCHAR NOT NULL,
            content_hash VARCHAR(64) NOT NULL,
            materialized_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            span_count_at_materialization BIGINT NOT NULL DEFAULT 0,
            duration_ms DOUBLE NOT NULL DEFAULT 0
        );
        """;
}
