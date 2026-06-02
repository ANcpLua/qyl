namespace Qyl.Collector.Search;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/search/query", static async Task<IResult> (
            SearchQuery query, DuckDbStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(query.Text))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["text"] = ["Search text is required."]
                });
            }

            var results = await store.SearchAsync(query, ct).ConfigureAwait(false);
            return TypedResults.Ok(new { items = results, total = results.Count });
        });

        app.MapGet("/api/v1/search/suggestions", static async (
            string? prefix, DuckDbStore store, CancellationToken ct) =>
        {
            var suggestions = await store.GetSuggestionsAsync(prefix ?? "", ct).ConfigureAwait(false);
            return TypedResults.Ok(new { items = suggestions, total = suggestions.Count });
        });
    }
}

public sealed record SearchQuery(
    string Text,
    string[]? EntityTypes = null,
    DateTime? StartTime = null,
    DateTime? EndTime = null,
    int Limit = 20);

public sealed record SearchResult(
    string EntityType,
    string EntityId,
    string Title,
    string? Snippet,
    DateTime Timestamp,
    double Score);

public sealed record SearchSuggestion(
    string Text,
    string EntityType,
    int Count);
