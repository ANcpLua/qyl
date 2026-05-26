namespace Qyl.Collector.Storage;

public sealed record PkceStateRecord(
    string CodeVerifier,
    string TenantId,
    string ClientRedirectUri,
    string Nonce,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public interface IPkceStateStore
{
    Task StoreAsync(
        string state,
        string codeVerifier,
        string tenantId,
        string clientRedirectUri,
        string nonce,
        TimeSpan ttl,
        CancellationToken ct);

    Task<PkceStateRecord?> ConsumeAsync(string state, CancellationToken ct);

    Task<int> CleanupExpiredAsync(CancellationToken ct);
}

internal sealed class PkceStateStore : IPkceStateStore
{
    private readonly DuckDbStore _store;
    private readonly TimeProvider _timeProvider;

    public PkceStateStore(DuckDbStore store, TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task StoreAsync(
        string state,
        string codeVerifier,
        string tenantId,
        string clientRedirectUri,
        string nonce,
        TimeSpan ttl,
        CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(ttl);

        await _store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO mcp_pkce_state
                                  (state, code_verifier, tenant_id, client_redirect_uri, nonce, created_at, expires_at)
                              VALUES (?, ?, ?, ?, ?, ?, ?);
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = state });
            cmd.Parameters.Add(new DuckDBParameter { Value = codeVerifier });
            cmd.Parameters.Add(new DuckDBParameter { Value = tenantId });
            cmd.Parameters.Add(new DuckDBParameter { Value = clientRedirectUri });
            cmd.Parameters.Add(new DuckDBParameter { Value = nonce });
            cmd.Parameters.Add(new DuckDBParameter { Value = now.UtcDateTime });
            cmd.Parameters.Add(new DuckDBParameter { Value = expiresAt.UtcDateTime });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task<PkceStateRecord?> ConsumeAsync(string state, CancellationToken ct)
    {
        PkceStateRecord? consumed = null;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              DELETE FROM mcp_pkce_state
                               WHERE state = ?
                                 AND expires_at > ?
                              RETURNING code_verifier, tenant_id, client_redirect_uri, nonce, created_at, expires_at;
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = state });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });

            await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                consumed = MapRow(reader);
            }
        }, ct).ConfigureAwait(false);

        return consumed;
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rowCount = 0;

        await _store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM mcp_pkce_state WHERE expires_at < ?;";
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            rowCount = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return rowCount;
    }

    private static PkceStateRecord MapRow(System.Data.Common.DbDataReader reader) =>
        new(
            CodeVerifier: reader.GetString(0),
            TenantId: reader.GetString(1),
            ClientRedirectUri: reader.GetString(2),
            Nonce: reader.GetString(3),
            CreatedAt: new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
            ExpiresAt: new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero));
}
