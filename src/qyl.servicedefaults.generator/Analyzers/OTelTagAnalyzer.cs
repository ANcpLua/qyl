using System.Collections.Immutable;
using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes types for [OTel] attributes on properties and parameters.
/// </summary>
internal static class OTelTagAnalyzer
{
    internal const string OTelAttributeMetadataName = "Qyl.ServiceDefaults.Instrumentation.OTelAttribute";

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node have an [OTel] attribute?
    ///     Runs on every syntax node, so must be cheap (no semantic model).
    /// </summary>
    public static bool CouldHaveOTelAttribute(SyntaxNode node, CancellationToken _) =>
        node is PropertyDeclarationSyntax { AttributeLists.Count: > 0 }
            or ParameterSyntax { AttributeLists.Count: > 0 };

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

    /// <summary>
    ///     Extracts an OTel tag binding from a targeted attribute context.
    ///     This is used with <c>ForAttributeWithMetadataName</c> for incremental performance.
    /// </summary>
    public static OTelTagBinding? ExtractTagBindingFromAttribute(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
    {
        if (AnalyzerHelpers.IsGeneratedFile(context.TargetNode.SyntaxTree.FilePath))
            return null;

        return context.TargetSymbol switch
        {
            IPropertySymbol propertySymbol => AnalyzeProperty(propertySymbol, context.Attributes),
            IParameterSymbol parameterSymbol => AnalyzeParameter(parameterSymbol, context.Attributes),
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

        if (AnalyzerHelpers.FindAttributeByName(propertySymbol.GetAttributes(), OTelAttributeMetadataName) is not { } otelAttr)
            return null;

        return AnalyzeProperty(propertySymbol, otelAttr);
    }

    private static OTelTagBinding? AnalyzeProperty(
        IPropertySymbol propertySymbol,
        ImmutableArray<AttributeData> attributes)
    {
        if (AnalyzerHelpers.FindAttributeByName(attributes, OTelAttributeMetadataName) is not { } otelAttr)
            return null;

        return AnalyzeProperty(propertySymbol, otelAttr);
    }

    private static OTelTagBinding? AnalyzeProperty(
        IPropertySymbol propertySymbol,
        AttributeData otelAttr)
    {
        if (propertySymbol.ContainingType is not { } containingType)
            return null;

        var (attributeName, skipIfNull) = ExtractAttributeValues(otelAttr);
        if (attributeName is null)
            return null;

        return new OTelTagBinding(
            containingType.GetFullyQualifiedName(),
            propertySymbol.Name,
            propertySymbol.Type.GetFullyQualifiedName(),
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

        if (AnalyzerHelpers.FindAttributeByName(parameterSymbol.GetAttributes(), OTelAttributeMetadataName) is not { } otelAttr)
            return null;

        return AnalyzeParameter(parameterSymbol, otelAttr);
    }

    private static OTelTagBinding? AnalyzeParameter(
        IParameterSymbol parameterSymbol,
        ImmutableArray<AttributeData> attributes)
    {
        if (AnalyzerHelpers.FindAttributeByName(attributes, OTelAttributeMetadataName) is not { } otelAttr)
            return null;

        return AnalyzeParameter(parameterSymbol, otelAttr);
    }

    private static OTelTagBinding? AnalyzeParameter(
        IParameterSymbol parameterSymbol,
        AttributeData otelAttr)
    {
        if (parameterSymbol.ContainingSymbol?.ContainingType is not { } containingType)
            return null;

        var (attributeName, skipIfNull) = ExtractAttributeValues(otelAttr);
        if (attributeName is null)
            return null;

        return new OTelTagBinding(
            containingType.GetFullyQualifiedName(),
            parameterSymbol.Name,
            parameterSymbol.Type.GetFullyQualifiedName(),
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
        {
            if (namedArg is
                {
                    Key: "SkipIfNull",
                    Value.Value: bool skipValue
                })
                skipIfNull = skipValue;
        }

        return (name, skipIfNull);
    }
}
