using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Context;

#pragma warning disable CA1305, CA1863 // Build-time code generators use invariant formatting

namespace Domain.CodeGen.Generators;

/// <summary>
///     Generates DuckDB schema SQL and C# mapping code.
///     Produces:
///     - schema.sql - CREATE TABLE/VIEW statements
///     - DuckDbSchema.g.cs - FrozenDictionary column mappings for type-safe access
/// </summary>
public sealed class DuckDbGenerator : IGenerator
{
    const string GeneratorName = nameof(DuckDbGenerator);

    public string Name => "DuckDB";

    public FrozenDictionary<string, string> Generate(QylSchema schema, BuildPaths paths, string rootNamespace)
    {
        var outputs = new Dictionary<string, string>();

        // Generate SQL schema
        outputs["schema.sql"] = EmitSql(schema);

        // Generate C# schema mappings
        outputs["DuckDbSchema.g.cs"] = EmitCSharpMappings(schema, rootNamespace);

        return outputs.ToFrozenDictionary();
    }

    // ════════════════════════════════════════════════════════════════════════
    // SQL Schema Generation
    // ════════════════════════════════════════════════════════════════════════

    static string EmitSql(QylSchema schema)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.Sql(GeneratorName, "schema.sql"));

        // Emit tables
        foreach (var table in schema.Tables.Where(t => !t.IsView))
            EmitTable(sb, table, schema);

        // Emit views
        foreach (var table in schema.Tables.Where(t => t.IsView))
            EmitView(sb, table);

        return sb.ToString();
    }

    static void EmitTable(StringBuilder sb, TableDefinition table, QylSchema schema)
    {
        var model = schema.Models.FirstOrDefault(m => m.Name == table.ModelName);
        if (model is null) return;

        sb.AppendLine($"-- {table.Description}");
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {table.Name} (");

        var columns = model.Properties?
            .Where(p => p.DuckDbColumn is not null)
            .ToList() ?? [];

        for (var i = 0; i < columns.Count; i++)
        {
            var prop = columns[i];
            var isLast = i == columns.Count - 1 && table.PrimaryKey is null;

            var nullConstraint = prop.IsRequired ? " NOT NULL" : "";
            var defaultValue = GetDefaultValue(prop);
            var defaultClause = defaultValue is not null ? $" DEFAULT {defaultValue}" : "";

            sb.Append($"    {prop.DuckDbColumn} {prop.DuckDbType}{nullConstraint}{defaultClause}");

            if (!isLast || table.PrimaryKey is not null)
                sb.Append(',');

            sb.AppendLine($" -- {prop.Description}");
        }

        // Primary key
        if (table.PrimaryKey is not null)
            sb.AppendLine($"    PRIMARY KEY ({table.PrimaryKey})");

        sb.AppendLine(");");
        sb.AppendLine();

        // Indexes
        if (table.Indexes is { Count: > 0 })
        {
            foreach (var index in table.Indexes)
            {
                var unique = index.IsUnique ? "UNIQUE " : "";
                var columnsList = string.Join(", ", index.Columns.Select(c =>
                    index.IsDescending ? $"{c} DESC" : c));

                sb.AppendLine($"CREATE {unique}INDEX IF NOT EXISTS {index.Name} ON {table.Name}({columnsList});");
            }

            sb.AppendLine();
        }
    }

    static void EmitView(StringBuilder sb, TableDefinition table)
    {
        sb.AppendLine($"-- {table.Description}");
        sb.AppendLine($"CREATE OR REPLACE VIEW {table.Name} AS");
        sb.AppendLine(table.ViewSql!.Trim() + ";");
        sb.AppendLine();
    }

    static string? GetDefaultValue(PropertyDefinition prop)
    {
        if (prop.DuckDbColumn == "created_at")
            return "now()";

        return null;
    }

    // ════════════════════════════════════════════════════════════════════════
    // C# Mappings Generation
    // ════════════════════════════════════════════════════════════════════════

    static string EmitCSharpMappings(QylSchema schema, string rootNamespace)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, "DuckDbSchema.g.cs"));
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Storage;");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// DuckDB schema mappings for type-safe column access.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class DuckDbSchema");
        sb.AppendLine("{");

        // Table names
        sb.AppendLine("    #region Table Names");
        sb.AppendLine();
        foreach (var table in schema.Tables)
        {
            var constName = ToPascalCase(table.Name);
            sb.AppendLine($"    /// <summary>Table: {table.Description}</summary>");
            sb.AppendLine($"    public const string {constName}Table = \"{table.Name}\";");
        }

        sb.AppendLine();
        sb.AppendLine("    #endregion");
        sb.AppendLine();

        // Column mappings per table
        foreach (var table in schema.Tables.Where(t => !t.IsView))
        {
            var model = schema.Models.FirstOrDefault(m => m.Name == table.ModelName);
            if (model is null) continue;

            var className = ToPascalCase(table.Name);

            sb.AppendLine($"    #region {className} Columns");
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>Column mappings for {table.Name} table.</summary>");
            sb.AppendLine($"    public static class {className}");
            sb.AppendLine("    {");

            // Individual column constants
            foreach (var prop in model.Properties?.Where(p => p.DuckDbColumn is not null) ?? [])
            {
                var constName = prop.Name;
                sb.AppendLine($"        /// <summary>{prop.Description} ({prop.DuckDbType})</summary>");
                sb.AppendLine($"        public const string {constName} = \"{prop.DuckDbColumn}\";");
            }

            sb.AppendLine();

            // All columns frozen set
            sb.AppendLine("        /// <summary>All column names.</summary>");
            sb.AppendLine("        public static FrozenSet<string> AllColumns { get; } = new HashSet<string>");
            sb.AppendLine("        {");
            foreach (var prop in model.Properties?.Where(p => p.DuckDbColumn is not null) ?? [])
                sb.AppendLine($"            {prop.Name},");
            sb.AppendLine("        }.ToFrozenSet();");
            sb.AppendLine();

            // Property to column mapping
            sb.AppendLine("        /// <summary>Maps C# property names to DuckDB column names.</summary>");
            sb.AppendLine(
                "        public static FrozenDictionary<string, string> PropertyToColumn { get; } = new Dictionary<string, string>");
            sb.AppendLine("        {");
            foreach (var prop in model.Properties?.Where(p => p.DuckDbColumn is not null) ?? [])
                sb.AppendLine($"            [nameof(Models.{model.Name}.{prop.Name})] = {prop.Name},");
            sb.AppendLine("        }.ToFrozenDictionary();");
            sb.AppendLine();

            // Promoted columns (gen_ai.*)
            var promotedCols = model.Properties?.Where(p => p.IsPromoted).ToList() ?? [];
            if (promotedCols.Count > 0)
            {
                sb.AppendLine("        /// <summary>Promoted gen_ai.* columns for fast access.</summary>");
                sb.AppendLine("        public static FrozenSet<string> PromotedColumns { get; } = new HashSet<string>");
                sb.AppendLine("        {");
                foreach (var prop in promotedCols)
                    sb.AppendLine($"            {prop.Name},");
                sb.AppendLine("        }.ToFrozenSet();");
                sb.AppendLine();
            }

            // Insert SQL
            var insertCols = model.Properties?
                .Where(p => p.DuckDbColumn is not null && p.DuckDbColumn != "created_at")
                .Select(p => p.DuckDbColumn!)
                .ToList() ?? [];

            sb.AppendLine("        /// <summary>INSERT statement with all columns.</summary>");
            sb.AppendLine("        public const string InsertSql = @\"");
            sb.AppendLine($"INSERT INTO {table.Name} (");
            sb.AppendLine($"    {string.Join(",\n    ", insertCols)}");
            sb.AppendLine(") VALUES (");
            sb.AppendLine($"    {string.Join(",\n    ", insertCols.Select((_, i) => $"${i + 1}"))}");
            sb.AppendLine(")\";");
            sb.AppendLine();

            // Select SQL
            sb.AppendLine("        /// <summary>SELECT * statement.</summary>");
            sb.AppendLine($"        public const string SelectAllSql = \"SELECT * FROM {table.Name}\";");
            sb.AppendLine();

            // Select by primary key
            if (table.PrimaryKey is not null)
            {
                sb.AppendLine("        /// <summary>SELECT by primary key.</summary>");
                sb.AppendLine(
                    $"        public const string SelectByIdSql = \"SELECT * FROM {table.Name} WHERE {table.PrimaryKey} = $1\";");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
        }

        // GenAi attribute to column mapping
        sb.AppendLine("    #region GenAI Attribute Mappings");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Maps OTel gen_ai.* attribute keys to promoted column names.</summary>");
        sb.AppendLine(
            "    public static FrozenDictionary<string, string> GenAiAttributeToColumn { get; } = new Dictionary<string, string>");
        sb.AppendLine("    {");

        var genAiMappings = new Dictionary<string, string>
        {
            ["gen_ai.system"] = "gen_ai_system",
            ["gen_ai.provider.name"] = "gen_ai_system", // Maps to same column
            ["gen_ai.request.model"] = "gen_ai_request_model",
            ["gen_ai.response.model"] = "gen_ai_response_model",
            ["gen_ai.usage.input_tokens"] = "gen_ai_input_tokens",
            ["gen_ai.usage.prompt_tokens"] = "gen_ai_input_tokens", // Deprecated alias
            ["gen_ai.usage.output_tokens"] = "gen_ai_output_tokens",
            ["gen_ai.usage.completion_tokens"] = "gen_ai_output_tokens", // Deprecated alias
            ["gen_ai.request.temperature"] = "gen_ai_temperature",
            ["gen_ai.response.finish_reason"] = "gen_ai_stop_reason",
            ["gen_ai.tool.name"] = "gen_ai_tool_name",
            ["gen_ai.tool.call.id"] = "gen_ai_tool_call_id",
            ["qyl.session.id"] = "session_id",
            ["qyl.cost.usd"] = "gen_ai_cost_usd"
        };

        foreach (var (attrKey, colName) in genAiMappings.OrderBy(kv => kv.Key))
            sb.AppendLine($"        [\"{attrKey}\"] = \"{colName}\",");

        sb.AppendLine("    }.ToFrozenDictionary();");
        sb.AppendLine();
        sb.AppendLine("    #endregion");

        // DDL
        sb.AppendLine();
        sb.AppendLine("    #region DDL");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Full schema DDL for initialization.</summary>");
        sb.AppendLine("    public const string SchemaDdl = @\"");

        // Inline the DDL
        foreach (var table in schema.Tables.Where(t => !t.IsView))
        {
            var model = schema.Models.FirstOrDefault(m => m.Name == table.ModelName);
            if (model is null) continue;

            sb.AppendLine($"CREATE TABLE IF NOT EXISTS {table.Name} (");

            var columns = model.Properties?
                .Where(p => p.DuckDbColumn is not null)
                .ToList() ?? [];

            for (var i = 0; i < columns.Count; i++)
            {
                var prop = columns[i];
                var isLast = i == columns.Count - 1 && table.PrimaryKey is null;

                var nullConstraint = prop.IsRequired ? " NOT NULL" : "";
                var defaultValue = GetDefaultValue(prop);
                var defaultClause = defaultValue is not null ? $" DEFAULT {defaultValue}" : "";

                sb.Append($"    {prop.DuckDbColumn} {prop.DuckDbType}{nullConstraint}{defaultClause}");

                if (!isLast || table.PrimaryKey is not null)
                    sb.Append(',');

                sb.AppendLine();
            }

            if (table.PrimaryKey is not null)
                sb.AppendLine($"    PRIMARY KEY ({table.PrimaryKey})");

            sb.AppendLine(");");
            sb.AppendLine();
        }

        // Indexes
        foreach (var table in schema.Tables.Where(t => !t.IsView && t.Indexes is { Count: > 0 }))
        foreach (var index in table.Indexes!)
        {
            var unique = index.IsUnique ? "UNIQUE " : "";
            var columnsList = string.Join(", ", index.Columns.Select(c =>
                index.IsDescending ? $"{c} DESC" : c));

            sb.AppendLine($"CREATE {unique}INDEX IF NOT EXISTS {index.Name} ON {table.Name}({columnsList});");
        }

        sb.AppendLine("\";");
        sb.AppendLine();
        sb.AppendLine("    #endregion");

        sb.AppendLine("}");

        return sb.ToString();
    }

    static string ToPascalCase(string snakeCase)
    {
        var parts = snakeCase.Split('_');
        var sb = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length is 0) continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            sb.Append(part[1..]);
        }

        return sb.ToString();
    }
}