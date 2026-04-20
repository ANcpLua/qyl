namespace Qyl.Instrumentation.Generators.Loom.Extraction;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Models;

internal static class LoomStepExtractor
{
    public static LoomStepModel? Extract(GeneratorAttributeSyntaxContext context, CancellationToken _)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type ||
            context.TargetNode is not TypeDeclarationSyntax declaration)
            return null;

        var attribute = context.Attributes[0];
        var id = attribute.ConstructorArguments[0].Value as string;
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var phase = GetNamedInt(attribute, "Phase");
        var description = GetNamedString(attribute, "Description");

        return new LoomStepModel(
            id!,
            phase,
            type.GetFullyQualifiedName(),
            LoomDeclarationChainExtractor.Extract(declaration),
            description);
    }

    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
                return argument.Value.Value as string;
        }

        return null;
    }

    private static int GetNamedInt(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
                return argument.Value.Value as int? ?? 0;
        }

        return 0;
    }
}
