using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Mcp.Generators.Models;

namespace Qyl.Mcp.Generators.Analyzers;

/// <summary>
///     Discovers classes decorated with <c>[McpServerToolType]</c> and their <c>[McpServerTool]</c>
///     methods, plus the accompanying <c>[QylSkill]</c> class attribution and <c>[QylCapability]</c>
///     method attributions. A separate entry point handles <c>[QylCapabilityDefinition]</c> marker
///     classes so the generator can join both pipelines at emit time.
/// </summary>
internal static class ToolManifestAnalyzer
{
    internal const string McpServerToolTypeMetadataName =
        "ModelContextProtocol.Server.McpServerToolTypeAttribute";

    internal const string CapabilityDefinitionMetadataName =
        "qyl.mcp.Capabilities.QylCapabilityDefinitionAttribute";

    private const string McpServerToolMetadataName =
        "ModelContextProtocol.Server.McpServerToolAttribute";

    private const string DescriptionAttributeMetadataName =
        "System.ComponentModel.DescriptionAttribute";

    private const string QylSkillMetadataName =
        "qyl.mcp.Skills.QylSkillAttribute";

    private const string QylCapabilityMetadataName =
        "qyl.mcp.Capabilities.QylCapabilityAttribute";

    public static bool CouldBeToolTypeClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    public static bool CouldBeCapabilityDefinition(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    public static ToolTypeEntry? ExtractToolType(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.TargetNode is not ClassDeclarationSyntax)
            return null;

        if (context.TargetNode.SyntaxTree.FilePath.EndsWithOrdinal(".g.cs") ||
            context.TargetNode.SyntaxTree.FilePath.EndsWithOrdinal(".generated.cs"))
            return null;

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var compilation = context.SemanticModel.Compilation;
        var toolAttrType = compilation.GetTypeByMetadataName(McpServerToolMetadataName);
        var descriptionAttrType = compilation.GetTypeByMetadataName(DescriptionAttributeMetadataName);
        var skillAttrType = compilation.GetTypeByMetadataName(QylSkillMetadataName);
        var capabilityAttrType = compilation.GetTypeByMetadataName(QylCapabilityMetadataName);

        var methods = new List<ToolMethodEntry>();

        foreach (var member in typeSymbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member is not IMethodSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } method)
                continue;

            var toolAttr = FindAttribute(method, toolAttrType);
            if (toolAttr is null)
                continue;

            var toolName = GetNamedString(toolAttr, "Name") ?? method.Name;
            var capabilities = ExtractCapabilities(method, capabilityAttrType);

            methods.Add(new ToolMethodEntry(
                method.Name,
                toolName,
                GetNamedString(toolAttr, "Title"),
                GetDescription(method, descriptionAttrType),
                GetNamedBool(toolAttr, "ReadOnly", false),
                GetNamedBool(toolAttr, "Destructive", false),
                GetNamedBool(toolAttr, "Idempotent", false),
                GetNamedBool(toolAttr, "OpenWorld", false),
                method.ReturnType.GetFullyQualifiedName(),
                capabilities.ToEquatableArray()));
        }

        var skillKindName = ExtractSkillKindName(typeSymbol, skillAttrType);
        var fqn = typeSymbol.GetFullyQualifiedName();
        return new ToolTypeEntry(fqn, skillKindName, methods.ToEquatableArray());
    }

    public static CapabilityDefinitionEntry? ExtractCapabilityDefinition(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.TargetNode is not ClassDeclarationSyntax)
            return null;

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        foreach (var attr in typeSymbol.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attr.AttributeClass is null ||
                attr.AttributeClass.ToDisplayString() != "qyl.mcp.Capabilities.QylCapabilityDefinitionAttribute")
                continue;

            if (attr.ConstructorArguments.Length is 0)
                return null;

            var id = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(id))
                return null;

            string? requiredSkillKindName = null;
            if (attr.ConstructorArguments is [_, { Value: int skillValue } _])
            {
                requiredSkillKindName = ResolveSkillKindName(skillValue);
            }

            return new CapabilityDefinitionEntry(
                id!,
                GetNamedString(attr, "Title") ?? "",
                GetNamedString(attr, "Summary") ?? "",
                requiredSkillKindName,
                GetNamedString(attr, "SkillLabel") ?? "",
                GetNamedStringArray(attr, "Tags").ToEquatableArray(),
                GetNamedStringArray(attr, "UseCases").ToEquatableArray(),
                GetNamedStringArray(attr, "PrimaryIdentifiers").ToEquatableArray(),
                GetNamedStringArray(attr, "ScopingHints").ToEquatableArray(),
                GetNamedStringArray(attr, "EvidenceHints").ToEquatableArray(),
                GetNamedStringArray(attr, "RelatedCapabilities").ToEquatableArray());
        }

        return null;
    }

    private static List<CapabilityAttribution> ExtractCapabilities(
        IMethodSymbol method,
        INamedTypeSymbol? capabilityAttrType)
    {
        var list = new List<CapabilityAttribution>();
        if (capabilityAttrType is null)
            return list;

        foreach (var attr in method.GetAttributes())
        {
            if (!attr.AttributeClass.IsEqualTo(capabilityAttrType))
                continue;

            if (attr.ConstructorArguments.Length is 0 ||
                attr.ConstructorArguments[0].Value is not string id ||
                string.IsNullOrWhiteSpace(id))
                continue;

            var role = CapabilityRoleKind.Starting;
            if (attr.ConstructorArguments is [_, { Value: int roleValue } _, ..])
            {
                role = roleValue == 1 ? CapabilityRoleKind.FollowUp : CapabilityRoleKind.Starting;
            }

            list.Add(new CapabilityAttribution(id, role));
        }

        return list;
    }

    private static string? ExtractSkillKindName(INamedTypeSymbol typeSymbol, INamedTypeSymbol? skillAttrType)
    {
        if (skillAttrType is null)
            return null;

        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (!attr.AttributeClass.IsEqualTo(skillAttrType))
                continue;

            if (attr.ConstructorArguments is [{ Value: int skillValue } _, ..])
            {
                return ResolveSkillKindName(skillValue);
            }
        }

        return null;
    }

    private static string ResolveSkillKindName(int value) =>
        value switch
        {
            0 => "Inspect",
            1 => "Health",
            2 => "Analytics",
            3 => "Agent",
            4 => "Build",
            5 => "Anomaly",
            6 => "Loom",
            7 => "Apps",
            8 => "Debug",
            _ => "Inspect"
        };

    private static AttributeData? FindAttribute(IMethodSymbol method, INamedTypeSymbol? attrType)
    {
        if (attrType is null)
            return null;

        foreach (var attr in method.GetAttributes())
        {
            if (attr.AttributeClass.IsEqualTo(attrType))
                return attr;
        }

        return null;
    }

    private static string? GetNamedString(AttributeData attr, string name)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is string s)
                return s;
        }

        return null;
    }

    private static bool GetNamedBool(AttributeData attr, string name, bool defaultValue)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is bool b)
                return b;
        }

        return defaultValue;
    }

    private static string[] GetNamedStringArray(AttributeData attr, string name)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key != name)
                continue;

            if (arg.Value.Kind != TypedConstantKind.Array || arg.Value.Values.IsDefaultOrEmpty)
                return [];

            var result = new string[arg.Value.Values.Length];
            for (var i = 0; i < arg.Value.Values.Length; i++)
            {
                result[i] = arg.Value.Values[i].Value as string ?? "";
            }

            return result;
        }

        return [];
    }

    private static string? GetDescription(IMethodSymbol method, INamedTypeSymbol? attrType)
    {
        if (attrType is null)
            return null;

        foreach (var attr in method.GetAttributes())
        {
            if (!attr.AttributeClass.IsEqualTo(attrType))
                continue;

            if (attr.ConstructorArguments is [{ Value: string description }])
                return description;
        }

        return null;
    }
}
