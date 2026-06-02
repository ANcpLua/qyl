namespace Qyl.Collector.Search;

[QylService(QylLifetime.Singleton)]
public sealed partial class SearchService(DuckDbStore store, ILogger<SearchService> logger)
{

    public async Task<IReadOnlyList<SearchDocumentResult>> SearchDocumentsAsync(
        string queryText,
        string? entityType = null,
        string? projectId = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return [];

        var likePattern = $"%{SqlLikeEscape.Escape(queryText)}%";

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

        var results = await store.ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT d.id, d.entity_type, d.entity_id, d.title, d.body, d.url,"
                              + " d.tags_json, d.boost, d.indexed_at,"
                              + " COALESCE(t.score, 0.0) + d.boost AS relevance_score"
                              + " FROM search_documents d"
                              + " LEFT JOIN ("
                              + "     SELECT document_id, SUM(term_frequency) AS score"
                              + "     FROM search_terms WHERE term ILIKE $1 ESCAPE '\\'"
                              + "     GROUP BY document_id"
                              + " ) t ON t.document_id = d.id"
                              + " WHERE (d.title ILIKE $1 ESCAPE '\\' OR d.body ILIKE $1 ESCAPE '\\' OR t.score IS NOT NULL)"
                              + " " + additionalWhere
                              + " ORDER BY relevance_score DESC, d.updated_at DESC"
                              + " LIMIT $" + paramIndex.ToString(CultureInfo.InvariantCulture);

            cmd.Parameters.AddRange(parameters);
            cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 200) });

            var rows = new List<SearchDocumentResult>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new SearchDocumentResult
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

            return rows;
        }, ct).ConfigureAwait(false);

        await LogQueryAuditAsync(queryText, entityType, projectId, results.Count, ct).ConfigureAwait(false);

        LogSearchExecuted(queryText, results.Count);
        return results;
    }


    public async Task<IReadOnlyList<SearchTermSuggestion>> GetSuggestionsAsync(
        string prefix,
        int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length < 2)
            return [];

        var likePattern = $"{SqlLikeEscape.Escape(prefix)}%";

        return await store.ExecuteReadAsync<IReadOnlyList<SearchTermSuggestion>>(con =>
        {
            using var cmd = con.CreateCommand();
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
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                suggestions.Add(new SearchTermSuggestion
                {
                    Term = reader.GetString(0), Field = reader.GetString(1), Frequency = reader.GetInt32(2)
                });
            }

            return suggestions;
        }, ct).ConfigureAwait(false);
    }


    public async Task RecordClickAsync(
        string queryAuditId,
        string clickedResultId,
        int clickedPosition,
        CancellationToken ct = default) =>
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
                        : DBNull.Value
                });
                cmd.Parameters.Add(new DuckDBParameter { Value = projectId ?? (object)DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = resultCount });
                cmd.Parameters.Add(new DuckDBParameter { Value = now });
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            Debug.WriteLine(ex);
        }
        catch (DuckDBException ex)
        {
            LogAuditFailed(ex);
        }
    }


    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Search executed: '{QueryText}' returned {ResultCount} results")]
    private partial void LogSearchExecuted(string queryText, int resultCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Audit logging failed")]
    private partial void LogAuditFailed(Exception ex);
}


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

public sealed record SearchTermSuggestion
{
    public required string Term { get; init; }
    public required string Field { get; init; }
    public required int Frequency { get; init; }
}
