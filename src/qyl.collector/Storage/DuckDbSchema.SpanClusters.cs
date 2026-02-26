// =============================================================================
// Manual schema extension for semantic span clusters.
// Not yet in the TypeSpec model — span_clusters stores per-span cluster
// assignments produced by EmbeddingClusterWorker.
// =============================================================================

namespace qyl.collector.Storage;

public static partial class DuckDbSchema
{
    public const string SpanClustersDdl = """
                                          CREATE TABLE IF NOT EXISTS span_clusters (
                                              span_id      VARCHAR NOT NULL PRIMARY KEY,
                                              cluster_id   INTEGER NOT NULL,
                                              cluster_label VARCHAR NOT NULL,
                                              distance     DOUBLE  NOT NULL DEFAULT 0.0,
                                              model_version VARCHAR NOT NULL,
                                              computed_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                                          );
                                          CREATE INDEX IF NOT EXISTS idx_span_clusters_label
                                              ON span_clusters(cluster_label);
                                          """;
}
