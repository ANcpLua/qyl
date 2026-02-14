namespace qyl.collector.Search;

/// <summary>
///     Simple text-search indexer for DuckDB.
///     DuckDB does not need a separate full-text index — <c>ILIKE</c> and <c>contains()</c>
///     operate directly on columnar storage with acceptable performance for moderate datasets.
///     Time-windowed queries keep scan sizes manageable.
/// </summary>
internal static class SearchIndexer
{
    /// <summary>
    ///     No-op: DuckDB handles text matching inline via ILIKE with time-window constraints.
    ///     This class exists as a documentation placeholder and future extension point
    ///     for FTS5-style indexing if query volumes warrant it.
    /// </summary>
    public static void EnsureReady()
    {
        // Intentionally empty — ILIKE-based search requires no index setup.
    }
}
