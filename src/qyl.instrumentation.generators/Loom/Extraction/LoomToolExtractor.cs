namespace Qyl.Instrumentation.Generators.Loom.Extraction;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Models;

internal static class LoomToolExtractor
{
    public static LoomToolModel? Extract(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not IMethodSymbol method ||
            context.TargetNode is not MethodDeclarationSyntax declaration ||
            declaration.Parent is not TypeDeclarationSyntax containingType)
            return null;

        var attribute = context.Attributes[0];
        var name = attribute.ConstructorArguments[0].Value as string;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var description = GetNamedString(attribute, "Description") ?? string.Empty;
        var useOnlyWhen = GetNamedString(attribute, "UseOnlyWhen");
        var doNotUseWhen = GetNamedString(attribute, "DoNotUseWhen");
        var phase = GetNamedInt(attribute, "Phase");

        var structuredOutputType = LoomPolicyExtractor.ExtractStructuredOutputType(method, method.ContainingType);
        var budget = LoomPolicyExtractor.ExtractBudget(method, method.ContainingType);
        var requiredCapabilities = LoomPolicyExtractor.ExtractCapabilities(method, method.ContainingType);
        var requiresApproval = LoomPolicyExtractor.ExtractRequiresApproval(method, method.ContainingType);
        var sideEffect = LoomPolicyExtractor.ExtractSideEffect(method, method.ContainingType);
        var parameters = LoomParameterExtractor.Extract(method.Parameters, cancellationToken);
        var outputType = GetOutputType(method);
        var result = new LoomToolResultModel(
            outputType,
            structuredOutputType,
            structuredOutputType ?? outputType,
            structuredOutputType is not null,
            outputType is not null || structuredOutputType is not null);

        return new LoomToolModel(
            name!,
            description,
            phase,
            useOnlyWhen,
            doNotUseWhen,
            method.ContainingType.GetFullyQualifiedName(),
            LoomDeclarationChainExtractor.Extract(containingType),
            method.Name,
            method.IsStatic,
            IsAwaitable(method.ReturnType),
            outputType is not null,
            outputType,
            structuredOutputType,
            result,
            budget,
            parameters,
            requiredCapabilities,
            requiresApproval,
            sideEffect);
    }

    private static bool IsAwaitable(ITypeSymbol typeSymbol) => typeSymbol.Name is "Task" or "ValueTask";

    private static string? GetOutputType(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        if (returnType.SpecialType == SpecialType.System_Void)
            return null;

        if (returnType is INamedTypeSymbol
            {
                IsGenericType: true, TypeArguments.Length: 1, Name: "Task" or "ValueTask"
            } namedType)
        {
            return namedType.TypeArguments[0].GetFullyQualifiedName();
        }

        if (returnType.Name is "Task" or "ValueTask")
            return null;

        return returnType.GetFullyQualifiedName();
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

    private static int GetNamedInt(AttributeData attribute, string name, int defaultValue = 0)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
                return argument.Value.Value as int? ?? defaultValue;
        }

        return defaultValue;
    }
}
