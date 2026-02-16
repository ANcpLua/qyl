using qyl.collector.Search;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with unified cross-entity search operations.
/// </summary>
public sealed partial class DuckDbStore
{
    // ==========================================================================
    // Unified Search Operations
    // ==========================================================================

    /// <summary>
    ///     Searches across spans, logs, errors, agent runs, and workflows using text matching.
    ///     Results are ranked by relevance score and ordered by timestamp.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        SearchQuery query,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var (sql, parameters) = UnifiedQueryEngine.BuildQuery(query);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(parameters);

        var results = new List<SearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapSearchResult(reader));

        return results;
    }

    /// <summary>
    ///     Returns autocomplete suggestions by querying distinct values from key columns.
    /// </summary>
    public async Task<IReadOnlyList<SearchSuggestion>> GetSuggestionsAsync(
        string prefix,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length < 2)
            return [];

        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var escapedPrefix = prefix
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        var likePattern = $"{escapedPrefix}%";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT text, entity_type, cnt FROM (
                              SELECT name AS text, 'spans' AS entity_type, COUNT(*) AS cnt
                              FROM spans WHERE name ILIKE $1 ESCAPE '\'
                              GROUP BY name
                              UNION ALL
                              SELECT COALESCE(service_name, '') AS text, 'spans' AS entity_type, COUNT(*) AS cnt
                              FROM spans WHERE service_name ILIKE $1 ESCAPE '\'
                              GROUP BY service_name
                              UNION ALL
                              SELECT COALESCE(agent_name, '') AS text, 'agent_runs' AS entity_type, COUNT(*) AS cnt
                              FROM agent_runs WHERE agent_name ILIKE $1 ESCAPE '\'
                              GROUP BY agent_name
                              UNION ALL
                              SELECT COALESCE(workflow_name, '') AS text, 'workflows' AS entity_type, COUNT(*) AS cnt
                              FROM workflow_executions WHERE workflow_name ILIKE $1 ESCAPE '\'
                              GROUP BY workflow_name
                              UNION ALL
                              SELECT COALESCE(error_type, '') AS text, 'errors' AS entity_type, COUNT(*) AS cnt
                              FROM errors WHERE error_type ILIKE $1 ESCAPE '\'
                              GROUP BY error_type
                          ) AS suggestions
                          WHERE text != ''
                          ORDER BY cnt DESC
                          LIMIT 20
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = likePattern });

        var suggestions = new List<SearchSuggestion>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            suggestions.Add(new SearchSuggestion(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2)));
        }

        return suggestions;
    }

    // ==========================================================================
    // Private Methods - Search Result Mapping
    // ==========================================================================

    private static SearchResult MapSearchResult(IDataReader reader)
    {
        var tsRaw = reader.Col(4);
        var timestamp = tsRaw.AsUInt64 is { } nano
            ? DateTimeOffset.FromUnixTimeMilliseconds((long)(nano / 1_000_000)).UtcDateTime
            : TimeProvider.System.GetUtcNow().UtcDateTime;

        return new SearchResult(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.Col(3).AsString,
            timestamp,
            reader.Col(5).GetDouble(0));
    }
}
