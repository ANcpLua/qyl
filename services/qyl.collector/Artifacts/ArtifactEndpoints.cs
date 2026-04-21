namespace Qyl.Collector.Artifacts;

public static class ArtifactEndpoints
{
    /// <summary>URL-safe 12-char artifact id (72 bits of entropy).</summary>
    private static string NewArtifactId() => Base64Url.NewRandom(9);

    [QylMapEndpoints]
    public static void MapArtifactEndpoints(this WebApplication app)
    {
        // ── REST API ──────────────────────────────────────────────────

        app.MapPost("/api/v1/artifacts", static async (
            ArtifactCreateRequest request,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var id = string.IsNullOrWhiteSpace(request.Id) ? NewArtifactId() : request.Id;
            var now = TimeProvider.System.GetUtcNow().UtcDateTime;

            var row = new ArtifactRow(
                id,
                request.ContentType ?? "text/plain",
                request.Content,
                request.Title,
                request.Source,
                request.Metadata is not null
                    ? JsonSerializer.Serialize(
                        request.Metadata, ArtifactJsonContext.Default.DictionaryStringString)
                    : null,
                now,
                request.TtlSeconds is > 0
                    ? now.AddSeconds(request.TtlSeconds.Value)
                    : null);

            await store.StoreArtifactAsync(row, ct).ConfigureAwait(false);

            return TypedResults.Created($"/a/{id}", new ArtifactResponse(
                row.Id, row.ContentType, row.Content, row.Title,
                row.Source, request.Metadata, row.CreatedAt, row.ExpiresAt));
        });

        app.MapGet("/api/v1/artifacts/{id}", static async Task<IResult> (
            string id, DuckDbStore store, CancellationToken ct) =>
        {
            var row = await store.GetArtifactAsync(id, ct).ConfigureAwait(false);
            if (row is null) return TypedResults.NotFound();

            if (row.ExpiresAt.HasValue && row.ExpiresAt.Value < TimeProvider.System.GetUtcNow().UtcDateTime)
                return TypedResults.NotFound();

            return TypedResults.Ok(ToResponse(row));
        });

        // ── Short URL ─────────────────────────────────────────────────

        app.MapGet("/a/{id}", static async Task<IResult> (
            string id, DuckDbStore store, HttpContext ctx, CancellationToken ct) =>
        {
            var row = await store.GetArtifactAsync(id, ct).ConfigureAwait(false);
            if (row is null) return TypedResults.NotFound();

            if (row.ExpiresAt.HasValue && row.ExpiresAt.Value < TimeProvider.System.GetUtcNow().UtcDateTime)
                return TypedResults.NotFound();

            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";

            // If the client accepts JSON, return structured response
            var accept = ctx.Request.Headers.Accept.ToString();
            if (accept.ContainsIgnoreCase("application/json"))
                return TypedResults.Ok(ToResponse(row));

            // Only serve safe content types inline — force text/plain for anything executable
            var safeType = SanitizeContentType(row.ContentType);
            return TypedResults.Content(row.Content, safeType);
        });
    }

    /// <summary>
    ///     Allowlist of content types safe to serve inline. Everything else becomes text/plain
    ///     to prevent stored XSS via attacker-controlled content_type (e.g. text/html, image/svg+xml).
    /// </summary>
    private static string SanitizeContentType(string contentType) =>
        contentType switch
        {
            "text/plain" or "application/json" or "text/csv"
                or "text/markdown" or "application/xml" or "text/xml" => contentType,
            _ => "text/plain"
        };

    private static ArtifactResponse ToResponse(ArtifactRow row)
    {
        Dictionary<string, string>? metadata = null;
        if (row.MetadataJson is not null)
        {
            metadata = JsonSerializer.Deserialize(
                row.MetadataJson, ArtifactJsonContext.Default.DictionaryStringString);
        }

        return new ArtifactResponse(
            row.Id, row.ContentType, row.Content, row.Title,
            row.Source, metadata, row.CreatedAt, row.ExpiresAt);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════════════════

public sealed record ArtifactCreateRequest(
    [property: JsonPropertyName("content")]
    string Content,
    [property: JsonPropertyName("content_type")]
    string? ContentType = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("metadata")]
    Dictionary<string, string>? Metadata = null,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("ttl_seconds")]
    int? TtlSeconds = null);

public sealed record ArtifactResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("content_type")]
    string ContentType,
    [property: JsonPropertyName("content")]
    string Content,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("metadata")]
    Dictionary<string, string>? Metadata,
    [property: JsonPropertyName("created_at")]
    DateTime CreatedAt,
    [property: JsonPropertyName("expires_at")]
    DateTime? ExpiresAt);

[JsonSerializable(typeof(ArtifactCreateRequest))]
[JsonSerializable(typeof(ArtifactResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class ArtifactJsonContext : JsonSerializerContext;
