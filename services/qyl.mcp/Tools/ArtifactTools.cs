using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
public sealed partial class ArtifactTools(HttpClient client)
{
    [McpServerTool(
        Name = "qyl.store_artifact",
        Title = "Store Artifact",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    public partial Task<string> StoreArtifactAsync(
        string content,
        string? contentType = null,
        string? title = null,
        string? source = null,
        int? ttlSeconds = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var request = new ArtifactStoreRequest(content, contentType, title, source, ttlSeconds);
            var response = await client.PostAsJsonAsync(
                "/api/v1/artifacts",
                request,
                ArtifactToolsJsonContext.Default.ArtifactStoreRequest).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync(
                ArtifactToolsJsonContext.Default.ArtifactStoreResponse).ConfigureAwait(false);
            result = Guard.NotNull(result);

            var sb = new StringBuilder();
            sb.AppendLine("# Artifact Stored");
            sb.AppendLine();
            sb.AppendLine($"- **ID:** `{result.Id}`");
            sb.AppendLine($"- **URL:** `/a/{result.Id}`");
            sb.AppendLine($"- **Type:** {result.ContentType}");
            if (result.Title is not null)
                sb.AppendLine($"- **Title:** {result.Title}");
            if (result.ExpiresAt is not null)
                sb.AppendLine($"- **Expires:** {result.ExpiresAt:O}");

            return sb.ToString();
        }, "Failed to store artifact");

    [McpServerTool(
        Name = "qyl.get_artifact",
        Title = "Get Artifact",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    public partial Task<string> GetArtifactAsync(
        string id) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await client.GetFromJsonAsync(
                $"/api/v1/artifacts/{Uri.EscapeDataString(id)}",
                ArtifactToolsJsonContext.Default.ArtifactStoreResponse).ConfigureAwait(false);
            result = Guard.NotNull(result);

            var sb = new StringBuilder();
            if (result.Title is not null)
                sb.AppendLine($"# {result.Title}");
            else
                sb.AppendLine($"# Artifact {result.Id}");
            sb.AppendLine();
            sb.AppendLine($"- **Type:** {result.ContentType}");
            sb.AppendLine($"- **Created:** {result.CreatedAt:O}");
            if (result.Source is not null)
                sb.AppendLine($"- **Source:** {result.Source}");
            if (result.ExpiresAt is not null)
                sb.AppendLine($"- **Expires:** {result.ExpiresAt:O}");
            sb.AppendLine();
            sb.AppendLine("## Content");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(result.Content);
            sb.AppendLine("```");

            return sb.ToString();
        }, "Artifact not found");
}


internal sealed record ArtifactStoreRequest(
    [property: JsonPropertyName("content")]
    string Content,
    [property: JsonPropertyName("content_type")]
    string? ContentType = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("ttl_seconds")]
    int? TtlSeconds = null);

internal sealed record ArtifactStoreResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("content_type")]
    string ContentType,
    [property: JsonPropertyName("content")]
    string Content,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("created_at")]
    DateTime CreatedAt,
    [property: JsonPropertyName("expires_at")]
    DateTime? ExpiresAt);

[JsonSerializable(typeof(ArtifactStoreRequest))]
[JsonSerializable(typeof(ArtifactStoreResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class ArtifactToolsJsonContext : JsonSerializerContext;
