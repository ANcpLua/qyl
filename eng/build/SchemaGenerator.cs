// =============================================================================
// Schema Generator - OpenAPI → C# / DuckDB
// =============================================================================
// Single source of truth code generation from TypeSpec-compiled OpenAPI.
// OTel 1.39 semantic conventions, .NET 10 only.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
    /// <summary>
    ///     Generate all code from OpenAPI schema.
    /// </summary>
    public static GenerationResult Generate(AbsolutePath openApiPath, AbsolutePath protocolDir,
        AbsolutePath collectorDir, GenerationGuard guard)
    {
        var schema = OpenApiSchema.Load(openApiPath);
        Log.Information("Loaded schema: {Title} v{Version} ({Count} definitions)",
            schema.Title, schema.Version, schema.Schemas.Length);

        var files = new List<GeneratedFile>();

        // C# Primitives (Qyl.Common namespace)
        var scalars = schema.Schemas.Where(static s => s.IsScalar).ToList();
        if (scalars.Count > 0)
            files.Add(new GeneratedFile(
                protocolDir / "Primitives" / "Scalars.g.cs",
                GenerateScalars(scalars)));

        // C# Enums (grouped by namespace, like Models)
        var enums = schema.Schemas.Where(static s => s.IsEnum).ToList();
        foreach (var group in enums.GroupBy(static e => GetCSharpNamespace(e.Name)))
        {
            var fileName = GetFileNameFromNamespace(group.Key) + "Enums";
            files.Add(new GeneratedFile(
                protocolDir / "Enums" / $"{fileName}.g.cs",
                GenerateEnumsForNamespace(group.Key, [.. group])));
        }

        // C# Models (grouped by namespace)
        var models = schema.Schemas.Where(static s => s is { IsScalar: false, IsEnum: false, Type: "object" }).ToList();

        foreach (var group in models.GroupBy(static m => GetCSharpNamespace(m.Name)))
        {
            var fileName = GetFileNameFromNamespace(group.Key);
            files.Add(new GeneratedFile(
                protocolDir / "Models" / $"{fileName}.g.cs",
                GenerateModels(group.Key, (List<SchemaDefinition>)[.. group])));
        }

        // DuckDB Schema
        var tables = schema.Schemas.Where(static s => s.Extensions.ContainsKey("x-duckdb-table")).ToList();
        if (tables.Count > 0)
            files.Add(new GeneratedFile(
                collectorDir / "Storage" / "DuckDbSchema.g.cs",
                GenerateDuckDb(tables, schema)));

        // Write files using guard
        foreach (var file in files) guard.WriteIfAllowed(file.Path, file.Content, file.Path.Name);

        return new GenerationResult(files.Count, guard.Stats);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // C# SCALARS - Strongly-typed primitives (TraceId, SpanId, SessionId, etc.)
    // ════════════════════════════════════════════════════════════════════════════

    static string GenerateScalars(IEnumerable<SchemaDefinition> scalars)
    {
        var sb = new StringBuilder();
        AppendCSharpHeader(sb, "Strongly-typed scalar primitives");

        foreach (var group in scalars.GroupBy(static s => GetCSharpNamespace(s.Name)).OrderBy(static g => g.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {group.Key};");
            sb.AppendLine();

            foreach (var scalar in group.OrderBy(static s => s.GetTypeName()))
            {
                var typeName = EscapeKeyword(scalar.GetTypeName());
                var (underlying, jsonRead, jsonWrite) = GetScalarTypeInfo(scalar.Type, scalar.Format);
                var isHex = typeName is "TraceId" or "SpanId";
                var hexLen = typeName switch
                {
                    "TraceId" => 32,
                    "SpanId" => 16,
                    _ => 0
                };

                AppendXmlDoc(sb, scalar.Description, "");

                // Type declaration
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"[System.Text.Json.Serialization.JsonConverter(typeof({typeName}JsonConverter))]");
                if (isHex)
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"public readonly partial record struct {typeName}({underlying} Value) : System.IParsable<{typeName}>, System.ISpanFormattable");
                else
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"public readonly partial record struct {typeName}({underlying} Value)");

                sb.AppendLine("{");

                // Implicit conversions
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"    public static implicit operator {underlying}({typeName} v) => v.Value;");
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"    public static implicit operator {typeName}({underlying} v) => new(v);");
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"    public override string ToString() => {(underlying == "string" ? "Value ?? string.Empty" : "Value.ToString()")};");

                // Validation
                if (scalar.Pattern is not null)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"    private static readonly System.Text.RegularExpressions.Regex s_pattern = new(@\"{scalar.Pattern.Replace("\"", "\"\"")}\", System.Text.RegularExpressions.RegexOptions.Compiled);");
                    sb.AppendLine(
                        "    public bool IsValid => !string.IsNullOrEmpty(Value) && s_pattern.IsMatch(Value);");
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
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"file sealed class {typeName}JsonConverter : System.Text.Json.Serialization.JsonConverter<{typeName}>");
                sb.AppendLine("{");
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"    public override {typeName} Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options) => new({jsonRead});");
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"    public override void Write(System.Text.Json.Utf8JsonWriter writer, {typeName} value, System.Text.Json.JsonSerializerOptions options) => {jsonWrite};");
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
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"    public bool IsEmpty => string.IsNullOrEmpty(Value) || Value == \"{zeros}\";");
        sb.AppendLine();

        // IParsable<T> - string
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"    public static {typeName} Parse(string s, IFormatProvider? provider) => TryParse(s, provider, out var r) ? r : throw new FormatException($\"Invalid {typeName}: {{s}}\");");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"    public static bool TryParse(string? s, IFormatProvider? provider, out {typeName} result)");
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"        if (s is {{ Length: {len} }} && IsValidHex(s.AsSpan())) {{ result = new(s); return true; }}");
        sb.AppendLine("        result = default; return false;");
        sb.AppendLine("    }");

        // ReadOnlySpan<char> - hot-path
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out {typeName} result)");
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"        if (s.Length == {len} && IsValidHex(s)) {{ result = new(new string(s)); return true; }}");
        sb.AppendLine("        result = default; return false;");
        sb.AppendLine("    }");

        // ReadOnlySpan<byte> - UTF8 hot-path
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"    public static bool TryParse(ReadOnlySpan<byte> utf8, IFormatProvider? provider, out {typeName} result)");
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        if (utf8.Length == {len} && IsValidHexUtf8(utf8))");
        sb.AppendLine("        {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            Span<char> chars = stackalloc char[{len}];");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"            for (var i = 0; i < {len}; i++) chars[i] = (char)utf8[i];");
        sb.AppendLine("            result = new(new string(chars)); return true;");
        sb.AppendLine("        }");
        sb.AppendLine("        result = default; return false;");
        sb.AppendLine("    }");

        // ISpanFormattable
        sb.AppendLine(
            "    public bool TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? provider)");
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"        if (dest.Length < {len} || string.IsNullOrEmpty(Value)) {{ written = 0; return false; }}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"        Value.AsSpan().CopyTo(dest); written = {len}; return true;");
        sb.AppendLine("    }");
        sb.AppendLine(
            "    string IFormattable.ToString(string? format, IFormatProvider? provider) => Value ?? string.Empty;");

        // Hex validation helpers
        sb.AppendLine(
            "    static bool IsValidHex(ReadOnlySpan<char> s) { foreach (var c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false; return true; }");
        sb.AppendLine(
            "    static bool IsValidHexUtf8(ReadOnlySpan<byte> s) { foreach (var b in s) if (!((b >= '0' && b <= '9') || (b >= 'a' && b <= 'f') || (b >= 'A' && b <= 'F'))) return false; return true; }");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // C# ENUMS - Integer and string-backed enumerations
    // ════════════════════════════════════════════════════════════════════════════

    static string GenerateEnumsForNamespace(string ns, List<SchemaDefinition> enums)
    {
        var sb = new StringBuilder();
        AppendCSharpHeader(sb, $"Enumeration types for {ns}");

        sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {ns};");
        sb.AppendLine();

        // Deduplicate by type name within this namespace
        var deduped = enums
            .GroupBy(static e => e.GetTypeName())
            .Select(static g => g.First())
            .OrderBy(static e => e.GetTypeName());

        foreach (var enumDef in deduped)
        {
            var typeName = EscapeKeyword(enumDef.GetTypeName());
            var isIntegerEnum = enumDef.Type == "integer";
            var enumVarNames = enumDef.GetEnumVarNames();

            AppendXmlDoc(sb, enumDef.Description, "");

            // Use appropriate JSON converter
            if (isIntegerEnum)
                // Integer enums: serialize as integers
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonNumberEnumConverter<{typeName}>))]");
            else
                // String enums: serialize as strings
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<{typeName}>))]");

            sb.AppendLine(CultureInfo.InvariantCulture, $"public enum {typeName}");
            sb.AppendLine("{");

            for (var i = 0; i < enumDef.EnumValues.Length; i++)
            {
                var rawValue = enumDef.EnumValues[i];

                // Determine member name
                string memberName;
                if (i < enumVarNames.Length)
                    // Use x-enum-varnames if available
                    memberName = enumVarNames[i];
                else if (isIntegerEnum)
                    // Integer enum without varnames - try to infer from common patterns
                    memberName = InferEnumMemberName(typeName, rawValue);
                else
                    // String enum - convert value to PascalCase
                    memberName = ToPascalCase(rawValue);

                memberName = EscapeKeyword(memberName);

                // Emit member
                if (isIntegerEnum)
                    // Integer enum: memberName = value
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    {memberName} = {rawValue},");
                else
                {
                    // String enum: need EnumMember for serialization
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"    [System.Runtime.Serialization.EnumMember(Value = \"{rawValue}\")]");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    {memberName} = {i},");
                }
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Infer enum member name for integer enums without x-enum-varnames.
    /// </summary>
    static string InferEnumMemberName(string enumTypeName, string value) =>
        // For well-known OTel enums, provide proper names
        enumTypeName switch
        {
            "SpanKind" => value switch
            {
                "0" => "Unspecified",
                "1" => "Internal",
                "2" => "Server",
                "3" => "Client",
                "4" => "Producer",
                "5" => "Consumer",
                _ => $"Value{value}"
            },
            "StatusCode" => value switch
            {
                "0" => "Unset",
                "1" => "Ok",
                "2" => "Error",
                _ => $"Value{value}"
            },
            "SeverityNumber" => value switch
            {
                "0" => "Unspecified",
                "1" => "Trace",
                "5" => "Debug",
                "9" => "Info",
                "13" => "Warn",
                "17" => "Error",
                "21" => "Fatal",
                _ => $"Value{value}"
            },
            _ => $"Value{value}"
        };

    /// <summary>
    ///     Convert snake_case or kebab-case to PascalCase.
    ///     Ensures valid C# identifier (prefix with underscore if starts with digit).
    /// </summary>
    static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Unknown";

        var sb = new StringBuilder();
        var capitalizeNext = true;

        foreach (var c in value)
            if (c is '_' or '-' or ' ' or '.')
                capitalizeNext = true;
            else if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
                sb.Append(c);

        if (sb.Length == 0) return "Unknown";

        // C# identifiers cannot start with a digit - prefix with underscore
        var result = sb.ToString();
        return char.IsDigit(result[0]) ? $"_{result}" : result;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // C# MODELS - Record types with JSON serialization
    // ════════════════════════════════════════════════════════════════════════════

    static string GenerateModels(string ns, IEnumerable<SchemaDefinition> models)
    {
        var sb = new StringBuilder();
        AppendCSharpHeader(sb, $"Models for {ns}");

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {ns};");
        sb.AppendLine();

        foreach (var model in models.OrderBy(static m => m.GetTypeName()))
        {
            var typeName = EscapeKeyword(model.GetTypeName());

            AppendXmlDoc(sb, model.Description, "");
            sb.AppendLine(CultureInfo.InvariantCulture, $"public sealed record {typeName}");
            sb.AppendLine("{");

            foreach (var prop in model.Properties)
            {
                var propName = ToPascalCase(prop.Name);
                var propType = ResolveCSharpType(prop);
                var isNullable = !prop.IsRequired;

                AppendXmlDoc(sb, prop.Description, "    ");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    [JsonPropertyName(\"{prop.Name}\")]");

                if (isNullable)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    public {propType}? {propName} {{ get; init; }}");
                else
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"    public required {propType} {propName} {{ get; init; }}");

                sb.AppendLine();
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    static string ResolveCSharpType(SchemaProperty prop)
    {
        // Check for x-csharp-type override
        if (prop.Extensions.TryGetValue("x-csharp-type", out var csType))
            return csType;

        // Handle $ref
        if (prop.GetRefTypeName() is { } refTypeName)
        {
            // Handle union types (anyOf) - these are dynamic JSON values
            // AttributeValue can be string, bool, long, double, or arrays thereof
            if (refTypeName.EndsWith(".AttributeValue", StringComparison.Ordinal) ||
                refTypeName == "AttributeValue")
                return "global::System.Text.Json.Nodes.JsonNode";

            // Map namespace-qualified refs to C# types (TypeSpec-style with Qyl.* prefix)
            var ns = GetCSharpNamespace(refTypeName);
            var typeName = refTypeName[(refTypeName.LastIndexOf('.') + 1)..];
            return $"global::{ns}.{typeName}";
        }

        // Handle arrays
        if (prop.Type == "array")
        {
            var itemType = prop.ItemsRef is not null
                ? ResolveCSharpType(
                    new SchemaProperty(prop.Name, null, null, null, prop.ItemsRef, null, null, true, []))
                : MapOpenApiType(prop.ItemsType, null);
            return $"IReadOnlyList<{itemType}>";
        }

        // Map primitive types
        return MapOpenApiType(prop.Type, prop.Format);
    }

    static string MapOpenApiType(string? type, string? format) => (type, format) switch
    {
        ("string", "date-time") => "DateTimeOffset",
        ("string", "date") => "DateOnly",
        ("string", "time") => "TimeOnly",
        ("string", "uuid") => "Guid",
        ("string", "byte") => "ReadOnlyMemory<byte>",
        ("string", _) => "string",
        ("integer", "int32") => "int",
        ("integer", "int64") => "long",
        ("integer", _) => "int",
        ("number", "float") => "float",
        ("number", "double") => "double",
        ("number", _) => "double",
        ("boolean", _) => "bool",
        _ => "object"
    };

    // ════════════════════════════════════════════════════════════════════════════
    // DUCKDB SCHEMA - DDL generation
    // ════════════════════════════════════════════════════════════════════════════

    static string GenerateDuckDb(IEnumerable<SchemaDefinition> tables, OpenApiSchema schema)
    {
        var sb = new StringBuilder();
        AppendCSharpHeader(sb, "DuckDB schema definitions");

        var version = int.Parse(DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

        sb.AppendLine("namespace qyl.collector.Storage;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>DuckDB schema from TypeSpec God Schema.</summary>");
        sb.AppendLine("public static partial class DuckDbSchema");
        sb.AppendLine("{");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public const int Version = {version};");
        sb.AppendLine();

        var tableNames = new List<string>();
        var indexes = new List<string>();

        foreach (var table in tables.OrderBy(static t => t.Extensions["x-duckdb-table"]))
        {
            var tableName = table.Extensions["x-duckdb-table"];
            var constName = ToPascalCase(tableName) + "Ddl";
            tableNames.Add(constName);

            sb.AppendLine(CultureInfo.InvariantCulture, $"    public const string {constName} = \"\"\"");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        CREATE TABLE IF NOT EXISTS {tableName} (");

            var columns = new List<string>();
            string? primaryKey = null;

            foreach (var prop in table.Properties)
            {
                var columnName = prop.Extensions.TryGetValue("x-duckdb-column", out var col)
                    ? col
                    : ToSnakeCase(prop.Name);
                var columnType = ResolveDuckDbType(prop, schema);
                var isRequired = prop.IsRequired;

                var columnDef = $"            {columnName} {columnType}";
                if (isRequired && !columnType.Contains("DEFAULT", StringComparison.OrdinalIgnoreCase))
                    columnDef += " NOT NULL";

                columns.Add(columnDef);

                if (prop.Extensions.ContainsKey("x-duckdb-primary-key"))
                    primaryKey = columnName;

                if (prop.Extensions.TryGetValue("x-duckdb-index", out var indexName))
                    indexes.Add($"CREATE INDEX IF NOT EXISTS {indexName} ON {tableName}({columnName});");
            }

            // Add created_at if not present
            if (!table.Properties.Any(static p => p.Name == "createdAt"))
                columns.Add("            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP");

            sb.AppendLine(string.Join(",\n", columns));

            if (primaryKey is not null)
                sb.AppendLine(CultureInfo.InvariantCulture, $"            PRIMARY KEY ({primaryKey})");

            sb.AppendLine("        );");
            sb.AppendLine("        \"\"\";");
            sb.AppendLine();
        }

        // GetSchemaDdl method
        sb.AppendLine("    public static string GetSchemaDdl() =>");
        sb.AppendLine("        $\"\"\"");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        -- QYL DuckDB Schema v{version}");
        foreach (var name in tableNames)
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {{{name}}}");
        foreach (var index in indexes)
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {index}");
        sb.AppendLine("        \"\"\";");

        sb.AppendLine("}");

        return sb.ToString();
    }

    static string ResolveDuckDbType(SchemaProperty prop, OpenApiSchema schema)
    {
        // Check for explicit x-duckdb-type
        if (prop.Extensions.TryGetValue("x-duckdb-type", out var duckType))
            return duckType;

        // Handle $ref - look up the referenced type
        if (prop.RefPath is not null)
        {
            var refTypeName = prop.GetRefTypeName();
            var refSchema = schema.Schemas.FirstOrDefault(s => s.Name == refTypeName);

            if (refSchema is not null)
                return refSchema.Extensions.TryGetValue("x-duckdb-type", out var refDuckType)
                    ? refDuckType
                    :
                    // Fall back to type mapping
                    MapOpenApiTypeToDuckDb(refSchema.Type, refSchema.Format);
        }

        return MapOpenApiTypeToDuckDb(prop.Type, prop.Format);
    }

    static string MapOpenApiTypeToDuckDb(string? type, string? format) => (type, format) switch
    {
        ("string", "date-time") => "TIMESTAMP",
        ("string", "date") => "DATE",
        ("string", _) => "VARCHAR",
        ("integer", "int32") => "INTEGER",
        ("integer", "int64") => "BIGINT",
        ("integer", _) => "INTEGER",
        ("number", "float") => "FLOAT",
        ("number", "double") => "DOUBLE",
        ("number", _) => "DOUBLE",
        ("boolean", _) => "BOOLEAN",
        _ => "VARCHAR"
    };

    static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var sb = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
                sb.Append(c);
        }

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════════

    static void AppendCSharpHeader(StringBuilder sb, string description)
    {
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// AUTO-GENERATED FILE - DO NOT EDIT");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("//     Source:    core/openapi/openapi.yaml");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//     Generated: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//     {description}");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// To modify: update TypeSpec in core/specs/ then run: nuke Generate");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }

    static void AppendXmlDoc(StringBuilder sb, string? description, string indent)
    {
        if (string.IsNullOrWhiteSpace(description)) return;
        var escaped = description.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}/// <summary>{escaped}</summary>");
    }

    /// <summary>
    ///     Maps TypeSpec schema names to C# namespaces.
    ///     TypeSpec generates: Qyl.Common.TraceId, Qyl.Domains.AI.GenAi.*, etc.
    /// </summary>
    static string GetCSharpNamespace(string schemaName)
    {
        // Legacy prefixes (for backward compatibility)
        if (schemaName.StartsWith("Primitives.", StringComparison.Ordinal)) return "Qyl.Common";
        if (schemaName.StartsWith("Enums.", StringComparison.Ordinal)) return "Qyl.Enums";
        if (schemaName.StartsWith("Models.", StringComparison.Ordinal)) return "Qyl.Models";
        if (schemaName.StartsWith("Api.", StringComparison.Ordinal)) return "Qyl.Api";

        // TypeSpec-generated namespaces (Qyl.* prefix)
        // Order matters: more specific prefixes first!

        // Common subnamespaces
        if (schemaName.StartsWith("Qyl.Common.Errors.", StringComparison.Ordinal)) return "Qyl.Common.Errors";
        if (schemaName.StartsWith("Qyl.Common.Pagination.", StringComparison.Ordinal)) return "Qyl.Common.Pagination";
        if (schemaName.StartsWith("Qyl.Common.", StringComparison.Ordinal)) return "Qyl.Common";

        // OTel namespaces
        if (schemaName.StartsWith("Qyl.OTel.Enums.", StringComparison.Ordinal)) return "Qyl.OTel.Enums";
        if (schemaName.StartsWith("Qyl.OTel.Traces.", StringComparison.Ordinal)) return "Qyl.OTel.Traces";
        if (schemaName.StartsWith("Qyl.OTel.Logs.", StringComparison.Ordinal)) return "Qyl.OTel.Logs";
        if (schemaName.StartsWith("Qyl.OTel.Metrics.", StringComparison.Ordinal)) return "Qyl.OTel.Metrics";
        if (schemaName.StartsWith("Qyl.OTel.Resource.", StringComparison.Ordinal)) return "Qyl.OTel.Resource";
        if (schemaName.StartsWith("Qyl.OTel.", StringComparison.Ordinal)) return "Qyl.OTel";

        // Domain namespaces - AI (most specific first)
        if (schemaName.StartsWith("Qyl.Domains.AI.Code.", StringComparison.Ordinal)) return "Qyl.Domains.AI.Code";
        if (schemaName.StartsWith("Qyl.Domains.AI.", StringComparison.Ordinal)) return "Qyl.Domains.AI";

        // Domain namespaces - Identity
        if (schemaName.StartsWith("Qyl.Domains.Identity.", StringComparison.Ordinal)) return "Qyl.Domains.Identity";

        // Domain namespaces - Observe (most specific first)
        if (schemaName.StartsWith("Qyl.Domains.Observe.Error.", StringComparison.Ordinal)) return "Qyl.Domains.Observe.Error";
        if (schemaName.StartsWith("Qyl.Domains.Observe.Exceptions.", StringComparison.Ordinal)) return "Qyl.Domains.Observe.Exceptions";
        if (schemaName.StartsWith("Qyl.Domains.Observe.Log.", StringComparison.Ordinal)) return "Qyl.Domains.Observe.Log";
        if (schemaName.StartsWith("Qyl.Domains.Observe.Session.", StringComparison.Ordinal)) return "Qyl.Domains.Observe.Session";
        if (schemaName.StartsWith("Qyl.Domains.Observe.", StringComparison.Ordinal)) return "Qyl.Domains.Observe";

        // Domain namespaces - Ops (most specific first)
        if (schemaName.StartsWith("Qyl.Domains.Ops.Cicd.", StringComparison.Ordinal)) return "Qyl.Domains.Ops.Cicd";
        if (schemaName.StartsWith("Qyl.Domains.Ops.Deployment.", StringComparison.Ordinal)) return "Qyl.Domains.Ops.Deployment";
        if (schemaName.StartsWith("Qyl.Domains.Ops.", StringComparison.Ordinal)) return "Qyl.Domains.Ops";

        // Domain namespaces - Others
        if (schemaName.StartsWith("Qyl.Domains.Transport.", StringComparison.Ordinal)) return "Qyl.Domains.Transport";
        if (schemaName.StartsWith("Qyl.Domains.Security.", StringComparison.Ordinal)) return "Qyl.Domains.Security";
        if (schemaName.StartsWith("Qyl.Domains.Infra.", StringComparison.Ordinal)) return "Qyl.Domains.Infra";
        if (schemaName.StartsWith("Qyl.Domains.Runtime.", StringComparison.Ordinal)) return "Qyl.Domains.Runtime";
        if (schemaName.StartsWith("Qyl.Domains.Data.", StringComparison.Ordinal)) return "Qyl.Domains.Data";
        if (schemaName.StartsWith("Qyl.Domains.", StringComparison.Ordinal)) return "Qyl.Domains";

        // API namespace
        if (schemaName.StartsWith("Qyl.Api.", StringComparison.Ordinal)) return "Qyl.Api";

        // Streaming namespace (without Qyl. prefix)
        if (schemaName.StartsWith("Streaming.", StringComparison.Ordinal)) return "Qyl.Streaming";

        // Default: extract namespace from schema name or fall back
        return "Qyl.Models";
    }

    /// <summary>
    ///     Maps C# namespaces to output file names.
    /// </summary>
    static string GetFileNameFromNamespace(string ns) => ns switch
    {
        // Primitives/Scalars
        "Qyl.Common" => "Common",

        // Common subnamespaces
        "Qyl.Common.Errors" => "Errors",
        "Qyl.Common.Pagination" => "Pagination",

        // Enums
        "Qyl.Enums" => "Enums",
        "Qyl.OTel.Enums" => "OTelEnums",

        // OTel namespaces
        "Qyl.OTel" => "OTel",
        "Qyl.OTel.Traces" => "OTelTraces",
        "Qyl.OTel.Logs" => "OTelLogs",
        "Qyl.OTel.Metrics" => "OTelMetrics",
        "Qyl.OTel.Resource" => "OTelResource",

        // Domain namespaces - AI
        "Qyl.Domains" => "Domains",
        "Qyl.Domains.AI" => "DomainsAI",
        "Qyl.Domains.AI.Code" => "DomainsAICode",

        // Domain namespaces - Identity
        "Qyl.Domains.Identity" => "DomainsIdentity",

        // Domain namespaces - Observe
        "Qyl.Domains.Observe" => "DomainsObserve",
        "Qyl.Domains.Observe.Error" => "DomainsObserveError",
        "Qyl.Domains.Observe.Exceptions" => "DomainsObserveExceptions",
        "Qyl.Domains.Observe.Log" => "DomainsObserveLog",
        "Qyl.Domains.Observe.Session" => "DomainsObserveSession",

        // Domain namespaces - Ops
        "Qyl.Domains.Ops" => "DomainsOps",
        "Qyl.Domains.Ops.Cicd" => "DomainsOpsCicd",
        "Qyl.Domains.Ops.Deployment" => "DomainsOpsDeployment",

        // Domain namespaces - Others
        "Qyl.Domains.Transport" => "DomainsTransport",
        "Qyl.Domains.Security" => "DomainsSecurity",
        "Qyl.Domains.Infra" => "DomainsInfra",
        "Qyl.Domains.Runtime" => "DomainsRuntime",
        "Qyl.Domains.Data" => "DomainsData",

        // API
        "Qyl.Api" => "Api",

        // Streaming
        "Qyl.Streaming" => "Streaming",

        // Legacy/Default
        "Qyl.Models" => "Models",
        _ => ns.Replace(".", "")
    };

    static (string CSharpType, string JsonRead, string JsonWrite) GetScalarTypeInfo(string? type, string? format) =>
        (type, format) switch
        {
            ("string", _) => ("string", "reader.GetString() ?? string.Empty", "writer.WriteStringValue(value.Value)"),
            ("integer", "int64") => ("long", "reader.GetInt64()", "writer.WriteNumberValue(value.Value)"),
            ("integer", _) => ("int", "reader.GetInt32()", "writer.WriteNumberValue(value.Value)"),
            ("number", "double") => ("double", "reader.GetDouble()", "writer.WriteNumberValue(value.Value)"),
            ("number", "float") => ("float", "reader.GetSingle()", "writer.WriteNumberValue(value.Value)"),
            ("number", _) => ("double", "reader.GetDouble()", "writer.WriteNumberValue(value.Value)"),
            ("boolean", _) => ("bool", "reader.GetBoolean()", "writer.WriteBooleanValue(value.Value)"),
            _ => ("string", "reader.GetString() ?? string.Empty", "writer.WriteStringValue(value.Value)")
        };

    static string EscapeKeyword(string name) => name switch
    {
        "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or "catch" or "char" or "checked"
            or "class" or "const" or "continue" or "decimal" or "default" or "delegate" or "do" or "double"
            or "else" or "enum" or "event" or "explicit" or "extern" or "false" or "finally" or "fixed"
            or "float" or "for" or "foreach" or "goto" or "if" or "implicit" or "in" or "int" or "interface"
            or "internal" or "is" or "lock" or "long" or "namespace" or "new" or "null" or "object" or "operator"
            or "out" or "override" or "params" or "private" or "protected" or "public" or "readonly" or "ref"
            or "return" or "sbyte" or "sealed" or "short" or "sizeof" or "stackalloc" or "static" or "string"
            or "struct" or "switch" or "this" or "throw" or "true" or "try" or "typeof" or "uint" or "ulong"
            or "unchecked" or "unsafe" or "ushort" or "using" or "virtual" or "void" or "volatile" or "while"
            => $"@{name}",
        _ => name
    };
}

// ════════════════════════════════════════════════════════════════════════════════
// OPENAPI SCHEMA PARSER
// ════════════════════════════════════════════════════════════════════════════════
/// <summary>Parsed OpenAPI schema.</summary>
public sealed record OpenApiSchema(
    string Title,
    string Version,
    ImmutableArray<SchemaDefinition> Schemas)
{
    public static OpenApiSchema Load(AbsolutePath path)
    {
        using var reader = new StreamReader(path);
        var yaml = new YamlStream();
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var info = GetMapping(root, "info")
            ?? throw new InvalidOperationException($"OpenAPI schema missing required 'info' section: {path}");
        var title = GetString(info, "title") ?? "API";
        var version = GetString(info, "version") ?? "0.0.0";

        var schemas = ImmutableArray.CreateBuilder<SchemaDefinition>();
        var components = GetMapping(root, "components");
        var schemasNode = components is not null ? GetMapping(components, "schemas") : null;

        if (schemasNode is not null)
            foreach (var (keyNode, valueNode) in schemasNode.Children)
            {
                if (keyNode is not YamlScalarNode { Value: { } name })
                    continue;
                if (valueNode is YamlMappingNode schemaNode)
                    schemas.Add(ParseSchema(name, schemaNode));
            }

        return new OpenApiSchema(title, version, schemas.ToImmutable());
    }

    static SchemaDefinition ParseSchema(string name, YamlMappingNode node)
    {
        var type = GetString(node, "type");
        var description = GetString(node, "description");
        var format = GetString(node, "format");
        var pattern = GetString(node, "pattern");
        var enumValues = GetStringArray(node, "enum");
        var extensions = ParseExtensions(node);

        // Determine if scalar (primitive wrapper)
        // A type is a scalar if it has no properties AND is a primitive type (string/integer/number) AND is not an enum
        var propsMapping = GetMapping(node, "properties");
        var hasNoProperties = propsMapping is null || !propsMapping.Children.Any();
        var isScalar = extensions.ContainsKey("x-csharp-struct") ||
                       (type is "string" or "integer" or "number" && enumValues.Length == 0 && hasNoProperties);

        // Determine if enum
        var isEnum = enumValues.Length > 0;

        // Determine if union (anyOf)
        var isUnion = node.Children.ContainsKey("anyOf");

        // Parse properties for objects
        var properties = ImmutableArray<SchemaProperty>.Empty;
        var propsNode = GetMapping(node, "properties");
        var required = GetStringArray(node, "required").ToHashSet();

        if (propsNode is not null)
        {
            var propsBuilder = ImmutableArray.CreateBuilder<SchemaProperty>();
            foreach (var (keyNode, valueNode) in propsNode.Children)
            {
                if (keyNode is not YamlScalarNode { Value: { } propName })
                    continue;
                if (valueNode is YamlMappingNode propNode)
                    propsBuilder.Add(ParseProperty(propName, propNode, required.Contains(propName)));
            }

            properties = propsBuilder.ToImmutable();
        }

        return new SchemaDefinition(name, type, description, format, pattern, enumValues, properties, extensions,
            isScalar, isEnum, isUnion);
    }

    static SchemaProperty ParseProperty(string name, YamlMappingNode node, bool isRequired)
    {
        var type = GetString(node, "type");
        var format = GetString(node, "format");
        var description = GetString(node, "description");

        // Handle allOf (TypeSpec wraps refs in allOf)
        var allOf = node.Children.TryGetValue("allOf", out var allOfNode) && allOfNode is YamlSequenceNode seq
            ? seq.Children.OfType<YamlMappingNode>().FirstOrDefault()
            : null;

        var refPath = GetRef(node) ?? (allOf is not null ? GetRef(allOf) : null);

        // Handle arrays
        string? itemsRef = null;
        string? itemsType = null;
        var items = GetMapping(node, "items");
        if (items is not null)
        {
            itemsRef = GetRef(items);
            itemsType = GetString(items, "type");
        }

        return new SchemaProperty(name, type, format, description, refPath, itemsRef, itemsType, isRequired,
            ParseExtensions(node));
    }

    static ImmutableDictionary<string, string> ParseExtensions(YamlMappingNode node)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        foreach (var (keyNode, valueNode) in node.Children)
        {
            var key = ((YamlScalarNode)keyNode).Value ?? "";
            if (key.StartsWith("x-", StringComparison.Ordinal))
                builder[key] = valueNode switch
                {
                    YamlScalarNode scalar => scalar.Value ?? "",
                    YamlSequenceNode seq => string.Join(",",
                        seq.Children.OfType<YamlScalarNode>().Select(static s => s.Value)),
                    _ => builder[key]
                };
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
            return
            [
                ..seq.Children.OfType<YamlScalarNode>().Select(static s => s.Value ?? "")
                    .Where(static s => s.Length > 0)
            ];
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
    bool IsEnum,
    bool IsUnion)
{
    /// <summary>Gets the C# type name from the last part of the schema name.</summary>
    public string GetTypeName() => Name[(Name.LastIndexOf('.') + 1)..];

    /// <summary>Get enum member names from x-enum-varnames extension.</summary>
    public ImmutableArray<string> GetEnumVarNames()
    {
        if (Extensions.TryGetValue("x-enum-varnames", out var varnames) && !string.IsNullOrEmpty(varnames))
            return [..varnames.Split(',').Select(static s => s.Trim())];
        return [];
    }
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

    [return: NotNullIfNotNull(nameof(RefPath))]
    public string? GetRefTypeName() => RefPath?.StartsWith(RefPrefix, StringComparison.Ordinal) == true
        ? RefPath[RefPrefix.Length..]
        : RefPath;
}

/// <summary>Generation output.</summary>
public readonly record struct GeneratedFile(AbsolutePath Path, string Content);

/// <summary>Generation result with statistics.</summary>
public readonly record struct GenerationResult(int FileCount, GenerationStats Stats)
{
    /// <summary>Summary for logging.</summary>
    public override string ToString() =>
        $"Generated {FileCount} files ({Stats.GeneratedCount} new, {Stats.UpdatedCount} updated)";
}

// ════════════════════════════════════════════════════════════════════════════════
// GENERATION GUARD - Write control
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>Controls file overwrites during generation.</summary>
public sealed partial class GenerationGuard
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

            switch (Force)
            {
                case false when SkipExisting:
                    Log.Information("  [SKIP] {Description} (use --igenerate-force-generate)", description);
                    Stats.IncrementSkipped();
                    return;
                case false:
                    Log.Warning("  [SKIP] {Description} (use --igenerate-force-generate to overwrite)", description);
                    Stats.IncrementSkipped();
                    return;
                default:
                    Log.Information("  [UPDATE] {Description}", description);
                    Stats.IncrementUpdated();
                    break;
            }
        }
        else
            Log.Debug("  [NEW] {Description}", description);

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
            throw new InvalidOperationException(
                $"CI: {Stats.SkippedCount} stale files. Run 'nuke Generate --igenerate-force-generate'.");

        Log.Information("═══════════════════════════════════════════════════════════════");
    }

    static string NormalizeForComparison(string content)
    {
        content = content.ReplaceLineEndings("\n");
        return MyRegex().Replace(content, "$1     Generated: [TIMESTAMP]");
    }

    [GeneratedRegex(@"^(//|--)\s+Generated:\s+\d{4}-\d{2}-\d{2}T.*$", RegexOptions.Multiline)]
    private static partial Regex MyRegex();
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