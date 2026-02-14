// =============================================================================
// Type Mapping Table - Single source of truth for OpenAPI → C# / DuckDB
// =============================================================================
// Unifies MapOpenApiType() and MapOpenApiTypeToDuckDb() into one lookup table.
// The TS semconv generator uses suffix-based mappings (not covered here).
// =============================================================================

using System;
using System.Collections.Immutable;

namespace Domain.CodeGen;

/// <summary>
///     A single row in the type mapping table: OpenAPI (type, format) → C# type + DuckDB type.
/// </summary>
public sealed record TypeMapping(string? OpenApiType, string? OpenApiFormat, string CSharpType, string DuckDbType);

/// <summary>
///     Single source of truth for OpenAPI → C# and OpenAPI → DuckDB type mappings.
///     Replaces <c>MapOpenApiType</c> and <c>MapOpenApiTypeToDuckDb</c>.
/// </summary>
public static class TypeMappingTable
{
    /// <summary>
    ///     All known type mappings. Order matters: more specific (type+format) entries first,
    ///     then format-wildcard entries. The lookup methods scan linearly and return the first match.
    /// </summary>
    public static readonly ImmutableArray<TypeMapping> Mappings =
    [
        // string with specific formats
        new("string", "date-time", "DateTimeOffset", "TIMESTAMP"),
        new("string", "date", "DateOnly", "DATE"),
        new("string", "time", "TimeOnly", "VARCHAR"),
        new("string", "uuid", "Guid", "VARCHAR"),
        new("string", "byte", "ReadOnlyMemory<byte>", "VARCHAR"),

        // string wildcard (any other format or null format)
        new("string", null, "string", "VARCHAR"),

        // integer with specific formats
        new("integer", "int32", "int", "INTEGER"),
        new("integer", "int64", "long", "BIGINT"),

        // integer wildcard
        new("integer", null, "int", "INTEGER"),

        // number with specific formats
        new("number", "float", "float", "FLOAT"),
        new("number", "double", "double", "DOUBLE"),

        // number wildcard
        new("number", null, "double", "DOUBLE"),

        // boolean (no format variants)
        new("boolean", null, "bool", "BOOLEAN"),
    ];

    /// <summary>
    ///     Maps an OpenAPI (type, format) pair to a C# type name.
    ///     Returns <c>"object"</c> when no mapping is found.
    /// </summary>
    public static string GetCSharpType(string? type, string? format)
    {
        foreach (var m in Mappings.AsSpan())
        {
            if (!string.Equals(m.OpenApiType, type, StringComparison.Ordinal))
                continue;

            // Exact format match
            if (m.OpenApiFormat is not null && string.Equals(m.OpenApiFormat, format, StringComparison.Ordinal))
                return m.CSharpType;

            // Wildcard format row (OpenApiFormat == null) matches any format
            if (m.OpenApiFormat is null)
                return m.CSharpType;
        }

        return "object";
    }

    /// <summary>
    ///     Maps an OpenAPI (type, format) pair to a DuckDB column type.
    ///     Returns <c>"VARCHAR"</c> when no mapping is found.
    /// </summary>
    public static string GetDuckDbType(string? type, string? format)
    {
        foreach (var m in Mappings.AsSpan())
        {
            if (!string.Equals(m.OpenApiType, type, StringComparison.Ordinal))
                continue;

            // Exact format match
            if (m.OpenApiFormat is not null && string.Equals(m.OpenApiFormat, format, StringComparison.Ordinal))
                return m.DuckDbType;

            // Wildcard format row (OpenApiFormat == null) matches any format
            if (m.OpenApiFormat is null)
                return m.DuckDbType;
        }

        return "VARCHAR";
    }
}
