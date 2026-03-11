using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.Analyzers;

/// <summary>
///     Analyzes classes for [Meter] attributes and methods for [Counter]/[Histogram] attributes.
/// </summary>
internal static class MeterAnalyzer
{
    internal const string MeterAttributeMetadataName = "Qyl.Instrumentation.Instrumentation.MeterAttribute";
    private const string CounterAttributeFullName = "Qyl.Instrumentation.Instrumentation.CounterAttribute";
    private const string HistogramAttributeFullName = "Qyl.Instrumentation.Instrumentation.HistogramAttribute";
    private const string GaugeAttributeFullName = "Qyl.Instrumentation.Instrumentation.GaugeAttribute";
    private const string UpDownCounterAttributeFullName = "Qyl.Instrumentation.Instrumentation.UpDownCounterAttribute";
    private const string TagAttributeFullName = "Qyl.Instrumentation.Instrumentation.TagAttribute";

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be a [Meter] class?
    ///     Runs on every syntax node, so must be cheap (no semantic model).
    /// </summary>
    public static bool CouldBeMeterClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    /// <summary>
    ///     Extracts a meter definition from a targeted attribute context.
    ///     This is used with <c>ForAttributeWithMetadataName</c> for incremental performance.
    /// </summary>
    public static MeterDefinition? ExtractDefinitionFromAttribute(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
    {
        if (context.TargetNode is not ClassDeclarationSyntax classSyntax)
            return null;

        if (AnalyzerHelpers.IsGeneratedFile(context.TargetNode.SyntaxTree.FilePath))
            return null;

        if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
            return null;

        return AnalyzerHelpers.FindAttributeByName(context.Attributes, MeterAttributeMetadataName) is not { } meterAttr
            ? null
            : BuildDefinition(classSyntax, classSymbol, meterAttr, AnalyzerHelpers.FormatSortKey(context.TargetNode));
    }

    private static MeterDefinition? BuildDefinition(
        ClassDeclarationSyntax classSyntax,
        INamedTypeSymbol classSymbol,
        AttributeData meterAttr,
        string sortKey)
    {
        // The generator requires a partial container but can target either static
        // or non-static classes as long as the metric methods themselves are static.
        if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            return null;

        var (meterName, meterVersion) = ExtractMeterAttributeValues(meterAttr);
        if (meterName is null)
            return null;

        var methods = ExtractMetricMethods(classSymbol);
        if (methods.Length is 0)
            return null;

        return new MeterDefinition(
            sortKey,
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
            if (member is not IMethodSymbol { IsStatic: true } method)
                continue;

            if (!method.IsPartialDefinition)
                continue;

            var counterAttr = AnalyzerHelpers.FindAttributeByName(method.GetAttributes(), CounterAttributeFullName);
            var histogramAttr = AnalyzerHelpers.FindAttributeByName(method.GetAttributes(), HistogramAttributeFullName);
            var gaugeAttr = AnalyzerHelpers.FindAttributeByName(method.GetAttributes(), GaugeAttributeFullName);
            var upDownCounterAttr =
                AnalyzerHelpers.FindAttributeByName(method.GetAttributes(), UpDownCounterAttributeFullName);

            var (kind, attr) = counterAttr is not null ? (MetricKind.Counter, counterAttr)
                : histogramAttr is not null ? (MetricKind.Histogram, histogramAttr)
                : gaugeAttr is not null ? (MetricKind.Gauge, gaugeAttr)
                : upDownCounterAttr is not null ? (MetricKind.UpDownCounter, upDownCounterAttr)
                : default;

            if (attr is null)
                continue;

            var (metricName, unit, description) = ExtractMetricAttributeValues(attr);
            var valueTypeName = FindMetricValueTypeName(method);

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

    private static string? FindMetricValueTypeName(IMethodSymbol method)
    {
        foreach (var param in method.Parameters.Where(static param =>
                     AnalyzerHelpers.FindAttributeByName(param.GetAttributes(), TagAttributeFullName) is null))
        {
            return param.Type.ToDisplayString();
        }

        return null;
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
            if (AnalyzerHelpers.FindAttributeByName(param.GetAttributes(), TagAttributeFullName) is not { } tagAttr)
                continue;

            string? explicitTagName = null;
            if (tagAttr.ConstructorArguments.Length > 0 && tagAttr.ConstructorArguments[0].Value is string tagNameValue)
                explicitTagName = tagNameValue;

            var tagName = TelemetryTagNameResolver.ResolveName(param, explicitTagName, param.Name);

            tags.Add(new MetricTagParameter(
                param.Name,
                tagName,
                param.Type.ToDisplayString()));
        }

        return tags.ToArray().ToEquatableArray();
    }
}
