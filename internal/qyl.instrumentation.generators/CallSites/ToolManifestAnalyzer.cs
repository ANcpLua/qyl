using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class ToolManifestAnalyzer
{
    internal const string McpServerToolTypeMetadataName = "ModelContextProtocol.Server.McpServerToolTypeAttribute";

    public static bool CouldBeToolTypeClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    public static ToolTypeEntry? ExtractToolType(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
    {
        if (context.TargetNode is not ClassDeclarationSyntax)
            return null;

        if (IncrementalPipelineHelpers.IsGeneratedFile(context.TargetNode.SyntaxTree.FilePath))
            return null;

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        return new ToolTypeEntry(
            IncrementalPipelineHelpers.FormatSortKey(context.TargetNode),
            typeSymbol.GetFullyQualifiedName());
    }
}
