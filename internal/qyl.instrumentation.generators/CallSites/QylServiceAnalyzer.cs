using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class QylServiceAnalyzer
{
    internal const string QylServiceAttributeMetadataName = "Qyl.Instrumentation.QylServiceAttribute";

    private static readonly string[] s_lifetimeNames = ["Singleton", "Scoped", "Transient"];

    public static bool CouldBeQylServiceClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    public static QylServiceDefinition? Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
    {
        if (context.TargetNode is not ClassDeclarationSyntax)
            return null;

        if (IncrementalPipelineHelpers.IsGeneratedFile(context.TargetNode.SyntaxTree.FilePath))
            return null;

        if (context.TargetSymbol is not INamedTypeSymbol { IsAbstract: false } classSymbol)
            return null;

        // ForAttributeWithMetadataName already filtered to [QylService]; read the match directly.
        if (context.Attributes is not [{ } attr, ..])
            return null;

        var lifetimeIndex = attr.ConstructorArguments is [{ Value: int i }, ..] ? i : 0;
        if (lifetimeIndex < 0 || lifetimeIndex >= s_lifetimeNames.Length)
            lifetimeIndex = 0;

        string? interfaceFqn = null;
        if (attr.ConstructorArguments is [_, { Value: INamedTypeSymbol iface }, ..])
            interfaceFqn = iface.GetFullyQualifiedName();

        return new QylServiceDefinition(
            classSymbol.GetFullyQualifiedName(),
            s_lifetimeNames[lifetimeIndex],
            interfaceFqn,
            IncrementalPipelineHelpers.FormatSortKey(context.TargetNode));
    }
}
