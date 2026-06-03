using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Qyl.Collector.Storage.Generators;

[Generator]
public sealed class DuckDbInsertGenerator : IIncrementalGenerator
{
    private const string DuckDbTableAttribute = "Qyl.Collector.Storage.DuckDbTableAttribute";
    private const string DuckDbColumnAttribute = "Qyl.Collector.Storage.DuckDbColumnAttribute";
    private const string DuckDbIgnoreAttribute = "Qyl.Collector.Storage.DuckDbIgnoreAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("DuckDbAttributes.g.cs", SourceText.From(DuckDbAttributeSource.Source, Encoding.UTF8)));

        var tableTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DuckDbTableAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => ExtractTableInfo(ctx))
            .WhereNotNull();

        context.RegisterSourceOutput(tableTypes, static (spc, tableInfo) =>
        {
            var source = DuckDbEmitter.Emit(tableInfo);
            spc.AddSource(GetHintName(tableInfo), SourceText.From(source, Encoding.UTF8));
        });
    }

    private static string GetHintName(DuckDbTableInfo tableInfo) =>
        string.IsNullOrEmpty(tableInfo.Namespace) ||
        string.Equals(tableInfo.Namespace, "<global namespace>", StringComparison.Ordinal)
            ? $"{tableInfo.TypeName}.DuckDb.g.cs"
            : $"{tableInfo.Namespace}.{tableInfo.TypeName}.DuckDb.g.cs";

    private static DuckDbTableInfo? ExtractTableInfo(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var typeDecl = (TypeDeclarationSyntax)ctx.TargetNode;

        if (!typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            return null;

        // ForAttributeWithMetadataName already guarantees this target carries [DuckDbTable],
        // so read the matched attribute directly instead of re-resolving its type per target.
        if (ctx.Attributes is not [{ } tableAttr, ..])
            return null;

        string? tableName = null;
        string? onConflict = null;

        if (tableAttr.ConstructorArguments.Length > 0)
            tableName = tableAttr.ConstructorArguments[0].Value as string;

        foreach (var named in tableAttr.NamedArguments.Where(static named => named.Key == "OnConflict"))
            onConflict = named.Value.Value as string;

        if (tableName is not { Length: > 0 })
            return null;

        // Resolve the per-property attribute types once per table, not once per property.
        var compilation = ctx.SemanticModel.Compilation;
        var columnAttributeType = compilation.GetTypeByMetadataName(DuckDbColumnAttribute);
        var ignoreAttributeType = compilation.GetTypeByMetadataName(DuckDbIgnoreAttribute);

        var columns = new List<DuckDbColumnInfo>();
        var ordinal = 0;

        foreach (var prop in GetPublicMappedProperties(typeSymbol, ignoreAttributeType))
        {
            var columnInfo = ExtractColumnInfo(prop, ordinal, columnAttributeType);
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
            columns.ToArray().ToEquatableArray());
    }

    private static List<IPropertySymbol> GetPublicMappedProperties(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol? ignoreAttributeType)
    {
        var properties = new List<IPropertySymbol>();
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol prop)
                continue;

            if (prop.DeclaredAccessibility != Accessibility.Public)
                continue;

            if (prop.GetMethod is null)
                continue;

            if (FindAttribute(prop, ignoreAttributeType) is not null)
                continue;

            properties.Add(prop);
        }

        properties.Sort(ComparePropertyDeclarationOrder);
        return properties;
    }

    private static int ComparePropertyDeclarationOrder(IPropertySymbol left, IPropertySymbol right)
    {
        var leftReference = left.DeclaringSyntaxReferences.FirstOrDefault();
        var rightReference = right.DeclaringSyntaxReferences.FirstOrDefault();
        if (leftReference is null || rightReference is null)
            return StringComparer.Ordinal.Compare(left.MetadataName, right.MetadataName);

        var fileComparison = StringComparer.Ordinal.Compare(
            leftReference.SyntaxTree.FilePath,
            rightReference.SyntaxTree.FilePath);
        if (fileComparison is not 0)
            return fileComparison;

        var positionComparison = leftReference.Span.Start.CompareTo(rightReference.Span.Start);
        return positionComparison is not 0
            ? positionComparison
            : StringComparer.Ordinal.Compare(left.MetadataName, right.MetadataName);
    }

    private static DuckDbColumnInfo? ExtractColumnInfo(
        IPropertySymbol prop,
        int defaultOrdinal,
        INamedTypeSymbol? columnAttributeType)
    {
        string? columnName = null;
        var isUBigInt = false;
        var excludeFromInsert = false;
        var ordinal = defaultOrdinal;
        string? sqlType = null;
        string? defaultSql = null;
        var primaryKeyOrdinal = -1;

        var colAttr = FindAttribute(prop, columnAttributeType);
        if (colAttr is not null)
        {
            if (colAttr.ConstructorArguments.Length > 0)
                columnName = colAttr.ConstructorArguments[0].Value as string;

            foreach (var named in colAttr.NamedArguments)
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
                    case "SqlType":
                        sqlType = named.Value.Value as string;
                        break;
                    case "DefaultSql":
                        defaultSql = named.Value.Value as string;
                        break;
                    case "PrimaryKeyOrdinal":
                        if (named.Value.Value is int p)
                            primaryKeyOrdinal = p;
                        break;
                }
            }
        }

        columnName ??= ToSnakeCase(prop.Name);

        var propType = prop.Type.ToDisplayString();
        var isNullable = prop.Type.NullableAnnotation == NullableAnnotation.Annotated ||
                         propType.EndsWithOrdinal("?");

        if (!isUBigInt && propType is "ulong" or "System.UInt64")
            isUBigInt = true;

        return new DuckDbColumnInfo(
            prop.Name,
            columnName,
            propType,
            isNullable,
            isUBigInt,
            excludeFromInsert,
            ordinal,
            sqlType,
            defaultSql,
            primaryKeyOrdinal);
    }

    private static AttributeData? FindAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
            return null;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass.IsEqualTo(attributeType))
                return attribute;
        }

        return null;
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
    EquatableArray<DuckDbColumnInfo> Columns);

internal readonly record struct DuckDbColumnInfo(
    string PropertyName,
    string ColumnName,
    string PropertyType,
    bool IsNullable,
    bool IsUBigInt,
    bool ExcludeFromInsert,
    int Ordinal,
    string? SqlType,
    string? DefaultSql,
    int PrimaryKeyOrdinal);
