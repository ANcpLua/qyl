using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class QylHealthCheckAnalyzer
{
    internal const string QylHealthCheckAttributeMetadataName =
        "Qyl.Instrumentation.QylHealthCheckAttribute";

    public static bool CouldBeHealthCheckClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    public static QylHealthCheckDefinition? Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
    {
        if (context.TargetNode is not ClassDeclarationSyntax)
            return null;

        if (IncrementalPipelineHelpers.IsGeneratedFile(context.TargetNode.SyntaxTree.FilePath))
            return null;

        if (context.TargetSymbol is not INamedTypeSymbol { IsAbstract: false } classSymbol)
            return null;

        // ForAttributeWithMetadataName already filtered to [QylHealthCheck]; read the match directly.
        if (context.Attributes is not [{ } attr, ..])
            return null;

        if (attr.ConstructorArguments is not [{ Value: string name }, { Values: var tagConstants }, ..])
            return null;

        var tags = ExtractTags(tagConstants);

        return new QylHealthCheckDefinition(
            classSymbol.GetFullyQualifiedName(),
            name,
            tags,
            IncrementalPipelineHelpers.FormatSortKey(context.TargetNode));
    }

    private static EquatableArray<string> ExtractTags(ImmutableArray<TypedConstant> tagConstants)
    {
        if (tagConstants.IsDefault)
            return default;

        var tags = new List<string>();
        foreach (var tagConstant in tagConstants)
        {
            if (tagConstant.Value is string tag && !string.IsNullOrEmpty(tag))
                tags.Add(tag);
        }

        return tags.ToEquatableArray();
    }
}
