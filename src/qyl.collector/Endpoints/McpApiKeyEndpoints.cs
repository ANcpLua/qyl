using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Endpoints;

/// <summary>
///     MCP API key endpoints — /api/v1/mcp/api-keys.
///     Generates and stores API keys for MCP tool authentication.
/// </summary>
internal static class McpApiKeyEndpoints
{
    private const int ApiKeyLength = 32;
    private const int PrefixLength = 8;

    public static WebApplication MapMcpApiKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mcp");

        group.MapPost("/api-keys", CreateApiKeyAsync);

        return app;
    }

    /// <summary>
    ///     POST /api/v1/mcp/api-keys — generate a new API key.
    ///     The full key is returned only once; only the prefix is stored.
    /// </summary>
    private static async Task<IResult> CreateApiKeyAsync(
        [FromBody] McpCreateApiKeyRequest request,
        [FromServices] DuckDbStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return TypedResults.BadRequest(new { error = "name is required" });

        var keyBytes = RandomNumberGenerator.GetBytes(ApiKeyLength);
        var fullKey = Convert.ToHexStringLower(keyBytes);
        var prefix = fullKey[..PrefixLength];
        var keyHash = Convert.ToHexStringLower(SHA256.HashData(keyBytes));
        var keyId = Guid.NewGuid().ToString("N");
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            // Ensure api_keys table exists
            await using var ddlCmd = con.CreateCommand();
            ddlCmd.CommandText = """
                                 CREATE TABLE IF NOT EXISTS mcp_api_keys (
                                     id VARCHAR NOT NULL PRIMARY KEY,
                                     name VARCHAR NOT NULL,
                                     key_prefix VARCHAR NOT NULL,
                                     key_hash VARCHAR NOT NULL,
                                     created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                     revoked_at TIMESTAMP
                                 )
                                 """;
            await ddlCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO mcp_api_keys (id, name, key_prefix, key_hash, created_at)
                              VALUES ($1, $2, $3, $4, $5)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = keyId });
            cmd.Parameters.Add(new DuckDBParameter { Value = request.Name });
            cmd.Parameters.Add(new DuckDBParameter { Value = prefix });
            cmd.Parameters.Add(new DuckDBParameter { Value = keyHash });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return TypedResults.Ok(new McpApiKeyResponseDto { Key = fullKey, Prefix = prefix, Name = request.Name });
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DTOs — API Key Endpoints
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed record McpCreateApiKeyRequest
{
    [JsonPropertyName("name")] public string? Name { get; init; }
}

internal sealed record McpApiKeyResponseDto
{
    [JsonPropertyName("key")] public required string Key { get; init; }
    [JsonPropertyName("prefix")] public required string Prefix { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
}
