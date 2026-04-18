using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

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
    /// <remarks>
    ///     This predicate is largely redundant with <c>ForAttributeWithMetadataName</c>, which already
    ///     filters by the <c>[Meter]</c> attribute. It provides marginal value by rejecting non-class
    ///     syntax nodes before the pipeline starts. Removing it would change the public pipeline API
    ///     signature, so it is kept as a required pipeline step. Could be tightened to check for
    ///     <c>partial</c> modifier if needed, but that would reject classes before a diagnostic can
    ///     suggest adding the keyword.
    /// </remarks>
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

        if (IncrementalPipelineHelpers.IsGeneratedFile(context.TargetNode.SyntaxTree.FilePath))
            return null;

        if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
            return null;

        return IncrementalPipelineHelpers.FindAttributeByName(context.Attributes, context.SemanticModel.Compilation,
            MeterAttributeMetadataName) is not { } meterAttr
            ? null
            : BuildDefinition(classSyntax, classSymbol, meterAttr, context.SemanticModel.Compilation,
                IncrementalPipelineHelpers.FormatSortKey(context.TargetNode));
    }

    private static MeterDefinition? BuildDefinition(
        ClassDeclarationSyntax classSyntax,
        INamedTypeSymbol classSymbol,
        AttributeData meterAttr,
        Compilation compilation,
        string sortKey)
    {
        // The generator requires a partial container but can target either static
        // or non-static classes as long as the metric methods themselves are static.
        if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            return null;

        var (meterName, meterVersion) = ExtractMeterAttributeValues(meterAttr);
        if (meterName is null)
            return null;

        var methods = ExtractMetricMethods(classSymbol, compilation);
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

        if (attr.TryGetConstructorArgument<string>(0, out var nameValue))
            name = nameValue;

        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg is { Key: "Version", Value.Value: string versionValue })
                version = versionValue;
        }

        return (name, version);
    }

    private static EquatableArray<MetricMethodDefinition> ExtractMetricMethods(
        INamespaceOrTypeSymbol classSymbol,
        Compilation compilation)
    {
        var methods = new List<MetricMethodDefinition>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IMethodSymbol { IsStatic: true } method)
                continue;

            if (!method.IsPartialDefinition)
                continue;

            var counterAttr =
                IncrementalPipelineHelpers.FindAttributeByName(method.GetAttributes(), compilation, CounterAttributeFullName);
            var histogramAttr =
                IncrementalPipelineHelpers.FindAttributeByName(method.GetAttributes(), compilation, HistogramAttributeFullName);
            var gaugeAttr =
                IncrementalPipelineHelpers.FindAttributeByName(method.GetAttributes(), compilation, GaugeAttributeFullName);
            var upDownCounterAttr =
                IncrementalPipelineHelpers.FindAttributeByName(method.GetAttributes(), compilation,
                    UpDownCounterAttributeFullName);

            var (kind, attr) = counterAttr is not null ? (MetricKind.Counter, counterAttr)
                : histogramAttr is not null ? (MetricKind.Histogram, histogramAttr)
                : gaugeAttr is not null ? (MetricKind.Gauge, gaugeAttr)
                : upDownCounterAttr is not null ? (MetricKind.UpDownCounter, upDownCounterAttr)
                : default;

            if (attr is null)
                continue;

            var (metricName, unit, description) = ExtractMetricAttributeValues(attr);
            var valueTypeName = FindMetricValueTypeName(method, compilation);

            if (metricName is null)
                continue;

            var tags = ExtractTags(method, compilation);

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

    private static string? FindMetricValueTypeName(IMethodSymbol method, Compilation compilation)
    {
        foreach (var param in method.Parameters.Where(param =>
                     IncrementalPipelineHelpers.FindAttributeByName(param.GetAttributes(), compilation, TagAttributeFullName) is
                         null))
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

        if (attr.TryGetConstructorArgument<string>(0, out var nameValue))
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

    private static EquatableArray<MetricTagParameter> ExtractTags(IMethodSymbol method, Compilation compilation)
    {
        var tags = new List<MetricTagParameter>();

        foreach (var param in method.Parameters)
        {
            if (IncrementalPipelineHelpers.FindAttributeByName(param.GetAttributes(), compilation, TagAttributeFullName) is not
                { } tagAttr)
                continue;

            string? explicitTagName = null;
            if (tagAttr.TryGetConstructorArgument<string>(0, out var tagNameValue))
                explicitTagName = tagNameValue;

            var tagName = TelemetryTagNameResolver.ResolveName(param, compilation, explicitTagName, param.Name);

            tags.Add(new MetricTagParameter(
                param.Name,
                tagName,
                param.Type.ToDisplayString()));
        }

        return tags.ToArray().ToEquatableArray();
    }
}
