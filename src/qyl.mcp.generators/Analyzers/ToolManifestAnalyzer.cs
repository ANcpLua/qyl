using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Mcp.Generators.Models;

namespace Qyl.Mcp.Generators.Analyzers;

/// <summary>
///     Discovers classes decorated with [McpServerToolType] and their [McpServerTool] methods
///     for compile-time tool manifest generation.
/// </summary>
internal static class ToolManifestAnalyzer
{
    internal const string McpServerToolTypeMetadataName =
        "ModelContextProtocol.Server.McpServerToolTypeAttribute";

    private const string McpServerToolMetadataName =
        "ModelContextProtocol.Server.McpServerToolAttribute";

    private const string DescriptionAttributeMetadataName =
        "System.ComponentModel.DescriptionAttribute";

    public static bool CouldBeToolTypeClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    public static ToolTypeEntry? ExtractToolType(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.TargetNode is not ClassDeclarationSyntax)
            return null;

        if (context.TargetNode.SyntaxTree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) ||
            context.TargetNode.SyntaxTree.FilePath.EndsWith(".generated.cs", StringComparison.Ordinal))
            return null;

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var toolAttrType = context.SemanticModel.Compilation.GetTypeByMetadataName(McpServerToolMetadataName);
        var descriptionAttrType = context.SemanticModel.Compilation.GetTypeByMetadataName(DescriptionAttributeMetadataName);
        var methods = new List<ToolMethodEntry>();

        foreach (var member in typeSymbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member is not IMethodSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } method)
                continue;

            var toolAttr = FindAttribute(method, toolAttrType);
            if (toolAttr is null)
                continue;

            var toolName = GetNamedArgument(toolAttr, "Name") ?? method.Name;
            methods.Add(new ToolMethodEntry(
                method.Name,
                toolName,
                GetNamedArgument(toolAttr, "Title"),
                GetDescription(method, descriptionAttrType),
                GetNamedArgument(toolAttr, "ReadOnly", false),
                GetNamedArgument(toolAttr, "Destructive", false),
                GetNamedArgument(toolAttr, "Idempotent", false),
                GetNamedArgument(toolAttr, "OpenWorld", false),
                method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        var fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return new ToolTypeEntry(fqn, methods.ToEquatableArray());
    }

    private static AttributeData? FindAttribute(IMethodSymbol method, INamedTypeSymbol? attrType)
    {
        if (attrType is null)
            return null;

        foreach (var attr in method.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType))
                return attr;
        }

        return null;
    }

    private static string? GetNamedArgument(AttributeData attr, string name)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is string s)
                return s;
        }

        return null;
    }

    private static bool GetNamedArgument(AttributeData attr, string name, bool defaultValue)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is bool b)
                return b;
        }

        return defaultValue;
    }

    private static string? GetDescription(IMethodSymbol method, INamedTypeSymbol? attrType)
    {
        if (attrType is null)
            return null;

        foreach (var attr in method.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType))
                continue;

            if (attr.ConstructorArguments is [{ Value: string description }])
                return description;
        }

        return null;
    }
}
