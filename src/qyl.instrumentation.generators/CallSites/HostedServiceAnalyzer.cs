using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

/// <summary>
///     Discovers classes tagged with <c>[QylHostedService]</c> for compile-time
///     <c>AddHostedService&lt;T&gt;()</c> registration.
/// </summary>
internal static class HostedServiceAnalyzer
{
    internal const string HostedServiceAttributeMetadataName = "Qyl.Contracts.Observability.QylHostedServiceAttribute";

    public static bool CouldBeHostedServiceClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    public static HostedServiceDefinition? Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
    {
        if (context.TargetNode is not ClassDeclarationSyntax)
            return null;

        if (IncrementalPipelineHelpers.IsGeneratedFile(context.TargetNode.SyntaxTree.FilePath))
            return null;

        if (context.TargetSymbol is not INamedTypeSymbol { IsAbstract: false } classSymbol)
            return null;

        return new HostedServiceDefinition(
            TypeFullyQualifiedName: classSymbol.GetFullyQualifiedName(),
            SortKey: IncrementalPipelineHelpers.FormatSortKey(context.TargetNode));
    }
}
