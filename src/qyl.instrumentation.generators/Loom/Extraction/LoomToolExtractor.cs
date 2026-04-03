using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Loom.Models;

namespace Qyl.Instrumentation.Generators.Loom.Extraction;

internal static class LoomToolExtractor
{
    private const string LoomBudgetAttributeName = "LoomBudgetAttribute";
    private const string StructuredOutputAttributeName = "EmitsStructuredOutputAttribute";
    private const string RequiresCapabilityAttributeName = "RequiresCapabilityAttribute";
    private const string RequiresApprovalAttributeName = "RequiresApprovalAttribute";
    private const string ToolSideEffectAttributeName = "ToolSideEffectAttribute";

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

        var structuredOutputType = GetStructuredOutputType(method, containingType);
        var budget = GetBudget(method, containingType);
        var requiredCapabilities = GetRequiredCapabilities(method, containingType);
        var requiresApproval = HasAttribute(method, containingType, RequiresApprovalAttributeName);
        var sideEffect = GetSideEffect(method, containingType);
        var outputType = GetOutputType(method);

        return new LoomToolModel(
            name!,
            description,
            phase,
            useOnlyWhen,
            doNotUseWhen,
            method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            LoomDeclarationChainExtractor.Extract(containingType),
            method.Name,
            method.IsStatic,
            IsAwaitable(method.ReturnType),
            outputType is not null,
            outputType,
            structuredOutputType,
            budget,
            requiredCapabilities,
            requiresApproval,
            sideEffect);
    }

    private static LoomToolBudgetModel GetBudget(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        if (TryGetBudget(method, out var budget))
            return budget;

        if (TryGetBudget(containingType, out budget))
            return budget;

        return new LoomToolBudgetModel(1, 8, 16000);
    }

    private static bool TryGetBudget(ISymbol symbol, out LoomToolBudgetModel budget)
    {
        var attribute = GetAttribute(symbol, LoomBudgetAttributeName);
        if (attribute is null)
        {
            budget = default;
            return false;
        }

        budget = new LoomToolBudgetModel(
            GetNamedInt(attribute, "MaxAttempts", 1),
            GetNamedInt(attribute, "MaxToolCalls", 8),
            GetNamedInt(attribute, "MaxTokens", 16000));
        return true;
    }

    private static string? GetStructuredOutputType(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var attribute = GetAttribute(method, StructuredOutputAttributeName) ?? GetAttribute(containingType, StructuredOutputAttributeName);
        if (attribute is null)
            return null;

        return attribute.ConstructorArguments.FirstOrDefault().Value is ITypeSymbol typeSymbol
            ? typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;
    }

    private static EquatableArray<string> GetRequiredCapabilities(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var capabilities = new List<string>();
        AppendCapabilities(capabilities, method);
        AppendCapabilities(capabilities, containingType);
        return capabilities.Count is 0 ? default : capabilities.ToArray().ToEquatableArray();
    }

    private static void AppendCapabilities(List<string> capabilities, ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!string.Equals(attribute.AttributeClass?.Name, RequiresCapabilityAttributeName, StringComparison.Ordinal))
                continue;

            var capability = attribute.ConstructorArguments.FirstOrDefault().Value as string;
            if (!string.IsNullOrWhiteSpace(capability))
                capabilities.Add(capability!);
        }
    }

    private static bool HasAttribute(IMethodSymbol method, INamedTypeSymbol containingType, string attributeName)
        => GetAttribute(method, attributeName) is not null || GetAttribute(containingType, attributeName) is not null;

    private static int GetSideEffect(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var attribute = GetAttribute(method, ToolSideEffectAttributeName) ?? GetAttribute(containingType, ToolSideEffectAttributeName);
        return attribute?.ConstructorArguments.FirstOrDefault().Value as int? ?? 0;
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string attributeName)
        => symbol.GetAttributes().FirstOrDefault(attribute =>
            string.Equals(attribute.AttributeClass?.Name, attributeName, StringComparison.Ordinal));

    private static bool IsAwaitable(ITypeSymbol typeSymbol)
    {
        return typeSymbol.Name is "Task" or "ValueTask";
    }

    private static string? GetOutputType(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        if (returnType.SpecialType == SpecialType.System_Void)
            return null;

        if (returnType is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.TypeArguments.Length == 1 &&
            namedType.Name is "Task" or "ValueTask")
        {
            return namedType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        if (returnType.Name is "Task" or "ValueTask")
            return null;

        return returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
