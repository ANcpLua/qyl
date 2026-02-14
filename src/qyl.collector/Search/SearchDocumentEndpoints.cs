using Microsoft.AspNetCore.Mvc;

namespace qyl.collector.Search;

/// <summary>
///     Minimal API endpoints for document-indexed search.
///     Routes: <c>/api/v1/search/*</c>
/// </summary>
public static class SearchDocumentEndpoints
{
    /// <summary>
    ///     Maps document search, suggestion, and click audit endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapSearchDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/search")
            .WithTags("Search");

        group.MapGet("/documents", static async (
            [FromServices] SearchService service,
            string? q, string? entityType, string? projectId, int? limit,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["q"] = ["Search query text is required."]
                });

            var results = await service.SearchDocumentsAsync(
                q, entityType, projectId,
                Math.Clamp(limit ?? 20, 1, 200), ct).ConfigureAwait(false);
            return Results.Ok(new { items = results, total = results.Count });
        })
        .WithName("SearchDocuments")
        .WithSummary("Full-text search across indexed documents with relevance scoring");

        group.MapGet("/terms/suggestions", static async (
            [FromServices] SearchService service,
            string? prefix, int? limit,
            CancellationToken ct) =>
        {
            var suggestions = await service.GetSuggestionsAsync(
                prefix ?? "", Math.Clamp(limit ?? 20, 1, 50), ct).ConfigureAwait(false);
            return Results.Ok(new { items = suggestions, total = suggestions.Count });
        })
        .WithName("GetSearchTermSuggestions")
        .WithSummary("Autocomplete suggestions from the search term index");

        group.MapPost("/clicks", static async (
            SearchClickRequest body, [FromServices] SearchService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.QueryAuditId) ||
                string.IsNullOrWhiteSpace(body.ClickedResultId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["body"] = ["queryAuditId and clickedResultId are required."]
                });
            }

            await service.RecordClickAsync(
                body.QueryAuditId, body.ClickedResultId, body.ClickedPosition,
                ct).ConfigureAwait(false);
            return Results.Accepted();
        })
        .WithName("RecordSearchClick")
        .WithSummary("Record a search result click for relevance tuning");

        return endpoints;
    }
}

/// <summary>Request body for recording a search result click.</summary>
public sealed record SearchClickRequest(
    string? QueryAuditId,
    string? ClickedResultId,
    int ClickedPosition = 0);
