namespace qyl.collector.Search;

/// <summary>
///     Service layer for DuckDB full-text search. Operates against the
///     <c>search_documents</c>, <c>search_terms</c>, and <c>search_query_audit</c>
///     tables for document-indexed search with relevance scoring.
/// </summary>
public sealed partial class SearchService(DuckDbStore store, ILogger<SearchService> logger)
{
    // ==========================================================================
    // Full-Text Search
    // ==========================================================================

    /// <summary>
    ///     Searches across indexed documents using text matching with relevance scoring.
    ///     Results combine term-frequency scoring from the inverted index with
    ///     document boost factors.
    /// </summary>
    public async Task<IReadOnlyList<SearchDocumentResult>> SearchDocumentsAsync(
        string queryText,
        string? entityType = null,
        string? projectId = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return [];

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        var escapedQuery = queryText
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        var likePattern = $"%{escapedQuery}%";

        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter> { new() { Value = likePattern } };
        var paramIndex = 2;

        if (!string.IsNullOrEmpty(entityType))
        {
            conditions.Add($"d.entity_type = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = entityType });
        }

        if (!string.IsNullOrEmpty(projectId))
        {
            conditions.Add($"d.project_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = projectId });
        }

        var additionalWhere = conditions.Count > 0 ? $"AND {string.Join(" AND ", conditions)}" : "";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT d.id, d.entity_type, d.entity_id, d.title, d.body, d.url,
                   d.tags_json, d.boost, d.indexed_at,
                   COALESCE(t.score, 0.0) + d.boost AS relevance_score
            FROM search_documents d
            LEFT JOIN (
                SELECT document_id, SUM(term_frequency) AS score
                FROM search_terms
                WHERE term ILIKE $1 ESCAPE '\\'
                GROUP BY document_id
            ) t ON t.document_id = d.id
            WHERE (d.title ILIKE $1 ESCAPE '\\' OR d.body ILIKE $1 ESCAPE '\\' OR t.score IS NOT NULL)
            {additionalWhere}
            ORDER BY relevance_score DESC, d.updated_at DESC
            LIMIT ${paramIndex}
            """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 200) });

        var results = new List<SearchDocumentResult>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new SearchDocumentResult
            {
                Id = reader.GetString(0),
                EntityType = reader.GetString(1),
                EntityId = reader.GetString(2),
                Title = reader.GetString(3),
                Body = reader.Col(4).AsString,
                Url = reader.Col(5).AsString,
                TagsJson = reader.Col(6).AsString,
                Boost = reader.GetDouble(7),
                IndexedAt = reader.GetDateTime(8),
                RelevanceScore = reader.GetDouble(9)
            });
        }

        // Log the query for audit (fire-and-forget, non-blocking)
        _ = LogQueryAuditAsync(queryText, entityType, projectId, results.Count, ct);

        LogSearchExecuted(queryText, results.Count);
        return results;
    }

    // ==========================================================================
    // Suggestions
    // ==========================================================================

    /// <summary>
    ///     Returns autocomplete suggestions based on indexed search terms.
    /// </summary>
    public async Task<IReadOnlyList<SearchTermSuggestion>> GetSuggestionsAsync(
        string prefix,
        int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length < 2)
            return [];

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        var escapedPrefix = prefix
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        var likePattern = $"{escapedPrefix}%";

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT term, field, SUM(term_frequency) AS total_freq
            FROM search_terms
            WHERE term ILIKE $1 ESCAPE '\'
            GROUP BY term, field
            ORDER BY total_freq DESC
            LIMIT $2
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = likePattern });
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 50) });

        var suggestions = new List<SearchTermSuggestion>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            suggestions.Add(new SearchTermSuggestion
            {
                Term = reader.GetString(0),
                Field = reader.GetString(1),
                Frequency = reader.GetInt32(2)
            });
        }

        return suggestions;
    }

    // ==========================================================================
    // Query Audit
    // ==========================================================================

    /// <summary>
    ///     Records a search query in the audit log. Records clicks when a user
    ///     selects a result, enabling relevance tuning.
    /// </summary>
    public async Task RecordClickAsync(
        string queryAuditId,
        string clickedResultId,
        int clickedPosition,
        CancellationToken ct = default)
    {
        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                UPDATE search_query_audit SET
                    clicked_result_id = $1,
                    clicked_position = $2
                WHERE id = $3
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = clickedResultId });
            cmd.Parameters.Add(new DuckDBParameter { Value = clickedPosition });
            cmd.Parameters.Add(new DuckDBParameter { Value = queryAuditId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    // ==========================================================================
    // Private Methods
    // ==========================================================================

    private async Task LogQueryAuditAsync(
        string queryText,
        string? entityType,
        string? projectId,
        int resultCount,
        CancellationToken ct)
    {
        try
        {
            var auditId = Guid.NewGuid().ToString("N");
            var now = TimeProvider.System.GetUtcNow().UtcDateTime;

            await store.ExecuteWriteAsync(async (con, token) =>
            {
                await using var cmd = con.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO search_query_audit
                        (id, query_text, query_type, entity_types_json, project_id,
                         result_count, timestamp)
                    VALUES ($1, $2, 'text', $3, $4, $5, $6)
                    """;
                cmd.Parameters.Add(new DuckDBParameter { Value = auditId });
                cmd.Parameters.Add(new DuckDBParameter { Value = queryText });
                cmd.Parameters.Add(new DuckDBParameter
                {
                    Value = entityType is not null
                        ? JsonSerializer.Serialize(new[] { entityType })
                        : (object)DBNull.Value
                });
                cmd.Parameters.Add(new DuckDBParameter { Value = projectId ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = resultCount });
                cmd.Parameters.Add(new DuckDBParameter { Value = now });
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort audit logging -- never fail the search
        }
    }

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Search executed: '{QueryText}' returned {ResultCount} results")]
    private partial void LogSearchExecuted(string queryText, int resultCount);
}

// =============================================================================
// Search Storage Records
// =============================================================================

/// <summary>
///     Search result from the document index with relevance scoring.
/// </summary>
public sealed record SearchDocumentResult
{
    public required string Id { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Title { get; init; }
    public string? Body { get; init; }
    public string? Url { get; init; }
    public string? TagsJson { get; init; }
    public required double Boost { get; init; }
    public required DateTime IndexedAt { get; init; }
    public required double RelevanceScore { get; init; }
}

/// <summary>
///     Autocomplete suggestion from the inverted term index.
/// </summary>
public sealed record SearchTermSuggestion
{
    public required string Term { get; init; }
    public required string Field { get; init; }
    public required int Frequency { get; init; }
}
