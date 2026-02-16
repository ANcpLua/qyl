using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes classes for [Meter] attributes and methods for [Counter]/[Histogram] attributes.
/// </summary>
internal static class MeterAnalyzer
{
    private const string MeterAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.MeterAttribute";
    private const string CounterAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.CounterAttribute";
    private const string HistogramAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.HistogramAttribute";
    private const string GaugeAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.GaugeAttribute";
    private const string UpDownCounterAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.UpDownCounterAttribute";
    private const string TagAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.TagAttribute";

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be a [Meter] class?
    ///     Runs on every syntax node, so must be cheap (no semantic model).
    /// </summary>
    public static bool CouldBeMeterClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    /// <summary>
    ///     Extracts a meter definition from a syntax context if it has a [Meter] attribute.
    ///     Returns null if no [Meter] attribute or if the class is not properly decorated.
    /// </summary>
    public static MeterDefinition? ExtractDefinition(
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

        var meterAttr = AnalyzerHelpers.FindAttributeByName(classSymbol.GetAttributes(), MeterAttributeFullName);
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
        if (methods.Length is 0)
            return null;

        return new MeterDefinition(
            AnalyzerHelpers.FormatSortKey(context.Node),
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.Name,
            meterName,
            meterVersion,
            methods);
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

    private static EquatableArray<MetricMethodDefinition> ExtractMetricMethods(INamespaceOrTypeSymbol classSymbol)
    {
        var methods = new List<MetricMethodDefinition>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            if (!method.IsPartialDefinition)
                continue;

            var counterAttr = AnalyzerHelpers.FindAttributeByName(method.GetAttributes(), CounterAttributeFullName);
            var histogramAttr = AnalyzerHelpers.FindAttributeByName(method.GetAttributes(), HistogramAttributeFullName);
            var gaugeAttr = AnalyzerHelpers.FindAttributeByName(method.GetAttributes(), GaugeAttributeFullName);
            var upDownCounterAttr =
                AnalyzerHelpers.FindAttributeByName(method.GetAttributes(), UpDownCounterAttributeFullName);

            if (counterAttr is null && histogramAttr is null && gaugeAttr is null && upDownCounterAttr is null)
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
                    if (AnalyzerHelpers.FindAttributeByName(param.GetAttributes(), TagAttributeFullName) is null)
                    {
                        valueTypeName = param.Type.ToDisplayString();
                        break;
                    }
                }
            }
            else if (gaugeAttr is not null)
            {
                kind = MetricKind.Gauge;
                (metricName, unit, description) = ExtractMetricAttributeValues(gaugeAttr);

                // First non-tagged parameter is the value for gauge
                foreach (var param in method.Parameters)
                {
                    if (AnalyzerHelpers.FindAttributeByName(param.GetAttributes(), TagAttributeFullName) is null)
                    {
                        valueTypeName = param.Type.ToDisplayString();
                        break;
                    }
                }
            }
            else if (upDownCounterAttr is not null)
            {
                kind = MetricKind.UpDownCounter;
                (metricName, unit, description) = ExtractMetricAttributeValues(upDownCounterAttr);

                // First non-tagged parameter is the delta value for up-down counter
                foreach (var param in method.Parameters)
                {
                    if (AnalyzerHelpers.FindAttributeByName(param.GetAttributes(), TagAttributeFullName) is null)
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

            methods.Add(new MetricMethodDefinition(
                method.Name,
                kind,
                metricName,
                unit,
                description,
                valueTypeName,
                tags));
        }

        return methods.ToArray().ToEquatableArray();
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

    private static EquatableArray<MetricTagParameter> ExtractTags(IMethodSymbol method)
    {
        var tags = new List<MetricTagParameter>();

        foreach (var param in method.Parameters)
        {
            var tagAttr = AnalyzerHelpers.FindAttributeByName(param.GetAttributes(), TagAttributeFullName);
            if (tagAttr is null)
                continue;

            string? tagName = null;
            if (tagAttr.ConstructorArguments.Length > 0 && tagAttr.ConstructorArguments[0].Value is string tagNameValue)
                tagName = tagNameValue;

            if (tagName is null)
                continue;

            tags.Add(new MetricTagParameter(
                param.Name,
                tagName,
                param.Type.ToDisplayString()));
        }

        return tags.ToArray().ToEquatableArray();
    }
}
