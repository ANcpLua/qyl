namespace Qyl.Collector.Storage;

/// <summary>
///     Artifact CRUD: store and retrieve shareable content produced by AI agents.
/// </summary>
public sealed partial class DuckDbStore
{
    private const string ArtifactInsertSql = """
                                             INSERT INTO artifacts (id, content_type, content, title, source, metadata_json, created_at, expires_at)
                                             VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                                             ON CONFLICT (id) DO UPDATE SET
                                                 content_type  = EXCLUDED.content_type,
                                                 content       = EXCLUDED.content,
                                                 title         = EXCLUDED.title,
                                                 source        = EXCLUDED.source,
                                                 metadata_json = EXCLUDED.metadata_json,
                                                 expires_at    = EXCLUDED.expires_at
                                             """;

    public async Task<ArtifactRow> StoreArtifactAsync(ArtifactRow artifact, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = ArtifactInsertSql;
            cmd.Parameters.Add(new DuckDBParameter { Value = artifact.Id });
            cmd.Parameters.Add(new DuckDBParameter { Value = artifact.ContentType });
            cmd.Parameters.Add(new DuckDBParameter { Value = artifact.Content });
            cmd.Parameters.Add(new DuckDBParameter { Value = artifact.Title ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = artifact.Source ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = artifact.MetadataJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = artifact.CreatedAt });
            cmd.Parameters.Add(new DuckDBParameter { Value = artifact.ExpiresAt ?? (object)DBNull.Value });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            return artifact;
        }, ct).ConfigureAwait(false);

    public async Task<ArtifactRow?> GetArtifactAsync(string id, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT id, content_type, content, title, source, metadata_json, created_at, expires_at
                          FROM artifacts
                          WHERE id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = id });
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? ReadArtifactRow(reader)
            : null;
    }

    private static ArtifactRow ReadArtifactRow(IDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetDateTime(6),
            reader.IsDBNull(7) ? null : reader.GetDateTime(7));
}

public sealed record ArtifactRow(
    string Id,
    string ContentType,
    string Content,
    string? Title,
    string? Source,
    string? MetadataJson,
    DateTime CreatedAt,
    DateTime? ExpiresAt);
