using System.Collections.Immutable;
using Qyl.ServiceDefaults.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes types for [OTel] attributes on properties and parameters.
/// </summary>
internal static class OTelTagAnalyzer
{
    private const string OTelAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.OTelAttribute";

    /// <summary>
    ///     Predicate for filtering syntax nodes that might have [OTel] attributes.
    /// </summary>
    public static bool IsPotentialOTelMember(SyntaxNode node, CancellationToken _) =>
        node is PropertyDeclarationSyntax { AttributeLists.Count: > 0 } or ParameterSyntax { AttributeLists.Count: > 0 };

    /// <summary>
    ///     Transforms a syntax node into OTelTagInfo if it has an [OTel] attribute.
    /// </summary>
    public static OTelTagInfo? TransformToOTelTagInfo(
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

    private static OTelTagInfo? AnalyzeProperty(
        PropertyDeclarationSyntax property,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(property, cancellationToken) is not IPropertySymbol propertySymbol)
            return null;

        var otelAttr = FindOTelAttribute(propertySymbol.GetAttributes());
        if (otelAttr is null)
            return null;

        var containingType = propertySymbol.ContainingType;
        if (containingType is null)
            return null;

        var (attributeName, skipIfNull) = ExtractAttributeValues(otelAttr);
        if (attributeName is null)
            return null;

        return new OTelTagInfo(
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

    private static OTelTagInfo? AnalyzeParameter(
        ParameterSyntax parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(parameter, cancellationToken) is not IParameterSymbol parameterSymbol)
            return null;

        var otelAttr = FindOTelAttribute(parameterSymbol.GetAttributes());
        if (otelAttr is null)
            return null;

        var containingType = parameterSymbol.ContainingSymbol?.ContainingType;
        if (containingType is null)
            return null;

        var (attributeName, skipIfNull) = ExtractAttributeValues(otelAttr);
        if (attributeName is null)
            return null;

        return new OTelTagInfo(
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

    private static AttributeData? FindOTelAttribute(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attr in attributes)
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null)
                continue;

            var fullName = attrClass.ToDisplayString();
            if (fullName == OTelAttributeFullName)
                return attr;
        }

        return null;
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
