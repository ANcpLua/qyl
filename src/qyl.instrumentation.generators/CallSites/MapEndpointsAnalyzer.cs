using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

/// <summary>
///     Discovers <c>public static</c> extension methods tagged with
///     <c>[QylMapEndpoints]</c> so the generator can emit a single
///     <c>MapQylGeneratedEndpoints(this WebApplication)</c> aggregator.
/// </summary>
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

        if (IncrementalPipelineHelpers.FindAttributeByName(
                context.Attributes, context.SemanticModel.Compilation, MapEndpointsAttributeMetadataName)
            is not { } attr)
            return null;

        var order = attr.ConstructorArguments is [{ Value: int value }, ..] ? value : 100;

        return new MapEndpointsDefinition(
            ContainingTypeFullyQualifiedName: containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            MethodName: methodSymbol.Name,
            Order: order,
            SortKey: IncrementalPipelineHelpers.FormatSortKey(context.TargetNode));
    }
}
