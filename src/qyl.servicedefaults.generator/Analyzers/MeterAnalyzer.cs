using System.Collections.Immutable;
using Qyl.ServiceDefaults.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes classes for [Meter] attributes and methods for [Counter]/[Histogram] attributes.
/// </summary>
internal static class MeterAnalyzer
{
    private const string MeterAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.MeterAttribute";
    private const string CounterAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.CounterAttribute";
    private const string HistogramAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.HistogramAttribute";
    private const string TagAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.TagAttribute";

    /// <summary>
    ///     Predicate for filtering syntax nodes that might be [Meter] classes.
    /// </summary>
    public static bool IsPotentialMeterClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    /// <summary>
    ///     Transforms a syntax node into MeterClassInfo if it has a [Meter] attribute.
    /// </summary>
    public static MeterClassInfo? TransformToMeterClassInfo(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.Node is not ClassDeclarationSyntax classSyntax)
            return null;

        if (AnalyzerHelpers.IsGeneratedFile(context.Node.SyntaxTree.FilePath))
            return null;

        var semanticModel = context.SemanticModel;

        if (semanticModel.GetDeclaredSymbol(classSyntax, cancellationToken) is not { } classSymbol)
            return null;

        var meterAttr = FindAttribute(classSymbol.GetAttributes(), MeterAttributeFullName);
        if (meterAttr is null)
            return null;

        // Must be partial and static
        if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword) ||
            !classSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
            return null;

        var (meterName, meterVersion) = ExtractMeterAttributeValues(meterAttr);
        if (meterName is null)
            return null;

        var methods = ExtractMetricMethods(classSymbol);
        if (methods.Count == 0)
            return null;

        return new MeterClassInfo(
            AnalyzerHelpers.FormatOrderKey(context.Node),
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.Name,
            meterName,
            meterVersion,
            methods);
    }

    private static AttributeData? FindAttribute(ImmutableArray<AttributeData> attributes, string fullName)
    {
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.ToDisplayString() == fullName)
                return attr;
        }

        return null;
    }

    private static (string? Name, string? Version) ExtractMeterAttributeValues(AttributeData attr)
    {
        string? name = null;
        string? version = null;

        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string nameValue)
            name = nameValue;

        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg is { Key: "Version", Value.Value: string versionValue })
                version = versionValue;
        }

        return (name, version);
    }

    private static List<MetricMethodInfo> ExtractMetricMethods(INamedTypeSymbol classSymbol)
    {
        var methods = new List<MetricMethodInfo>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            if (!method.IsPartialDefinition)
                continue;

            var counterAttr = FindAttribute(method.GetAttributes(), CounterAttributeFullName);
            var histogramAttr = FindAttribute(method.GetAttributes(), HistogramAttributeFullName);

            if (counterAttr is null && histogramAttr is null)
                continue;

            MetricKind kind;
            string? metricName;
            string? unit;
            string? description;
            string? valueTypeName = null;

            if (counterAttr is not null)
            {
                kind = MetricKind.Counter;
                (metricName, unit, description) = ExtractMetricAttributeValues(counterAttr);
            }
            else if (histogramAttr is not null)
            {
                kind = MetricKind.Histogram;
                (metricName, unit, description) = ExtractMetricAttributeValues(histogramAttr);

                // First non-tagged parameter is the value for histogram
                foreach (var param in method.Parameters)
                {
                    if (FindAttribute(param.GetAttributes(), TagAttributeFullName) is null)
                    {
                        valueTypeName = param.Type.ToDisplayString();
                        break;
                    }
                }
            }
            else
            {
                continue;
            }

            if (metricName is null)
                continue;

            var tags = ExtractTags(method);

            methods.Add(new MetricMethodInfo(
                method.Name,
                kind,
                metricName,
                unit,
                description,
                valueTypeName,
                tags));
        }

        return methods;
    }

    private static (string? Name, string? Unit, string? Description) ExtractMetricAttributeValues(AttributeData attr)
    {
        string? name = null;
        string? unit = null;
        string? description = null;

        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string nameValue)
            name = nameValue;

        foreach (var namedArg in attr.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Unit" when namedArg.Value.Value is string unitValue:
                    unit = unitValue;
                    break;
                case "Description" when namedArg.Value.Value is string descValue:
                    description = descValue;
                    break;
            }
        }

        return (name, unit, description);
    }

    private static List<MetricTagInfo> ExtractTags(IMethodSymbol method)
    {
        var tags = new List<MetricTagInfo>();

        foreach (var param in method.Parameters)
        {
            var tagAttr = FindAttribute(param.GetAttributes(), TagAttributeFullName);
            if (tagAttr is null)
                continue;

            string? tagName = null;
            if (tagAttr.ConstructorArguments.Length > 0 && tagAttr.ConstructorArguments[0].Value is string tagNameValue)
                tagName = tagNameValue;

            if (tagName is null)
                continue;

            tags.Add(new MetricTagInfo(
                param.Name,
                tagName,
                param.Type.ToDisplayString()));
        }

        return tags;
    }
}
