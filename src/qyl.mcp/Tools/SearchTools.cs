using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for unified search across all telemetry entities.
/// </summary>
[McpServerToolType]
public sealed class SearchTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.search")]
    [Description("""
                 Unified search across all telemetry entities.

                 Searches spans, logs, issues, agent runs, and workflows
                 with a single query string. Results are ranked by relevance.

                 Supported entity types (comma-separated):
                 - spans, logs, issues, agents, workflows, all (default)

                 Example queries:
                 - Full text: search(query="timeout error")
                 - Scoped: search(query="500", entityTypes="spans,logs")
                 - Limited: search(query="deploy", limit=5)

                 Returns: Grouped search results by entity type
                 """)]
    public Task<string> SearchAsync(
        [Description("Search query string (full-text search)")]
        string query,
        [Description("Comma-separated entity types to search: 'spans', 'logs', 'issues', 'agents', 'workflows', 'all' (default: 'all')")]
        string entityTypes = "all",
        [Description("Maximum results per entity type (default: 10)")]
        int limit = 10)
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/search?q={Uri.EscapeDataString(query)}&limit={limit}";
            if (!string.IsNullOrEmpty(entityTypes) && entityTypes != "all")
                url += $"&entityTypes={Uri.EscapeDataString(entityTypes)}";

            var response = await client.GetFromJsonAsync<UnifiedSearchResponse>(
                url, SearchJsonContext.Default.UnifiedSearchResponse).ConfigureAwait(false);

            if (response?.Results is null || response.Results.Count is 0)
                return $"No results found for '{query}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Search Results for \"{query}\" ({response.TotalCount} total)");
            sb.AppendLine();

            foreach (var group in response.Results)
            {
                if (group.Items is null || group.Items.Count is 0)
                    continue;

                sb.AppendLine($"## {group.EntityType} ({group.Items.Count})");
                sb.AppendLine();
                sb.AppendLine("| ID | Name | Timestamp | Details |");
                sb.AppendLine("|----|------|-----------|---------|");

                foreach (var item in group.Items)
                {
                    var id = item.Id is { Length: > 8 } ? item.Id[..8] : item.Id ?? "-";
                    sb.AppendLine($"| {id} | {item.Name ?? "-"} | {item.Timestamp ?? "-"} | {item.Summary ?? "-"} |");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        });
    }
}

#region DTOs

internal sealed record UnifiedSearchResponse(
    [property: JsonPropertyName("results")] List<SearchResultGroup>? Results,
    [property: JsonPropertyName("total_count")] int TotalCount);

internal sealed record SearchResultGroup(
    [property: JsonPropertyName("entity_type")] string EntityType,
    [property: JsonPropertyName("items")] List<SearchResultItem>? Items);

internal sealed record SearchResultItem(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("timestamp")] string? Timestamp,
    [property: JsonPropertyName("summary")] string? Summary);

#endregion

[JsonSerializable(typeof(UnifiedSearchResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class SearchJsonContext : JsonSerializerContext;
