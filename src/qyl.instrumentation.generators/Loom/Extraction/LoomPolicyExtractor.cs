using Microsoft.CodeAnalysis;
using Qyl.Instrumentation.Generators.Loom.Models;

namespace Qyl.Instrumentation.Generators.Loom.Extraction;

internal static class LoomPolicyExtractor
{
    private const string LoomBudgetAttributeName = "LoomBudgetAttribute";
    private const string StructuredOutputAttributeName = "EmitsStructuredOutputAttribute";
    private const string RequiresCapabilityAttributeName = "RequiresCapabilityAttribute";
    private const string RequiresApprovalAttributeName = "RequiresApprovalAttribute";
    private const string ToolSideEffectAttributeName = "ToolSideEffectAttribute";

    public static LoomToolBudgetModel ExtractBudget(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        if (TryGetBudget(method, out var budget))
            return budget;

        if (TryGetBudget(containingType, out budget))
            return budget;

        return new LoomToolBudgetModel(1, 8, 16000);
    }

    public static EquatableArray<string> ExtractCapabilities(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var capabilities = new List<string>();
        AppendCapabilities(capabilities, method);
        AppendCapabilities(capabilities, containingType);
        return capabilities.Count is 0 ? default : capabilities.ToArray().ToEquatableArray();
    }

    public static bool ExtractRequiresApproval(IMethodSymbol method, INamedTypeSymbol containingType)
        => GetAttribute(method, RequiresApprovalAttributeName) is not null ||
           GetAttribute(containingType, RequiresApprovalAttributeName) is not null;

    public static int ExtractSideEffect(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var attribute = GetAttribute(method, ToolSideEffectAttributeName) ??
                        GetAttribute(containingType, ToolSideEffectAttributeName);
        return attribute?.ConstructorArguments.FirstOrDefault().Value as int? ?? 0;
    }

    public static string? ExtractStructuredOutputType(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var attribute = GetAttribute(method, StructuredOutputAttributeName) ??
                        GetAttribute(containingType, StructuredOutputAttributeName);
        if (attribute is null)
            return null;

        return attribute.ConstructorArguments.FirstOrDefault().Value is ITypeSymbol typeSymbol
            ? typeSymbol.GetFullyQualifiedName()
            : null;
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

    private static AttributeData? GetAttribute(ISymbol symbol, string attributeName)
        => symbol.GetAttributes().FirstOrDefault(attribute =>
            string.Equals(attribute.AttributeClass?.Name, attributeName, StringComparison.Ordinal));

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
