namespace qyl.collector.Search;

/// <summary>
///     REST endpoints for unified cross-entity search.
/// </summary>
public static class SearchEndpoints
{
    /// <summary>
    ///     Maps search endpoints: POST /api/v1/search/query and GET /api/v1/search/suggestions.
    /// </summary>
    public static void MapSearchEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/search/query", static async (
            SearchQuery query, DuckDbStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(query.Text))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["text"] = ["Search text is required."]
                });
            }

            var results = await store.SearchAsync(query, ct).ConfigureAwait(false);
            return Results.Ok(new { items = results, total = results.Count });
        });

        app.MapGet("/api/v1/search/suggestions", static async (
            string? prefix, DuckDbStore store, CancellationToken ct) =>
        {
            var suggestions = await store.GetSuggestionsAsync(prefix ?? "", ct).ConfigureAwait(false);
            return Results.Ok(new { items = suggestions, total = suggestions.Count });
        });
    }
}

/// <summary>
///     Unified search query across all entity types.
/// </summary>
public sealed record SearchQuery(
    string Text,
    string[]? EntityTypes = null,
    DateTime? StartTime = null,
    DateTime? EndTime = null,
    int Limit = 20);

/// <summary>
///     A single search result from any entity type.
/// </summary>
public sealed record SearchResult(
    string EntityType,
    string EntityId,
    string Title,
    string? Snippet,
    DateTime Timestamp,
    double Score);

/// <summary>
///     Autocomplete suggestion from recent data.
/// </summary>
public sealed record SearchSuggestion(
    string Text,
    string EntityType,
    int Count);
