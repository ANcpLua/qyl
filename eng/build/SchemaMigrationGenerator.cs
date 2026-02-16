// =============================================================================
// Schema Migration Generator - DDL Diff → ALTER TABLE
// =============================================================================
// Compares previous and current DuckDB DDL to generate additive migrations.
// Integrates with SchemaGenerator: called after DDL generation to produce
// migration files when the schema version changes.
//
// DuckDB ALTER TABLE support (0.10+):
//   ADD COLUMN, RENAME COLUMN, ALTER COLUMN TYPE (limited), DROP COLUMN
// We keep migrations additive where possible for safety.
// =============================================================================

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace Domain.CodeGen;

/// <summary>
///     Generates DuckDB migration SQL by diffing previous and current DDL snapshots.
/// </summary>
public static partial class SchemaMigrationGenerator
{
    // ════════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Compares two DDL snapshots and returns migration SQL if changes are detected.
    ///     Returns null if schemas are identical.
    /// </summary>
    public static MigrationResult? GenerateMigration(string previousDdl, string currentDdl, int fromVersion,
        int toVersion)
    {
        var previousTables = ParseDdl(previousDdl);
        var currentTables = ParseDdl(currentDdl);

        var changes = DiffTables(previousTables, currentTables);
        if (changes.Length is 0)
            return null;

        var sql = BuildMigrationSql(changes, fromVersion, toVersion);
        return new MigrationResult(fromVersion, toVersion, sql, changes);
    }

    /// <summary>
    ///     Writes a migration file to the specified directory.
    ///     File naming: V{version}__description.sql
    /// </summary>
    public static string WriteMigrationFile(string outputDirectory, MigrationResult migration,
        string? description = null)
    {
        Directory.CreateDirectory(outputDirectory);

        var desc = description ?? $"migrate_v{migration.FromVersion}_to_v{migration.ToVersion}";
        var safeDesc = InvalidFileChars().Replace(desc, "_");
        var fileName = $"V{migration.ToVersion}__{safeDesc}.sql";
        var filePath = Path.Combine(outputDirectory, fileName);

        File.WriteAllText(filePath, migration.Sql);
        Log.Information("  [MIGRATION] Written: {FileName} ({ChangeCount} changes)",
            fileName, migration.Changes.Length);

        return filePath;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // DDL PARSING
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Parses CREATE TABLE statements from DDL text into structured table definitions.
    /// </summary>
    internal static ImmutableDictionary<string, TableDefinition> ParseDdl(string ddl)
    {
        var tables = ImmutableDictionary.CreateBuilder<string, TableDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (Match tableMatch in CreateTableRegex().Matches(ddl))
        {
            var tableName = tableMatch.Groups["name"].Value.Trim();
            var body = tableMatch.Groups["body"].Value;

            var columns = ImmutableArray.CreateBuilder<ColumnDefinition>();
            var constraints = ImmutableArray.CreateBuilder<string>();

            foreach (var rawLine in body.Split('\n'))
            {
                var line = rawLine.Trim().TrimEnd(',');
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Skip constraint lines (PRIMARY KEY, UNIQUE, etc.)
                if (line.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                {
                    constraints.Add(line);
                    continue;
                }

                var colMatch = ColumnRegex().Match(line);
                if (colMatch.Success)
                {
                    var colName = colMatch.Groups["colname"].Value;
                    var colType = colMatch.Groups["coltype"].Value.Trim();
                    var modifiers = colMatch.Groups["modifiers"].Value.Trim();

                    var isNotNull = modifiers.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase);
                    var hasDefault = modifiers.Contains("DEFAULT", StringComparison.OrdinalIgnoreCase);

                    string? defaultValue = null;
                    if (hasDefault)
                    {
                        var defaultMatch = DefaultValueRegex().Match(modifiers);
                        if (defaultMatch.Success)
                            defaultValue = defaultMatch.Groups["val"].Value.Trim();
                    }

                    columns.Add(new ColumnDefinition(colName, colType, isNotNull, defaultValue));
                }
            }

            tables[tableName] = new TableDefinition(tableName, columns.ToImmutable(), constraints.ToImmutable());
        }

        // Also parse CREATE INDEX statements
        return tables.ToImmutable();
    }

    /// <summary>
    ///     Extracts CREATE INDEX statements from DDL text.
    /// </summary>
    internal static ImmutableArray<string> ParseIndexes(string ddl)
    {
        var indexes = ImmutableArray.CreateBuilder<string>();
        foreach (Match match in CreateIndexRegex().Matches(ddl)) indexes.Add(match.Value.Trim().TrimEnd(';'));

        return indexes.ToImmutable();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // DIFFING
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Compares two sets of table definitions and returns the list of changes.
    /// </summary>
    internal static ImmutableArray<SchemaChange> DiffTables(
        ImmutableDictionary<string, TableDefinition> previous,
        ImmutableDictionary<string, TableDefinition> current)
    {
        var changes = ImmutableArray.CreateBuilder<SchemaChange>();

        // New tables
        foreach (var (tableName, tableDef) in current)
        {
            if (!previous.TryGetValue(tableName, out var prevTable))
            {
                changes.Add(new SchemaChange(ChangeKind.AddTable, tableName, null, null));
                continue;
            }

            var prevColumns = prevTable.Columns.ToDictionary(
                static c => c.Name, static c => c, StringComparer.OrdinalIgnoreCase);

            // New columns
            foreach (var col in tableDef.Columns)
                if (!prevColumns.TryGetValue(col.Name, out var prevCol))
                {
                    changes.Add(new SchemaChange(ChangeKind.AddColumn, tableName, col.Name, col));
                }
                else
                {
                    // Type change
                    if (!string.Equals(prevCol.Type, col.Type, StringComparison.OrdinalIgnoreCase))
                        changes.Add(new SchemaChange(ChangeKind.AlterColumnType, tableName, col.Name, col)
                        {
                            PreviousColumn = prevCol
                        });

                    // Nullability change (NOT NULL added)
                    if (col.IsNotNull && !prevCol.IsNotNull)
                        changes.Add(new SchemaChange(ChangeKind.AddNotNull, tableName, col.Name, col));
                }

            // Removed columns (comment only, for safety)
            var currentColumns = tableDef.Columns.ToDictionary(
                static c => c.Name, static c => c, StringComparer.OrdinalIgnoreCase);

            foreach (var col in prevTable.Columns)
                if (!currentColumns.ContainsKey(col.Name))
                    changes.Add(new SchemaChange(ChangeKind.RemoveColumn, tableName, col.Name, col));
        }

        // Dropped tables (comment only, for safety)
        foreach (var (tableName, _) in previous)
            if (!current.ContainsKey(tableName))
                changes.Add(new SchemaChange(ChangeKind.RemoveTable, tableName, null, null));

        return changes.ToImmutable();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // SQL GENERATION
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Builds migration SQL from a list of schema changes.
    /// </summary>
    internal static string BuildMigrationSql(ImmutableArray<SchemaChange> changes, int fromVersion, int toVersion)
    {
        var sb = new StringBuilder();

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"-- =============================================================================");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"-- Migration: v{fromVersion} -> v{toVersion}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"-- Generated: {TimeProvider.System.GetUtcNow():O}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"-- Changes:   {changes.Length}");
        sb.AppendLine(
            "-- =============================================================================");
        sb.AppendLine();

        // Group changes by table for readability
        var grouped = changes.GroupBy(static c => c.TableName).OrderBy(static g => g.Key);

        foreach (var tableGroup in grouped)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"-- Table: {tableGroup.Key}");
            sb.AppendLine("-- ---------------------------------------------------------------------------");

            foreach (var change in tableGroup)
                switch (change.Kind)
                {
                    case ChangeKind.AddTable:
                        // Full CREATE TABLE (the table DDL is already in the schema)
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"-- New table: {change.TableName} (use CREATE TABLE IF NOT EXISTS from schema)");
                        break;

                    case ChangeKind.AddColumn when change.Column is { } col:
                        var colDef = $"{col.Type}";
                        if (col.DefaultValue is not null)
                            colDef += $" DEFAULT {col.DefaultValue}";
                        // Do NOT add NOT NULL without DEFAULT for existing rows
                        if (col is { IsNotNull: true, DefaultValue: not null })
                            colDef += " NOT NULL";

                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"ALTER TABLE {change.TableName} ADD COLUMN IF NOT EXISTS {col.Name} {colDef};");
                        break;

                    case ChangeKind.AlterColumnType
                        when change is { Column: { } newCol, PreviousColumn: { } prevCol }:
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"-- Type change: {change.ColumnName} {prevCol.Type} -> {newCol.Type}");
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"ALTER TABLE {change.TableName} ALTER COLUMN {change.ColumnName} SET DATA TYPE {newCol.Type};");
                        break;

                    case ChangeKind.AddNotNull:
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"-- WARNING: Adding NOT NULL constraint requires all existing rows to have values");
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"-- ALTER TABLE {change.TableName} ALTER COLUMN {change.ColumnName} SET NOT NULL;");
                        break;

                    case ChangeKind.RemoveColumn:
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"-- REMOVED: Column {change.ColumnName} no longer in schema");
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"-- DROP COLUMN is supported in DuckDB 0.10+. Uncomment if safe:");
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"-- ALTER TABLE {change.TableName} DROP COLUMN {change.ColumnName};");
                        break;

                    case ChangeKind.RemoveTable:
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"-- REMOVED: Table {change.TableName} no longer in schema");
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"-- DROP TABLE IF EXISTS {change.TableName};");
                        break;
                }

            sb.AppendLine();
        }

        // Version tracking
        sb.AppendLine("-- Record migration");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"INSERT INTO _schema_versions (version, description, applied_at)");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"VALUES ({toVersion}, 'Auto-generated migration from v{fromVersion}', CURRENT_TIMESTAMP);");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // REGEX PATTERNS
    // ════════════════════════════════════════════════════════════════════════════

    [GeneratedRegex(
        @"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\s+(?<name>\w+)\s*\((?<body>[^;]+?)\)\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CreateTableRegex();

    [GeneratedRegex(
        @"^\s*(?<colname>\w+)\s+(?<coltype>\w+(?:\(\w+(?:,\s*\w+)?\))?)\s*(?<modifiers>.*?)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ColumnRegex();

    [GeneratedRegex(
        @"DEFAULT\s+(?<val>\S+(?:\s+\S+)?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DefaultValueRegex();

    [GeneratedRegex(
        @"CREATE\s+(?:UNIQUE\s+)?INDEX\s+IF\s+NOT\s+EXISTS\s+\w+\s+ON\s+\w+\([^)]+\)\s*;?",
        RegexOptions.IgnoreCase)]
    private static partial Regex CreateIndexRegex();

    [GeneratedRegex(@"[^\w\-.]")]
    private static partial Regex InvalidFileChars();
}

// ════════════════════════════════════════════════════════════════════════════════
// DATA TYPES
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>Parsed DuckDB table definition.</summary>
public sealed class TableDefinition
{
    public TableDefinition(
        string name,
        ImmutableArray<ColumnDefinition> columns,
        ImmutableArray<string> constraints)
    {
        Name = name;
        Columns = columns;
        Constraints = constraints;
    }

    public string Name { get; }

    public ImmutableArray<ColumnDefinition> Columns { get; }

    public ImmutableArray<string> Constraints { get; }
}

/// <summary>Parsed DuckDB column definition.</summary>
public sealed record ColumnDefinition(
    string Name,
    string Type,
    bool IsNotNull,
    string? DefaultValue);

/// <summary>Kind of schema change detected.</summary>
public enum ChangeKind
{
    AddTable,
    RemoveTable,
    AddColumn,
    RemoveColumn,
    AlterColumnType,
    AddNotNull
}

/// <summary>A single detected schema change.</summary>
public sealed record SchemaChange(
    ChangeKind Kind,
    string TableName,
    string? ColumnName,
    ColumnDefinition? Column)
{
    /// <summary>Previous column definition (for type changes).</summary>
    public ColumnDefinition? PreviousColumn { get; init; }
}

/// <summary>Result of migration generation.</summary>
public sealed record MigrationResult(
    int FromVersion,
    int ToVersion,
    string Sql,
    ImmutableArray<SchemaChange> Changes);