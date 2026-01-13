// =============================================================================
// Schema Generator - OpenAPI → C# / DuckDB
// =============================================================================
// Single source of truth code generation from TypeSpec-compiled OpenAPI.
// OTel 1.38 semantic conventions, .NET 10 only.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;
using YamlDotNet.RepresentationModel;

namespace Domain.CodeGen;

// ════════════════════════════════════════════════════════════════════════════════
// SCHEMA GENERATOR - One button, entire schema translated
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
///     Generates C# types and DuckDB schema from TypeSpec-compiled OpenAPI.
///     Single entry point: <see cref="Generate" />.
/// </summary>
public static class SchemaGenerator
{
    const string SchemaSource = "schema/generated/openapi.yaml";

    /// <summary>
    ///     Generate all code from OpenAPI schema.
    /// </summary>
    public static GenerationResult Generate(AbsolutePath openApiPath, AbsolutePath protocolDir, AbsolutePath collectorDir, GenerationGuard guard)
    {
        var schema = OpenApiSchema.Load(openApiPath);
        Log.Information("Loaded schema: {Title} v{Version} ({Count} definitions)",
            schema.Title, schema.Version, schema.Schemas.Length);

        var files = new List<GeneratedFile>();

        // C# Primitives (Qyl.Common namespace)
        var scalars = schema.Schemas.Where(s => s.IsScalar).ToList();
        if (scalars.Count > 0)
        {
            files.Add(new GeneratedFile(
                protocolDir / "Primitives" / "Scalars.g.cs",
                GenerateScalars(scalars)));
        }

        // C# Enums (Qyl.Enums namespace)
        var enums = schema.Schemas.Where(s => s.IsEnum).ToList();
        if (enums.Count > 0)
        {
            files.Add(new GeneratedFile(
                protocolDir / "Enums" / "Enums.g.cs",
                GenerateEnums(enums)));
        }

        // C# Models (grouped by namespace)
        var models = schema.Schemas.Where(s => !s.IsScalar && !s.IsEnum && s.Type == "object").ToList();
        var unionTypes = schema.Schemas
            .Where(s => !s.IsScalar && !s.IsEnum && s.Type != "object")
            .Select(s => s.Name)
            .ToHashSet();

        foreach (var group in models.GroupBy(m => GetCSharpNamespace(m.Name)))
        {
            var fileName = GetFileNameFromNamespace(group.Key);
            files.Add(new GeneratedFile(
                protocolDir / "Models" / $"{fileName}.g.cs",
                GenerateModels(group.Key, group.ToList(), unionTypes)));
        }

        // DuckDB Schema
        var tables = schema.Schemas.Where(s => s.Extensions.ContainsKey("x-duckdb-table")).ToList();
        if (tables.Count > 0)
        {
            files.Add(new GeneratedFile(
                collectorDir / "Storage" / "DuckDbSchema.g.cs",
                GenerateDuckDb(tables, schema)));
        }

        // Write files using guard
        foreach (var file in files)
        {
            guard.WriteIfAllowed(file.Path, file.Content, file.Path.Name);
        }

        return new GenerationResult(files.Count, guard.Stats);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // C# SCALARS - Strongly-typed primitives (TraceId, SpanId, SessionId, etc.)
    // ════════════════════════════════════════════════════════════════════════════

    static string GenerateScalars(List<SchemaDefinition> scalars)
    {
        var sb = new StringBuilder();
        AppendCSharpHeader(sb, "Strongly-typed scalar primitives");

        foreach (var group in scalars.GroupBy(s => GetCSharpNamespace(s.Name)).OrderBy(g => g.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {group.Key};");
            sb.AppendLine();

            foreach (var scalar in group.OrderBy(s => s.GetTypeName()))
            {
                var typeName = EscapeKeyword(scalar.GetTypeName());
                var (underlying, jsonRead, jsonWrite) = GetScalarTypeInfo(scalar.Type, scalar.Format);
                var isHex = typeName is "TraceId" or "SpanId";
                var hexLen = typeName == "TraceId" ? 32 : typeName == "SpanId" ? 16 : 0;

                AppendXmlDoc(sb, scalar.Description, "");

                // Type declaration
                sb.AppendLine(CultureInfo.InvariantCulture, $"[System.Text.Json.Serialization.JsonConverter(typeof({typeName}JsonConverter))]");
                if (isHex)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"public readonly partial record struct {typeName}({underlying} Value) : System.IParsable<{typeName}>, System.ISpanFormattable");
                else
                    sb.AppendLine(CultureInfo.InvariantCulture, $"public readonly partial record struct {typeName}({underlying} Value)");

                sb.AppendLine("{");

                // Implicit conversions
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public static implicit operator {underlying}({typeName} v) => v.Value;");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public static implicit operator {typeName}({underlying} v) => new(v);");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public override string ToString() => {(underlying == "string" ? "Value ?? string.Empty" : "Value.ToString()")};");

                // Validation
                if (scalar.Pattern is not null)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    private static readonly System.Text.RegularExpressions.Regex s_pattern = new(@\"{scalar.Pattern.Replace("\"", "\"\"")}\", System.Text.RegularExpressions.RegexOptions.Compiled);");
                    sb.AppendLine("    public bool IsValid => !string.IsNullOrEmpty(Value) && s_pattern.IsMatch(Value);");
                }
                else if (underlying == "string")
                    sb.AppendLine("    public bool IsValid => !string.IsNullOrEmpty(Value);");
                else
                    sb.AppendLine("    public bool IsValid => true;");

                // Hex parsing for TraceId/SpanId
                if (isHex) AppendHexParsing(sb, typeName, hexLen);

                sb.AppendLine("}");
                sb.AppendLine();

                // JSON Converter
                sb.AppendLine(CultureInfo.InvariantCulture, $"file sealed class {typeName}JsonConverter : System.Text.Json.Serialization.JsonConverter<{typeName}>");
                sb.AppendLine("{");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public override {typeName} Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new({jsonRead});");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public override void Write(System.Text.Json.Utf8JsonWriter writer, {typeName} value, System.Text.Json.JsonSerializerOptions options) => {jsonWrite};");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    static void AppendHexParsing(StringBuilder sb, string typeName, int len)
    {
        var zeros = new string('0', len);
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public static {typeName} Empty => new(\"{zeros}\");");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public bool IsEmpty => string.IsNullOrEmpty(Value) || Value == \"{zeros}\";");
        sb.AppendLine();

        // IParsable<T> - string
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public static {typeName} Parse(string s, IFormatProvider? provider) => TryParse(s, provider, out var r) ? r : throw new FormatException($\"Invalid {typeName}: {{s}}\");");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public static bool TryParse(string? s, IFormatProvider? provider, out {typeName} result)");
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        if (s is {{ Length: {len} }} && IsValidHex(s.AsSpan())) {{ result = new(s); return true; }}");
        sb.AppendLine("        result = default; return false;");
        sb.AppendLine("    }");

        // ReadOnlySpan<char> - hot-path
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out {typeName} result)");
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        if (s.Length == {len} && IsValidHex(s)) {{ result = new(new string(s)); return true; }}");
        sb.AppendLine("        result = default; return false;");
        sb.AppendLine("    }");

        // ReadOnlySpan<byte> - UTF-8 JSON hot-path (used by Utf8JsonReader)
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public static bool TryParse(ReadOnlySpan<byte> utf8, IFormatProvider? provider, out {typeName} result)");
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        if (utf8.Length == {len} && IsValidHexUtf8(utf8))");
        sb.AppendLine("        {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            Span<char> chars = stackalloc char[{len}];");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            for (var i = 0; i < {len}; i++) chars[i] = (char)utf8[i];");
        sb.AppendLine("            result = new(new string(chars)); return true;");
        sb.AppendLine("        }");
        sb.AppendLine("        result = default; return false;");
        sb.AppendLine("    }");

        // ISpanFormattable
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public bool TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? provider)");
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        if (dest.Length < {len} || string.IsNullOrEmpty(Value)) {{ written = 0; return false; }}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        Value.AsSpan().CopyTo(dest); written = {len}; return true;");
        sb.AppendLine("    }");
        sb.AppendLine("    string IFormattable.ToString(string? format, IFormatProvider? provider) => Value ?? string.Empty;");

        // Helpers
        sb.AppendLine("    static bool IsValidHex(ReadOnlySpan<char> s) { foreach (var c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false; return true; }");
        sb.AppendLine("    static bool IsValidHexUtf8(ReadOnlySpan<byte> s) { foreach (var b in s) if (!((b >= '0' && b <= '9') || (b >= 'a' && b <= 'f') || (b >= 'A' && b <= 'F'))) return false; return true; }");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // C# ENUMS - OTel semantic convention enums
    // ════════════════════════════════════════════════════════════════════════════

    static string GenerateEnums(List<SchemaDefinition> enums)
    {
        var sb = new StringBuilder();
        AppendCSharpHeader(sb, "Enumeration types (OTel 1.38 semconv)");

        foreach (var group in enums.GroupBy(e => GetCSharpNamespace(e.Name)).OrderBy(g => g.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {group.Key}");
            sb.AppendLine("{");

            foreach (var enumDef in group.OrderBy(e => e.GetTypeName()))
            {
                var typeName = enumDef.GetTypeName();
                AppendXmlDoc(sb, enumDef.Description, "    ");

                sb.AppendLine(CultureInfo.InvariantCulture, $"    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<{typeName}>))]");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public enum {typeName}");
                sb.AppendLine("    {");

                for (var i = 0; i < enumDef.EnumValues.Length; i++)
                {
                    var value = enumDef.EnumValues[i];
                    var member = EscapeKeyword(ToPascalCase(value));
                    sb.AppendLine(CultureInfo.InvariantCulture, $"        [System.Runtime.Serialization.EnumMember(Value = \"{value}\")]");
                    sb.Append(CultureInfo.InvariantCulture, $"        {member} = {i}");
                    sb.AppendLine(i < enumDef.EnumValues.Length - 1 ? "," : "");
                }

                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // C# MODELS - Domain records (SpanRecord, SessionSummary, etc.)
    // ════════════════════════════════════════════════════════════════════════════

    static string GenerateModels(string ns, List<SchemaDefinition> models, HashSet<string> unionTypes)
    {
        var sb = new StringBuilder();
        AppendCSharpHeader(sb, $"Models for {ns}");

        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {ns};");
        sb.AppendLine();

        foreach (var model in models.OrderBy(m => m.GetTypeName()))
        {
            AppendXmlDoc(sb, model.Description, "");

            sb.AppendLine(CultureInfo.InvariantCulture, $"public sealed record {model.GetTypeName()}");
            sb.AppendLine("{");

            foreach (var prop in model.Properties)
            {
                var propName = EscapeKeyword(ToPascalCase(prop.Name));
                var propType = GetCSharpType(prop, unionTypes);
                var nullable = prop.IsRequired ? "" : "?";
                // Required non-nullable reference types need 'required' to satisfy CS8618
                var required = prop.IsRequired && IsReferenceType(propType) ? "required " : "";

                AppendXmlDoc(sb, prop.Description, "    ");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    [JsonPropertyName(\"{prop.Name}\")]");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public {required}{propType}{nullable} {propName} {{ get; init; }}");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // DUCKDB SCHEMA - Storage DDL
    // ════════════════════════════════════════════════════════════════════════════

    static string GenerateDuckDb(List<SchemaDefinition> tables, OpenApiSchema schema)
    {
        var sb = new StringBuilder();
        AppendCSharpHeader(sb, "DuckDB schema definitions");

        sb.AppendLine("namespace qyl.collector.Storage;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>DuckDB schema from TypeSpec God Schema.</summary>");
        sb.AppendLine("public static partial class DuckDbSchema");
        sb.AppendLine("{");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public const int Version = {GetSchemaVersion()};");
        sb.AppendLine();

        foreach (var table in tables)
        {
            var tableName = table.Extensions["x-duckdb-table"];
            var className = ToPascalCase(tableName);

            sb.AppendLine(CultureInfo.InvariantCulture, $"    public const string {className}Ddl = \"\"\"");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        CREATE TABLE IF NOT EXISTS {tableName} (");

            var cols = new List<string>();
            string? pk = null;

            foreach (var prop in table.Properties.Where(p => !p.Extensions.ContainsKey("x-internal")))
            {
                var colName = prop.Extensions.TryGetValue("x-duckdb-column", out var col) ? col : ToSnakeCase(prop.Name);
                var colType = GetDuckDbType(prop, schema);
                cols.Add($"            {colName} {colType}{(prop.IsRequired ? " NOT NULL" : "")}");
                if (prop.Extensions.TryGetValue("x-duckdb-primary-key", out var isPk) && isPk == "true") pk = colName;
            }

            cols.Add("            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP");
            if (pk is not null) cols.Add($"            PRIMARY KEY ({pk})");

            sb.AppendLine(string.Join(",\n", cols));
            sb.AppendLine("        );");
            sb.AppendLine("        \"\"\";");
            sb.AppendLine();
        }

        // Combined DDL
        sb.AppendLine("    public static string GetSchemaDdl() =>");
        sb.AppendLine("        $\"\"\"");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        -- QYL DuckDB Schema v{GetSchemaVersion()}");

        foreach (var table in tables)
        {
            var className = ToPascalCase(table.Extensions["x-duckdb-table"]);
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {{{className}Ddl}}");
        }

        // Indexes
        foreach (var table in tables)
        {
            var tableName = table.Extensions["x-duckdb-table"];
            foreach (var prop in table.Properties.Where(p => p.Extensions.ContainsKey("x-duckdb-index")))
            {
                var idx = prop.Extensions["x-duckdb-index"];
                var col = prop.Extensions.TryGetValue("x-duckdb-column", out var c) ? c : ToSnakeCase(prop.Name);
                sb.AppendLine(CultureInfo.InvariantCulture, $"        CREATE INDEX IF NOT EXISTS {idx} ON {tableName}({col});");
            }
        }

        sb.AppendLine("        \"\"\";");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════════

    static void AppendCSharpHeader(StringBuilder sb, string description)
    {
        var timestamp = TimeProvider.System.GetUtcNow().ToString("o", CultureInfo.InvariantCulture);
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// AUTO-GENERATED FILE - DO NOT EDIT");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//     Source:    {SchemaSource}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//     Generated: {timestamp}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//     {description}");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// To modify: update TypeSpec in schema/ then run: nuke Generate");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }

    static void AppendXmlDoc(StringBuilder sb, string? description, string indent)
    {
        if (description is null) return;
        sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}/// <summary>{EscapeXml(description)}</summary>");
    }

    static (string Type, string Read, string Write) GetScalarTypeInfo(string? type, string? format) => type switch
    {
        "integer" when format == "int32" => ("int", "reader.GetInt32()", "writer.WriteNumberValue(value.Value)"),
        "integer" => ("long", "reader.GetInt64()", "writer.WriteNumberValue(value.Value)"),
        "number" when format == "float" => ("float", "(float)reader.GetDouble()", "writer.WriteNumberValue(value.Value)"),
        "number" => ("double", "reader.GetDouble()", "writer.WriteNumberValue(value.Value)"),
        _ => ("string", "reader.GetString() ?? string.Empty", "writer.WriteStringValue(value.Value)")
    };

    static string GetCSharpType(SchemaProperty prop, HashSet<string> unionTypes)
    {
        if (prop.RefPath is not null)
        {
            var refType = prop.GetRefTypeName();
            if (refType is not null && !unionTypes.Contains(refType))
                return $"global::{MapNamespace(refType)}";
            return "object";
        }

        if (prop.Type == "array")
        {
            if (prop.ItemsRef is not null)
            {
                var itemType = prop.GetItemsTypeName();
                if (itemType is not null) return $"IReadOnlyList<global::{MapNamespace(itemType)}>";
            }
            return $"IReadOnlyList<{MapPrimitive(prop.ItemsType, prop.Format)}>";
        }

        return MapPrimitive(prop.Type, prop.Format);
    }

    static string MapNamespace(string name) => name switch
    {
        _ when name.StartsWith("Primitives.", StringComparison.Ordinal) => $"Qyl.Common.{name["Primitives.".Length..]}",
        _ when name.StartsWith("Models.", StringComparison.Ordinal) => $"Qyl.Models.{name["Models.".Length..]}",
        _ when name.StartsWith("Enums.", StringComparison.Ordinal) => $"Qyl.Enums.{name["Enums.".Length..]}",
        _ when name.StartsWith("Api.", StringComparison.Ordinal) => $"Qyl.Api.{name["Api.".Length..]}",
        _ => $"Qyl.{name}"
    };

    static string MapPrimitive(string? type, string? format) => type switch
    {
        "string" when format == "date-time" => "DateTimeOffset",
        "string" when format == "uuid" => "Guid",
        "string" => "string",
        "integer" when format == "int32" => "int",
        "integer" => "long",
        "number" when format == "float" => "float",
        "number" => "double",
        "boolean" => "bool",
        "object" => "IDictionary<string, object>",
        _ => "object"
    };

    static bool IsReferenceType(string propType) =>
        propType is "string" or "object" ||
        propType.StartsWith("IReadOnlyList<", StringComparison.Ordinal) ||
        propType.StartsWith("IDictionary<", StringComparison.Ordinal) ||
        propType.StartsWith("global::", StringComparison.Ordinal);

    static string GetDuckDbType(SchemaProperty prop, OpenApiSchema schema)
    {
        if (prop.Extensions.TryGetValue("x-duckdb-type", out var t)) return t;

        if (prop.RefPath is not null)
        {
            var refName = prop.GetRefTypeName();
            var refSchema = schema.Schemas.FirstOrDefault(s => s.Name == refName);
            if (refSchema?.Extensions.TryGetValue("x-duckdb-type", out var rt) == true) return rt;

            return refName switch
            {
                "Primitives.TraceId" => "VARCHAR(32)",
                "Primitives.SpanId" => "VARCHAR(16)",
                "Primitives.SessionId" => "VARCHAR(32)",
                "Primitives.UnixNano" or "Primitives.DurationNs" => "UBIGINT",
                "Primitives.TokenCount" or "Primitives.Count" => "BIGINT",
                "Primitives.CostUsd" or "Primitives.Temperature" => "DOUBLE",
                "Enums.SpanKind" or "Enums.StatusCode" => "TINYINT",
                _ => "JSON"
            };
        }

        return prop.Type switch
        {
            "string" when prop.Format == "date-time" => "TIMESTAMP",
            "string" when prop.Format == "uuid" => "UUID",
            "string" => "VARCHAR",
            "integer" when prop.Format == "int32" => "INTEGER",
            "integer" => "BIGINT",
            "number" => "DOUBLE",
            "boolean" => "BOOLEAN",
            "array" or "object" => "JSON",
            _ => "VARCHAR"
        };
    }

    static string GetCSharpNamespace(string name)
    {
        var lastDot = name.LastIndexOf('.');
        if (lastDot <= 0) return "Qyl";
        var ns = name[..lastDot];
        return ns switch
        {
            "Primitives" => "Qyl.Common",
            "Models" => "Qyl.Models",
            "Enums" => "Qyl.Enums",
            "Api" => "Qyl.Api",
            _ => $"Qyl.{ns}"
        };
    }

    static string GetFileNameFromNamespace(string ns) => ns[(ns.LastIndexOf('.') + 1)..];

    static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var sb = new StringBuilder();
        var cap = true;
        foreach (var c in value)
        {
            if (c is '_' or '-' or '.') { cap = true; continue; }
            sb.Append(cap ? char.ToUpperInvariant(c) : c);
            cap = false;
        }
        var result = sb.ToString();
        return result.Length > 0 && char.IsDigit(result[0]) ? "_" + result : result;
    }

    static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if (char.IsUpper(c) && sb.Length > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    static string EscapeKeyword(string name) =>
        s_keywords.Contains(name) ? $"@{name}" : name;

    static string EscapeXml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    static int GetSchemaVersion()
    {
        var now = TimeProvider.System.GetUtcNow();
        return now.Year * 10000 + now.Month * 100 + now.Day;
    }

    static readonly HashSet<string> s_keywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while", "unknown"
    ];
}

// ════════════════════════════════════════════════════════════════════════════════
// OPENAPI SCHEMA - YAML parsing
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>OpenAPI 3.1 schema parsed from YAML.</summary>
public sealed class OpenApiSchema
{
    OpenApiSchema(string title, string version, ImmutableArray<SchemaDefinition> schemas)
    {
        Title = title;
        Version = version;
        Schemas = schemas;
    }

    public string Title { get; }
    public string Version { get; }
    public ImmutableArray<SchemaDefinition> Schemas { get; }

    public static OpenApiSchema Load(AbsolutePath path)
    {
        var yaml = new YamlStream();
        using var reader = new StreamReader(path);
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var info = GetMapping(root, "info");
        var title = info is not null ? GetString(info, "title") ?? "Unknown" : "Unknown";
        var version = info is not null ? GetString(info, "version") ?? "1.0.0" : "1.0.0";

        var schemas = ImmutableArray<SchemaDefinition>.Empty;
        if (root.Children.TryGetValue("components", out var comp) &&
            comp is YamlMappingNode components &&
            components.Children.TryGetValue("schemas", out var sch) &&
            sch is YamlMappingNode schemasMap)
        {
            schemas = ParseSchemas(schemasMap);
        }

        return new OpenApiSchema(title, version, schemas);
    }

    static ImmutableArray<SchemaDefinition> ParseSchemas(YamlMappingNode map)
    {
        var builder = ImmutableArray.CreateBuilder<SchemaDefinition>();
        foreach (var (keyNode, valueNode) in map.Children)
        {
            var name = ((YamlScalarNode)keyNode).Value ?? "";
            if (valueNode is YamlMappingNode node) builder.Add(ParseSchema(name, node));
        }
        return builder.ToImmutable();
    }

    static SchemaDefinition ParseSchema(string name, YamlMappingNode node)
    {
        var type = GetString(node, "type");
        var description = GetString(node, "description");
        var format = GetString(node, "format");
        var pattern = GetString(node, "pattern");
        var enumValues = GetStringArray(node, "enum");
        var required = GetStringArray(node, "required");

        var properties = ImmutableArray<SchemaProperty>.Empty;
        if (node.Children.TryGetValue("properties", out var propsNode) && propsNode is YamlMappingNode propsMap)
            properties = ParseProperties(propsMap, required);

        var extensions = ParseExtensions(node);
        var hasAllOf = node.Children.ContainsKey("allOf");
        var hasAnyOf = node.Children.ContainsKey("anyOf") || node.Children.ContainsKey("oneOf");

        var isScalar = type is "string" or "integer" or "number" &&
                       properties.Length == 0 && enumValues.Length == 0 &&
                       !hasAllOf && !hasAnyOf && name.Contains('.');

        return new SchemaDefinition(name, type, description, format, pattern, enumValues, properties, extensions, isScalar, enumValues.Length > 0);
    }

    static ImmutableArray<SchemaProperty> ParseProperties(YamlMappingNode map, ImmutableArray<string> required)
    {
        var builder = ImmutableArray.CreateBuilder<SchemaProperty>();
        foreach (var (keyNode, valueNode) in map.Children)
        {
            var name = ((YamlScalarNode)keyNode).Value ?? "";
            if (valueNode is YamlMappingNode node) builder.Add(ParseProperty(name, node, required.Contains(name)));
        }
        return builder.ToImmutable();
    }

    static SchemaProperty ParseProperty(string name, YamlMappingNode node, bool isRequired)
    {
        var type = GetString(node, "type");
        var format = GetString(node, "format");
        var description = GetString(node, "description");
        var refPath = GetRef(node);

        if (refPath is null && node.Children.TryGetValue("allOf", out var allOfNode) &&
            allOfNode is YamlSequenceNode { Children.Count: > 0 } allOfSeq &&
            allOfSeq.Children[0] is YamlMappingNode firstAllOf)
            refPath = GetRef(firstAllOf);

        string? itemsRef = null, itemsType = null;
        if (type == "array" && node.Children.TryGetValue("items", out var itemsNode) && itemsNode is YamlMappingNode itemsMap)
        {
            itemsRef = GetRef(itemsMap);
            itemsType = GetString(itemsMap, "type");
        }

        return new SchemaProperty(name, type, format, description, refPath, itemsRef, itemsType, isRequired, ParseExtensions(node));
    }

    static ImmutableDictionary<string, string> ParseExtensions(YamlMappingNode node)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        foreach (var (keyNode, valueNode) in node.Children)
        {
            var key = ((YamlScalarNode)keyNode).Value ?? "";
            if (key.StartsWith("x-", StringComparison.Ordinal) && valueNode is YamlScalarNode scalar)
                builder[key] = scalar.Value ?? "";
        }
        return builder.ToImmutable();
    }

    static YamlMappingNode? GetMapping(YamlMappingNode parent, string key) =>
        parent.Children.TryGetValue(key, out var node) && node is YamlMappingNode m ? m : null;

    static string? GetString(YamlMappingNode parent, string key) =>
        parent.Children.TryGetValue(key, out var node) && node is YamlScalarNode s ? s.Value : null;

    static string? GetRef(YamlMappingNode node) =>
        node.Children.TryGetValue("$ref", out var refNode) && refNode is YamlScalarNode s ? s.Value : null;

    static ImmutableArray<string> GetStringArray(YamlMappingNode parent, string key)
    {
        if (parent.Children.TryGetValue(key, out var node) && node is YamlSequenceNode seq)
            return [..seq.Children.OfType<YamlScalarNode>().Select(s => s.Value ?? "").Where(s => s.Length > 0)];
        return [];
    }
}

/// <summary>OpenAPI schema definition.</summary>
public sealed record SchemaDefinition(
    string Name,
    string? Type,
    string? Description,
    string? Format,
    string? Pattern,
    ImmutableArray<string> EnumValues,
    ImmutableArray<SchemaProperty> Properties,
    ImmutableDictionary<string, string> Extensions,
    bool IsScalar,
    bool IsEnum)
{
    public string GetTypeName() => Name[(Name.LastIndexOf('.') + 1)..];
}

/// <summary>OpenAPI property definition.</summary>
public sealed record SchemaProperty(
    string Name,
    string? Type,
    string? Format,
    string? Description,
    string? RefPath,
    string? ItemsRef,
    string? ItemsType,
    bool IsRequired,
    ImmutableDictionary<string, string> Extensions)
{
    const string RefPrefix = "#/components/schemas/";
    public string? GetRefTypeName() => RefPath?.StartsWith(RefPrefix, StringComparison.Ordinal) == true ? RefPath[RefPrefix.Length..] : RefPath;
    public string? GetItemsTypeName() => ItemsRef?.StartsWith(RefPrefix, StringComparison.Ordinal) == true ? ItemsRef[RefPrefix.Length..] : ItemsRef;
}

/// <summary>Generation output.</summary>
public readonly record struct GeneratedFile(AbsolutePath Path, string Content);
public readonly record struct GenerationResult(int FileCount, GenerationStats Stats);

// ════════════════════════════════════════════════════════════════════════════════
// GENERATION GUARD - Write control
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>Controls file overwrites during generation.</summary>
public sealed class GenerationGuard
{
    public GenerationGuard(bool force = false, bool dryRun = false, bool skipExisting = false)
    {
        Force = force;
        DryRun = dryRun;
        SkipExisting = skipExisting;
    }

    public bool Force { get; }
    public bool DryRun { get; }
    public bool SkipExisting { get; }
    public GenerationStats Stats { get; } = new();

    public static GenerationGuard ForCi() => new(true);
    public static GenerationGuard ForLocal(bool force = false) => new(force, skipExisting: !force);

    public void WriteIfAllowed(AbsolutePath path, string content, string description)
    {
        if (DryRun)
        {
            Log.Information("  [DRY RUN] Would generate: {Path}", path);
            Stats.IncrementDryRun();
            return;
        }

        if (path.FileExists())
        {
            var existing = NormalizeForComparison(File.ReadAllText(path));
            var normalized = NormalizeForComparison(content);

            if (existing == normalized)
            {
                Log.Debug("  [UNCHANGED] {Description}", description);
                Stats.IncrementUnchanged();
                return;
            }

            if (!Force && SkipExisting)
            {
                Log.Information("  [SKIP] {Description} (use --igenerate-force-generate)", description);
                Stats.IncrementSkipped();
                return;
            }

            if (!Force)
            {
                Log.Warning("  [SKIP] {Description} (use --igenerate-force-generate to overwrite)", description);
                Stats.IncrementSkipped();
                return;
            }

            Log.Information("  [UPDATE] {Description}", description);
            Stats.IncrementUpdated();
        }
        else
        {
            Log.Debug("  [NEW] {Description}", description);
        }

        path.Parent.CreateDirectory();
        File.WriteAllText(path, content);
        Stats.IncrementGenerated();
    }

    public void LogSummary(bool failOnStaleInCi = true)
    {
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("  Generation Summary");
        Log.Information("═══════════════════════════════════════════════════════════════");
        if (Stats.GeneratedCount > 0) Log.Information("  Generated:   {Count} files", Stats.GeneratedCount);
        if (Stats.UpdatedCount > 0) Log.Information("  Updated:     {Count} files", Stats.UpdatedCount);
        if (Stats.SkippedCount > 0) Log.Information("  Skipped:     {Count} files", Stats.SkippedCount);
        if (Stats.UnchangedCount > 0) Log.Information("  Unchanged:   {Count} files", Stats.UnchangedCount);
        if (Stats.DryRunCount > 0) Log.Information("  Dry Run:     {Count} files", Stats.DryRunCount);

        if (NukeBuild.IsServerBuild && Stats.SkippedCount > 0 && !Force && failOnStaleInCi)
            throw new InvalidOperationException($"CI: {Stats.SkippedCount} stale files. Run 'nuke Generate --igenerate-force-generate'.");

        Log.Information("═══════════════════════════════════════════════════════════════");
    }

    static string NormalizeForComparison(string content)
    {
        content = content.ReplaceLineEndings("\n");
        return Regex.Replace(content, @"^(//|--)\s+Generated:\s+\d{4}-\d{2}-\d{2}T.*$", "$1     Generated: [TIMESTAMP]", RegexOptions.Multiline);
    }
}

/// <summary>Thread-safe generation statistics.</summary>
public sealed class GenerationStats
{
    int _generated, _updated, _skipped, _unchanged, _dryRun;

    public int GeneratedCount => _generated;
    public int UpdatedCount => _updated;
    public int SkippedCount => _skipped;
    public int UnchangedCount => _unchanged;
    public int DryRunCount => _dryRun;

    public void IncrementGenerated() => Interlocked.Increment(ref _generated);
    public void IncrementUpdated() => Interlocked.Increment(ref _updated);
    public void IncrementSkipped() => Interlocked.Increment(ref _skipped);
    public void IncrementUnchanged() => Interlocked.Increment(ref _unchanged);
    public void IncrementDryRun() => Interlocked.Increment(ref _dryRun);
}
