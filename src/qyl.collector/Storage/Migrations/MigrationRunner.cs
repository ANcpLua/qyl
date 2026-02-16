// =============================================================================
// DuckDB Schema Migration Runner
// =============================================================================
// Tracks applied migrations in a _schema_versions table.
// Applies pending .sql migration files on startup, ordered by version number.
//
// Migration file naming convention:
//   V{version}__description.sql
//   Example: V20260214__add_session_cost_columns.sql
//
// The runner is called from DuckDbStore.InitializeSchema after the base
// CREATE TABLE IF NOT EXISTS DDL runs. This ensures tables exist before
// ALTER TABLE migrations execute.
// =============================================================================

using System.Text.RegularExpressions;

namespace qyl.collector.Storage.Migrations;

/// <summary>
///     Applies pending DuckDB schema migrations on startup.
///     Tracks applied versions in a <c>_schema_versions</c> metadata table.
/// </summary>
public sealed partial class MigrationRunner
{
    /// <summary>
    ///     DDL for the migration tracking table. Created automatically on first run.
    /// </summary>
    public const string SchemaVersionsDdl = """
                                            CREATE TABLE IF NOT EXISTS _schema_versions (
                                                version INTEGER NOT NULL PRIMARY KEY,
                                                description VARCHAR NOT NULL,
                                                applied_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                                checksum VARCHAR(64)
                                            );
                                            """;

    private const string CommentPrefix = "--";

    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(ILogger<MigrationRunner> logger) => _logger = logger;

    /// <summary>
    ///     Initializes the version tracking table and applies any pending migrations.
    /// </summary>
    /// <param name="connection">Open DuckDB connection (the write connection).</param>
    /// <param name="currentSchemaVersion">
    ///     The current schema version from <see cref="DuckDbSchema.Version" />.
    /// </param>
    /// <param name="migrationDirectory">
    ///     Directory containing V*.sql migration files. Pass null to skip file-based migrations.
    /// </param>
    public void ApplyPendingMigrations(
        DuckDBConnection connection,
        int currentSchemaVersion,
        string? migrationDirectory = null)
    {
        // 1. Ensure _schema_versions table exists
        EnsureVersionTable(connection);

        // 2. Get the last applied version
        var lastApplied = GetLastAppliedVersion(connection);

        if (lastApplied >= currentSchemaVersion)
        {
            LogSchemaUpToDate(lastApplied);
            return;
        }

        // 3. If no migrations have ever been applied, optionally apply file migrations from scratch.
        // This allows newly added migrations to run on fresh databases.
        if (lastApplied is 0)
        {
            if (migrationDirectory is not null && Directory.Exists(migrationDirectory))
            {
                var bootstrapFiles = GetPendingMigrationFiles(migrationDirectory, 0)
                    .Where(f => f.Version <= currentSchemaVersion)
                    .ToList();

                if (bootstrapFiles.Count > 0)
                {
                    LogApplyingMigrations(bootstrapFiles.Count, 0, currentSchemaVersion);
                    foreach (var migrationFile in bootstrapFiles)
                        ApplyMigrationFile(connection, migrationFile);
                }
                else
                {
                    RecordVersion(connection, currentSchemaVersion, "Initial schema baseline");
                    LogBaselineRecorded(currentSchemaVersion);
                }
            }
            else
            {
                RecordVersion(connection, currentSchemaVersion, "Initial schema baseline");
                LogBaselineRecorded(currentSchemaVersion);
            }

            return;
        }

        // 4. Apply pending migration files if directory is provided
        if (migrationDirectory is not null && Directory.Exists(migrationDirectory))
        {
            var pendingFiles = GetPendingMigrationFiles(migrationDirectory, lastApplied);

            if (pendingFiles.Count > 0)
            {
                LogApplyingMigrations(pendingFiles.Count, lastApplied, currentSchemaVersion);

                foreach (var migrationFile in pendingFiles)
                {
                    ApplyMigrationFile(connection, migrationFile);
                }
            }
        }
        else
        {
            // No migration directory: just record the version bump
            // The CREATE TABLE IF NOT EXISTS DDL handles additive schema
            RecordVersion(connection, currentSchemaVersion,
                $"Schema version bump from v{lastApplied}");
            LogVersionBumped(lastApplied, currentSchemaVersion);
        }
    }

    /// <summary>
    ///     Gets the last applied schema version, or 0 if none.
    /// </summary>
    public static int GetLastAppliedVersion(DuckDBConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT MAX(version) FROM _schema_versions";
        var result = cmd.ExecuteScalar();

        return result switch
        {
            int v => v,
            long v => (int)v,
            _ => 0
        };
    }

    /// <summary>
    ///     Gets all applied versions, ordered ascending.
    /// </summary>
    public static IReadOnlyList<AppliedMigration> GetAppliedMigrations(DuckDBConnection connection)
    {
        var migrations = new List<AppliedMigration>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT version, description, applied_at, checksum
                          FROM _schema_versions
                          ORDER BY version ASC
                          """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            migrations.Add(new AppliedMigration(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return migrations;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // INTERNAL
    // ════════════════════════════════════════════════════════════════════════════

    private static void EnsureVersionTable(DuckDBConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = SchemaVersionsDdl;
        cmd.ExecuteNonQuery();
    }

    private static void RecordVersion(DuckDBConnection connection, int version, string description)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO _schema_versions (version, description, applied_at)
                          VALUES ($1, $2, CURRENT_TIMESTAMP)
                          ON CONFLICT (version) DO NOTHING
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = version });
        cmd.Parameters.Add(new DuckDBParameter { Value = description });
        cmd.ExecuteNonQuery();
    }

    private List<MigrationFile> GetPendingMigrationFiles(string directory, int afterVersion)
    {
        var files = new List<MigrationFile>();

        foreach (var filePath in Directory.GetFiles(directory, "V*.sql"))
        {
            var fileName = Path.GetFileName(filePath);
            var match = MigrationFileRegex().Match(fileName);
            if (!match.Success)
            {
                LogSkippingInvalidFile(fileName);
                continue;
            }

            var version = int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture);
            var description = match.Groups["description"].Value.Replace('_', ' ');

            if (version > afterVersion)
            {
                files.Add(new MigrationFile(version, description, filePath));
            }
        }

        // Apply in version order
        files.Sort(static (a, b) => a.Version.CompareTo(b.Version));
        return files;
    }

    private void ApplyMigrationFile(DuckDBConnection connection, MigrationFile migration)
    {
        LogApplyingMigration(migration.Version, migration.Description);

        var sql = File.ReadAllText(migration.FilePath);

        // Compute checksum for audit trail
        var checksum = ComputeChecksum(sql);

        // Execute the migration SQL
        // Split on semicolons to handle multi-statement migrations
        // (DuckDB requires individual statements)
        var statements = SplitStatements(sql);

        using var tx = connection.BeginTransaction();
        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement))
                continue;

            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = statement;
            cmd.ExecuteNonQuery();
        }

        // Record the migration (inside the same transaction if possible)
        using (var recordCmd = connection.CreateCommand())
        {
            recordCmd.Transaction = tx;
            recordCmd.CommandText = """
                                    INSERT INTO _schema_versions (version, description, applied_at, checksum)
                                    VALUES ($1, $2, CURRENT_TIMESTAMP, $3)
                                    ON CONFLICT (version) DO NOTHING
                                    """;
            recordCmd.Parameters.Add(new DuckDBParameter { Value = migration.Version });
            recordCmd.Parameters.Add(new DuckDBParameter { Value = migration.Description });
            recordCmd.Parameters.Add(new DuckDBParameter { Value = checksum });
            recordCmd.ExecuteNonQuery();
        }

        tx.Commit();

        LogMigrationApplied(migration.Version, checksum[..8]);
    }

    /// <summary>
    ///     Splits SQL text into individual statements, respecting comments.
    ///     Filters out comment-only lines and empty statements.
    /// </summary>
    internal static List<string> SplitStatements(string sql)
    {
        var statements = new List<string>();
        var current = new StringBuilder();

        foreach (var line in sql.Split('\n'))
        {
            var trimmed = line.TrimStart();

            // Skip pure comment lines for statement splitting
            // but preserve them within multi-line statements
            if (trimmed.StartsWithOrdinal(CommentPrefix) && current.Length is 0)
                continue;

            current.AppendLine(line);

            // Check if line ends with semicolon (outside comments)
            var effectiveLine = trimmed;
            var commentIdx = effectiveLine.IndexOfOrdinal(CommentPrefix);
            if (commentIdx >= 0)
                effectiveLine = effectiveLine[..commentIdx];

            if (effectiveLine.TrimEnd().EndsWith(';'))
            {
                var stmt = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(stmt))
                    statements.Add(stmt.TrimEnd(';'));
                current.Clear();
            }
        }

        // Handle trailing statement without semicolon
        var remaining = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(remaining) && !IsCommentOnly(remaining))
            statements.Add(remaining.TrimEnd(';'));

        return statements;
    }

    private static bool IsCommentOnly(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWithOrdinal(CommentPrefix))
                return false;
        }

        return true;
    }

    private static string ComputeChecksum(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = XxHash64.Hash(bytes);
        return Convert.ToHexStringLower(hash);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // LOGGER MESSAGES
    // ════════════════════════════════════════════════════════════════════════════

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Schema is up to date (version {Version})")]
    private partial void LogSchemaUpToDate(int version);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Recorded initial schema baseline: v{Version}")]
    private partial void LogBaselineRecorded(int version);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Applying {Count} pending migration(s) from v{FromVersion} to v{ToVersion}")]
    private partial void LogApplyingMigrations(int count, int fromVersion, int toVersion);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Schema version bumped: v{FromVersion} -> v{ToVersion} (no migration files)")]
    private partial void LogVersionBumped(int fromVersion, int toVersion);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Skipping migration file with invalid name: {FileName}")]
    private partial void LogSkippingInvalidFile(string fileName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Applying migration V{Version}: {Description}")]
    private partial void LogApplyingMigration(int version, string description);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migration V{Version} applied successfully (checksum: {Checksum})")]
    private partial void LogMigrationApplied(int version, string checksum);

    [GeneratedRegex(@"^V(?<version>\d+)__(?<description>.+)\.sql$", RegexOptions.IgnoreCase)]
    private static partial Regex MigrationFileRegex();
}

// ════════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
///     A migration file on disk pending application.
/// </summary>
public sealed record MigrationFile(int Version, string Description, string FilePath);

/// <summary>
///     A migration that has been applied (from the _schema_versions table).
/// </summary>
public sealed record AppliedMigration(int Version, string Description, DateTime AppliedAt, string? Checksum);
