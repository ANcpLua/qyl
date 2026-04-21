using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

/// <summary>
///     Discovers classes tagged with <c>[QylHealthCheck(name, tags...)]</c> for auto-registration
///     via <c>services.AddHealthChecks().AddCheck&lt;T&gt;(name, failure, tags)</c>.
/// </summary>
internal static class QylHealthCheckAnalyzer
{
    internal const string QylHealthCheckAttributeMetadataName =
        "Qyl.Contracts.Observability.QylHealthCheckAttribute";

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

        if (IncrementalPipelineHelpers.FindAttributeByName(
                context.Attributes, context.SemanticModel.Compilation, QylHealthCheckAttributeMetadataName)
            is not { } attr)
            return null;

        if (attr.ConstructorArguments is not [{ Value: string name }, { Values: var tagConstants }, ..])
            return null;

        var tags = tagConstants.IsDefault
            ? default
            : Enumerable.Select(
                    Enumerable.Select(tagConstants, static t => t.Value as string)
                        .Where(static t => !string.IsNullOrEmpty(t)),
                    static t => t!).ToArray()
                .ToEquatableArray();

        return new QylHealthCheckDefinition(
            classSymbol.GetFullyQualifiedName(),
            name,
            tags,
            IncrementalPipelineHelpers.FormatSortKey(context.TargetNode));
    }
}
