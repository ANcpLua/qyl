using Microsoft.CodeAnalysis;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

internal static class MarkerExtractor
{
    public static MarkerModel? Extract(
        GeneratorAttributeSyntaxContext context,
        StabilityFilter filter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var attributeData = context.Attributes.FirstOrDefault();
        if (attributeData is null || attributeData.ConstructorArguments.Length == 0)
            return null;

        if (attributeData.ConstructorArguments[0].Value is not string prefix)
            return null;

        if (string.IsNullOrWhiteSpace(prefix))
            return null;

        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        return new MarkerModel(ns, typeSymbol.Name, prefix, filter);
    }
}
