using System.Collections.Immutable;
using Qyl.ServiceDefaults.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes types for [OTel] attributes on properties and parameters.
/// </summary>
internal static class OTelTagAnalyzer
{
    private const string OTelAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.OTelAttribute";

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node have an [OTel] attribute?
    ///     Runs on every syntax node, so must be cheap (no semantic model).
    /// </summary>
    public static bool CouldHaveOTelAttribute(SyntaxNode node, CancellationToken _) =>
        node is PropertyDeclarationSyntax { AttributeLists.Count: > 0 } or ParameterSyntax { AttributeLists.Count: > 0 };

    /// <summary>
    ///     Extracts an OTel tag binding from a syntax context if it has an [OTel] attribute.
    ///     Returns null if no [OTel] attribute is present.
    /// </summary>
    public static OTelTagBinding? ExtractTagBinding(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var semanticModel = context.SemanticModel;

        return context.Node switch
        {
            PropertyDeclarationSyntax property => AnalyzeProperty(property, semanticModel, cancellationToken),
            ParameterSyntax parameter => AnalyzeParameter(parameter, semanticModel, cancellationToken),
            _ => null
        };
    }

    private static OTelTagBinding? AnalyzeProperty(
        PropertyDeclarationSyntax property,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(property, cancellationToken) is not { } propertySymbol)
            return null;

        var otelAttr = AnalyzerHelpers.FindAttributeByName(propertySymbol.GetAttributes(), OTelAttributeFullName);
        if (otelAttr is null)
            return null;

        var containingType = propertySymbol.ContainingType;
        if (containingType is null)
            return null;

        var (attributeName, skipIfNull) = ExtractAttributeValues(otelAttr);
        if (attributeName is null)
            return null;

        return new OTelTagBinding(
            containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            propertySymbol.Name,
            propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            attributeName,
            skipIfNull,
            propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated ||
            propertySymbol.Type is
            {
                IsValueType: true,
                OriginalDefinition.SpecialType: SpecialType.None
            });
    }

    private static OTelTagBinding? AnalyzeParameter(
        ParameterSyntax parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(parameter, cancellationToken) is not { } parameterSymbol)
            return null;

        var otelAttr = AnalyzerHelpers.FindAttributeByName(parameterSymbol.GetAttributes(), OTelAttributeFullName);
        if (otelAttr is null)
            return null;

        var containingType = parameterSymbol.ContainingSymbol?.ContainingType;
        if (containingType is null)
            return null;

        var (attributeName, skipIfNull) = ExtractAttributeValues(otelAttr);
        if (attributeName is null)
            return null;

        return new OTelTagBinding(
            containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            parameterSymbol.Name,
            parameterSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            attributeName,
            skipIfNull,
            parameterSymbol.Type.NullableAnnotation == NullableAnnotation.Annotated ||
            parameterSymbol.Type is
            {
                IsValueType: true,
                OriginalDefinition.SpecialType: SpecialType.None
            });
    }


    private static (string? Name, bool SkipIfNull) ExtractAttributeValues(AttributeData attr)
    {
        string? name = null;
        var skipIfNull = true;

        if (attr.ConstructorArguments.Length > 0)
        {
            var arg = attr.ConstructorArguments[0];
            if (arg.Value is string nameValue)
                name = nameValue;
        }

        foreach (var namedArg in attr.NamedArguments)
            if (namedArg is
                {
                    Key: "SkipIfNull",
                    Value.Value: bool skipValue
                })
                skipIfNull = skipValue;

        return (name, skipIfNull);
    }
}
