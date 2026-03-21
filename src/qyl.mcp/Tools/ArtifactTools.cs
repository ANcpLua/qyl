using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for storing and retrieving artifacts (code patches, reports, analysis results).
/// </summary>
[McpServerToolType]
public sealed class ArtifactTools(HttpClient client)
{
    [McpServerTool(
        Name = "qyl.store_artifact",
        Title = "Store Artifact",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    [Description("""
                 Store an artifact (code patch, analysis report, investigation notes) in qyl.

                 Returns a short URL for sharing: /a/<id>

                 Parameters:
                 - content (required): The artifact content (text, markdown, JSON, code)
                 - content_type: MIME type (default: text/plain). Common: text/markdown, application/json
                 - title: Human-readable title
                 - source: Origin identifier (e.g. "autofix", "rca", "code-review")
                 - ttl_seconds: Auto-expire after N seconds (0 = never expire)

                 Returns: Artifact ID and short URL
                 """)]
    public Task<string> StoreArtifactAsync(
        [Description("The artifact content")] string content,
        [Description("MIME type (default: text/plain)")]
        string? contentType = null,
        [Description("Human-readable title")] string? title = null,
        [Description("Origin (e.g. autofix, rca, code-review)")]
        string? source = null,
        [Description("Auto-expire seconds (0 = never)")]
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

            var sb = new StringBuilder();
            sb.AppendLine("# Artifact Stored");
            sb.AppendLine();
            sb.AppendLine($"- **ID:** `{result!.Id}`");
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
    [Description("""
                 Retrieve a stored artifact by ID.

                 Returns the artifact content, metadata, and creation time.
                 Returns an error if the artifact has expired or does not exist.

                 Returns: Artifact content and metadata
                 """)]
    public Task<string> GetArtifactAsync(
        [Description("Artifact ID")] string id) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var result = await client.GetFromJsonAsync(
                $"/api/v1/artifacts/{Uri.EscapeDataString(id)}",
                ArtifactToolsJsonContext.Default.ArtifactStoreResponse).ConfigureAwait(false);

            var sb = new StringBuilder();
            if (result!.Title is not null)
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

// ═══════════════════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════════════════

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
