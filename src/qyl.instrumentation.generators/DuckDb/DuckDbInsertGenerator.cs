// =============================================================================
// qyl.instrumentation.generators - DuckDB Insert Generator
// Generates type-safe parameter binding and reader mapping for DuckDB
// Owner: qyl.instrumentation.generators
// =============================================================================

using System.Collections.Immutable;
using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace qyl.instrumentation.generators.DuckDb;

/// <summary>
///     Generates DuckDB helper methods for types decorated with [DuckDbTable].
///     For each marked type, generates:
///     - ColumnList constant (comma-separated column names)
///     - ColumnCount constant
///     - AddParameters(DuckDBCommand, T) static method
///     - MapFromReader(DbDataReader) static method
///     - BuildMultiRowInsertSql(int) static method
/// </summary>
[Generator]
public sealed class DuckDbInsertGenerator : IIncrementalGenerator
{
    private const string DuckDbTableAttribute = "qyl.collector.Storage.DuckDbTableAttribute";
    private const string DuckDbColumnAttribute = "qyl.collector.Storage.DuckDbColumnAttribute";
    private const string DuckDbIgnoreAttribute = "qyl.collector.Storage.DuckDbIgnoreAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Emit attributes
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("DuckDbAttributes.g.cs", SourceText.From(DuckDbAttributeSource.Source, Encoding.UTF8)));

        // Find types with [DuckDbTable]
        var tableTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DuckDbTableAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => ExtractTableInfo(ctx))
            .WhereNotNull();

        // Generate code
        context.RegisterSourceOutput(tableTypes, static (spc, tableInfo) =>
        {
            var source = DuckDbEmitter.Emit(tableInfo);
            spc.AddSource($"{tableInfo.TypeName}.DuckDb.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static DuckDbTableInfo? ExtractTableInfo(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var typeDecl = (TypeDeclarationSyntax)ctx.TargetNode;

        // Check if partial
        if (!typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            return null;

        // Get table attribute data
        string? tableName = null;
        string? onConflict = null;

        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == DuckDbTableAttribute)
            {
                if (attr.ConstructorArguments.Length > 0)
                    tableName = attr.ConstructorArguments[0].Value as string;

                foreach (var named in attr.NamedArguments)
                {
                    if (named.Key == "OnConflict")
                        onConflict = named.Value.Value as string;
                }
            }
        }

        if (tableName is not { Length: > 0 })
            return null;

        // Collect properties
        var columns = new List<DuckDbColumnInfo>();
        var ordinal = 0;

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol prop)
                continue;

            if (prop.DeclaredAccessibility != Accessibility.Public)
                continue;

            if (prop.GetMethod is null)
                continue;

            // Check for [DuckDbIgnore]
            if (prop.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == DuckDbIgnoreAttribute))
                continue;

            var columnInfo = ExtractColumnInfo(prop, ordinal);
            if (columnInfo is not null)
            {
                columns.Add(columnInfo.Value);
                if (!columnInfo.Value.ExcludeFromInsert)
                    ordinal++;
            }
        }

        return new DuckDbTableInfo(
            typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.Name,
            typeDecl is RecordDeclarationSyntax ? "record" : "class",
            tableName,
            onConflict,
            [..columns]);
    }

    private static DuckDbColumnInfo? ExtractColumnInfo(IPropertySymbol prop, int defaultOrdinal)
    {
        string? columnName = null;
        var isUBigInt = false;
        var excludeFromInsert = false;
        var ordinal = defaultOrdinal;

        foreach (var attr in prop.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == DuckDbColumnAttribute)
            {
                if (attr.ConstructorArguments.Length > 0)
                    columnName = attr.ConstructorArguments[0].Value as string;

                foreach (var named in attr.NamedArguments)
                {
                    switch (named.Key)
                    {
                        case "IsUBigInt":
                            isUBigInt = named.Value.Value is true;
                            break;
                        case "ExcludeFromInsert":
                            excludeFromInsert = named.Value.Value is true;
                            break;
                        case "Ordinal":
                            if (named.Value.Value is int o)
                                ordinal = o;
                            break;
                    }
                }
            }
        }

        // Default column name: convert PascalCase to snake_case
        columnName ??= ToSnakeCase(prop.Name);

        var propType = prop.Type.ToDisplayString();
        var isNullable = prop.Type.NullableAnnotation == NullableAnnotation.Annotated ||
                         propType.EndsWith("?", StringComparison.Ordinal);

        // Detect UBIGINT by type if not explicitly marked
        if (!isUBigInt && propType is "ulong" or "System.UInt64")
            isUBigInt = true;

        return new DuckDbColumnInfo(
            prop.Name,
            columnName,
            propType,
            isNullable,
            isUBigInt,
            excludeFromInsert,
            ordinal);
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}

internal readonly record struct DuckDbTableInfo(
    string Namespace,
    string TypeName,
    string TypeKind,
    string TableName,
    string? OnConflict,
    ImmutableArray<DuckDbColumnInfo> Columns);

internal readonly record struct DuckDbColumnInfo(
    string PropertyName,
    string ColumnName,
    string PropertyType,
    bool IsNullable,
    bool IsUBigInt,
    bool ExcludeFromInsert,
    int Ordinal);
