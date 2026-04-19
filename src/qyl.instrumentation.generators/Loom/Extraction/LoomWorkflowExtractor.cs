using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Loom.Models;

namespace Qyl.Instrumentation.Generators.Loom.Extraction;

internal static class LoomWorkflowExtractor
{
    public static LoomWorkflowModel? Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type ||
            context.TargetNode is not TypeDeclarationSyntax declaration)
            return null;

        var attribute = context.Attributes[0];
        var id = attribute.ConstructorArguments[0].Value as string;
        var runStateType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;

        if (string.IsNullOrWhiteSpace(id) || runStateType is null)
            return null;

        var stepIds = attribute.ConstructorArguments[2].Values
            .Select(static value => value.Value as string)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        return new LoomWorkflowModel(
            id!,
            runStateType.GetFullyQualifiedName(),
            stepIds.Length is 0 ? default : stepIds.ToEquatableArray(),
            type.GetFullyQualifiedName(),
            LoomDeclarationChainExtractor.Extract(declaration),
            GetNamedString(attribute, "Description"));
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
}
