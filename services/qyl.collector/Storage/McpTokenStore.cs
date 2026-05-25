using DuckDB.NET.Data;

namespace Qyl.Collector.Storage;

public sealed record McpTokenCreate(
    string UserId,
    string TenantId,
    string Scopes,
    byte[] EncryptedRefresh,
    DateTimeOffset RefreshExpiresAt);

public sealed record McpTokenIssued(string OpaqueToken, string TokenHash);

public sealed record McpTokenRecord(
    string TokenHash,
    string UserId,
    string TenantId,
    string Scopes,
    byte[] EncryptedRefresh,
    DateTimeOffset RefreshExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt,
    DateTimeOffset? RevokedAt);

public interface IMcpTokenStore
{
    Task<McpTokenIssued> CreateAsync(McpTokenCreate request, CancellationToken ct);

    Task<McpTokenRecord?> GetByOpaqueTokenAsync(string opaqueToken, CancellationToken ct);

    Task TouchLastUsedAsync(string tokenHash, CancellationToken ct);

    Task UpdateRefreshAsync(
        string tokenHash,
        byte[] encryptedRefresh,
        DateTimeOffset refreshExpiresAt,
        CancellationToken ct);

    Task RevokeAsync(string tokenHash, CancellationToken ct);

    Task<IReadOnlyList<McpTokenRecord>> ListByUserAsync(string userId, CancellationToken ct);

    Task<IReadOnlyList<McpTokenRecord>> ListByTenantAsync(string tenantId, CancellationToken ct);

    Task<int> CleanupExpiredAsync(CancellationToken ct);
}

internal sealed class McpTokenStore : IMcpTokenStore
{
    private const int OpaqueTokenByteLength = 32;

    private readonly DuckDbStore _store;
    private readonly TimeProvider _timeProvider;

    public McpTokenStore(DuckDbStore store, TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<McpTokenIssued> CreateAsync(McpTokenCreate request, CancellationToken ct)
    {
        var opaqueToken = GenerateOpaqueToken();
        var tokenHash = HashToken(opaqueToken);
        var now = _timeProvider.GetUtcNow();

        await _store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO mcp_tokens
                                  (token_hash, user_id, tenant_id, scopes, encrypted_refresh,
                                   refresh_expires_at, created_at, last_used_at)
                              VALUES (?, ?, ?, ?, ?, ?, ?, ?);
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = tokenHash });
            cmd.Parameters.Add(new DuckDBParameter { Value = request.UserId });
            cmd.Parameters.Add(new DuckDBParameter { Value = request.TenantId });
            cmd.Parameters.Add(new DuckDBParameter { Value = request.Scopes });
            cmd.Parameters.Add(new DuckDBParameter { Value = request.EncryptedRefresh });
            cmd.Parameters.Add(new DuckDBParameter { Value = request.RefreshExpiresAt.UtcDateTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = now.UtcDateTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = now.UtcDateTime });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return new McpTokenIssued(opaqueToken, tokenHash);
    }

    public async Task<McpTokenRecord?> GetByOpaqueTokenAsync(string opaqueToken, CancellationToken ct)
    {
        var tokenHash = HashToken(opaqueToken);

        await using var lease = await _store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT token_hash, user_id, tenant_id, scopes, encrypted_refresh,
                                 refresh_expires_at, created_at, last_used_at, revoked_at
                            FROM mcp_tokens
                           WHERE token_hash = ?
                             AND revoked_at IS NULL
                             AND refresh_expires_at > ?;
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = tokenHash });
        cmd.Parameters.Add(new DuckDBParameter { Value = _timeProvider.GetUtcNow().UtcDateTime });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapRow(reader) : null;
    }

    public async Task TouchLastUsedAsync(string tokenHash, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();

        await _store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE mcp_tokens SET last_used_at = ? WHERE token_hash = ?;";
            cmd.Parameters.Add(new DuckDBParameter { Value = now.UtcDateTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = tokenHash });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task UpdateRefreshAsync(
        string tokenHash,
        byte[] encryptedRefresh,
        DateTimeOffset refreshExpiresAt,
        CancellationToken ct)
    {
        await _store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE mcp_tokens
                                 SET encrypted_refresh = ?,
                                     refresh_expires_at = ?,
                                     last_used_at = ?
                               WHERE token_hash = ?;
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = encryptedRefresh });
            cmd.Parameters.Add(new DuckDBParameter { Value = refreshExpiresAt.UtcDateTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = _timeProvider.GetUtcNow().UtcDateTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = tokenHash });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();

        await _store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE mcp_tokens SET revoked_at = ? WHERE token_hash = ? AND revoked_at IS NULL;";
            cmd.Parameters.Add(new DuckDBParameter { Value = now.UtcDateTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = tokenHash });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<McpTokenRecord>> ListByUserAsync(string userId, CancellationToken ct)
    {
        await using var lease = await _store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT token_hash, user_id, tenant_id, scopes, encrypted_refresh,
                                 refresh_expires_at, created_at, last_used_at, revoked_at
                            FROM mcp_tokens
                           WHERE user_id = ?
                             AND revoked_at IS NULL
                           ORDER BY last_used_at DESC;
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = userId });
        return await ReadRowsAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<McpTokenRecord>> ListByTenantAsync(string tenantId, CancellationToken ct)
    {
        await using var lease = await _store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT token_hash, user_id, tenant_id, scopes, encrypted_refresh,
                                 refresh_expires_at, created_at, last_used_at, revoked_at
                            FROM mcp_tokens
                           WHERE tenant_id = ?
                             AND revoked_at IS NULL
                           ORDER BY last_used_at DESC;
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = tenantId });
        return await ReadRowsAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var revokedCutoff = now.AddDays(-7);
        var rowCount = 0;

        await _store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              DELETE FROM mcp_tokens
                               WHERE refresh_expires_at < ?
                                  OR (revoked_at IS NOT NULL AND revoked_at < ?);
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = revokedCutoff });
            rowCount = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return rowCount;
    }

    private static async Task<IReadOnlyList<McpTokenRecord>> ReadRowsAsync(
        DuckDBCommand cmd,
        CancellationToken ct)
    {
        var results = new List<McpTokenRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(MapRow(reader));
        }

        return results;
    }

    private static string GenerateOpaqueToken()
    {
        Span<byte> buffer = stackalloc byte[OpaqueTokenByteLength];
        RandomNumberGenerator.Fill(buffer);
        return Base64UrlEncode(buffer);
    }

    private static string HashToken(string opaqueToken)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(opaqueToken), hash);
        return Convert.ToHexString(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static McpTokenRecord MapRow(System.Data.Common.DbDataReader reader) =>
        new(
            TokenHash: reader.GetString(0),
            UserId: reader.GetString(1),
            TenantId: reader.GetString(2),
            Scopes: reader.GetString(3),
            EncryptedRefresh: (byte[])reader.GetValue(4),
            RefreshExpiresAt: new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
            CreatedAt: new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
            LastUsedAt: new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
            RevokedAt: reader.IsDBNull(8) ? null : new DateTimeOffset(reader.GetDateTime(8), TimeSpan.Zero));
}
