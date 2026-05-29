using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class MapEndpointsAnalyzer
{
    internal const string MapEndpointsAttributeMetadataName = "Qyl.Contracts.Observability.QylMapEndpointsAttribute";

    public static bool CouldBeMapEndpointsMethod(SyntaxNode node, CancellationToken _) =>
        node is MethodDeclarationSyntax { AttributeLists.Count: > 0 };

    public static MapEndpointsDefinition? Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
    {
        if (context.TargetNode is not MethodDeclarationSyntax)
            return null;

        if (IncrementalPipelineHelpers.IsGeneratedFile(context.TargetNode.SyntaxTree.FilePath))
            return null;

        if (context.TargetSymbol is not IMethodSymbol
            {
                IsStatic: true,
                IsExtensionMethod: true,
                DeclaredAccessibility: Accessibility.Public,
                ContainingType: { } containingType
            } methodSymbol)
            return null;

        // ForAttributeWithMetadataName already filtered to [QylMapEndpoints]; read the match directly.
        if (context.Attributes is not [{ } attr, ..])
            return null;

        var order = attr.ConstructorArguments is [{ Value: int value }, ..] ? value : 100;

        return new MapEndpointsDefinition(
            containingType.GetFullyQualifiedName(),
            methodSymbol.Name,
            order,
            IncrementalPipelineHelpers.FormatSortKey(context.TargetNode));
    }
}
