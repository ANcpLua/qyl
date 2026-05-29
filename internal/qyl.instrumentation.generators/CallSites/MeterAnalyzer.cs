using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class MeterAnalyzer
{
    internal const string MeterAttributeMetadataName = "Qyl.Instrumentation.Instrumentation.MeterAttribute";
    private const string CounterAttributeFullName = "Qyl.Instrumentation.Instrumentation.CounterAttribute";
    private const string HistogramAttributeFullName = "Qyl.Instrumentation.Instrumentation.HistogramAttribute";
    private const string GaugeAttributeFullName = "Qyl.Instrumentation.Instrumentation.GaugeAttribute";
    private const string UpDownCounterAttributeFullName = "Qyl.Instrumentation.Instrumentation.UpDownCounterAttribute";
    private const string ObservableCounterAttributeFullName =
        "Qyl.Instrumentation.Instrumentation.ObservableCounterAttribute";
    private const string ObservableGaugeAttributeFullName = "Qyl.Instrumentation.Instrumentation.ObservableGaugeAttribute";
    private const string ObservableUpDownCounterAttributeFullName =
        "Qyl.Instrumentation.Instrumentation.ObservableUpDownCounterAttribute";
    private const string TagAttributeFullName = "Qyl.Instrumentation.Instrumentation.TagAttribute";

    public static bool CouldBeMeterClass(SyntaxNode node, CancellationToken _) =>
        node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

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

        var attributeTypes = MetricAttributeTypes.Create(context.SemanticModel.Compilation);
        return FindAttribute(context.Attributes, attributeTypes.Meter) is not { } meterAttr
            ? null
            : BuildDefinition(classSyntax, classSymbol, meterAttr, attributeTypes,
                IncrementalPipelineHelpers.FormatSortKey(context.TargetNode));
    }

    private static MeterDefinition? BuildDefinition(
        ClassDeclarationSyntax classSyntax,
        INamedTypeSymbol classSymbol,
        AttributeData meterAttr,
        MetricAttributeTypes attributeTypes,
        string sortKey)
    {
        if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            return null;

        if (classSymbol.TypeParameters.Length is not 0)
            return null;

        if (!HasSupportedContainingTypeChain(classSyntax))
            return null;

        var (meterName, meterVersion) = ExtractMeterAttributeValues(meterAttr);
        if (meterName is null)
            return null;

        var methods = ExtractMetricMethods(classSymbol, attributeTypes);
        if (methods.Length is 0)
            return null;

        return new MeterDefinition(
            sortKey,
            classSymbol.ContainingNamespace.ToDisplayString(),
            BuildContainingTypes(classSyntax, classSymbol),
            EscapeIdentifier(classSymbol.Name),
            BuildClassModifiers(classSyntax, classSymbol),
            meterName,
            meterVersion,
            methods);
    }

    private static string BuildClassModifiers(ClassDeclarationSyntax classSyntax, INamedTypeSymbol classSymbol)
    {
        var modifiers = new List<string>
        {
            GetAccessibility(classSymbol.DeclaredAccessibility)
        };

        if (classSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
            modifiers.Add("static");
        else
        {
            if (classSyntax.Modifiers.Any(SyntaxKind.AbstractKeyword))
                modifiers.Add("abstract");

            if (classSyntax.Modifiers.Any(SyntaxKind.SealedKeyword))
                modifiers.Add("sealed");
        }

        modifiers.Add("partial");
        return string.Join(" ", modifiers);
    }

    private static bool HasSupportedContainingTypeChain(ClassDeclarationSyntax classSyntax)
    {
        for (var parent = classSyntax.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is BaseTypeDeclarationSyntax containingType)
            {
                if (containingType is not ClassDeclarationSyntax containingClass ||
                    !containingClass.Modifiers.Any(SyntaxKind.PartialKeyword) ||
                    containingClass.TypeParameterList is not null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) is SyntaxKind.None
            ? identifier
            : string.Concat("@", identifier);

    private static EquatableArray<MeterContainingTypeDefinition> BuildContainingTypes(
        ClassDeclarationSyntax classSyntax,
        INamedTypeSymbol classSymbol)
    {
        var syntaxTypes = new List<ClassDeclarationSyntax>();
        for (var parent = classSyntax.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ClassDeclarationSyntax containingClass)
                syntaxTypes.Add(containingClass);
        }

        if (syntaxTypes.Count is 0)
            return default;

        syntaxTypes.Reverse();

        var symbolTypes = new List<INamedTypeSymbol>();
        for (var containingType = classSymbol.ContainingType;
             containingType is not null;
             containingType = containingType.ContainingType)
        {
            symbolTypes.Add(containingType);
        }

        symbolTypes.Reverse();

        var containingTypes = new List<MeterContainingTypeDefinition>(syntaxTypes.Count);
        for (var i = 0; i < syntaxTypes.Count; i++)
        {
            var syntaxType = syntaxTypes[i];
            var symbolType = i < symbolTypes.Count ? symbolTypes[i] : classSymbol;
            containingTypes.Add(new MeterContainingTypeDefinition(
                EscapeIdentifier(symbolType.Name),
                BuildClassModifiers(syntaxType, symbolType)));
        }

        return containingTypes.ToEquatableArray();
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
        MetricAttributeTypes attributeTypes)
    {
        var methods = new List<MetricMethodDefinition>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IMethodSymbol { IsStatic: true } method)
                continue;

            if (!TryGetMetricAttribute(method, attributeTypes, out var kind, out var attr))
                continue;

            if (method.TypeParameters.Length is not 0)
                continue;

            var (metricName, unit, description) = ExtractMetricAttributeValues(attr);
            var isObservable = IsObservable(kind);

            if (!isObservable && !method.IsPartialDefinition)
                continue;

            if (!isObservable && (!method.ReturnsVoid || HasUnsupportedMetricParameters(method)))
                continue;

            string? valueTypeName;
            string? valueParameterName;
            ObservableCallbackKind callbackKind;
            if (isObservable)
            {
                (valueTypeName, callbackKind) = FindObservableValueTypeName(method);
                valueParameterName = null;
            }
            else
            {
                var valueParameter = FindMetricValueParameter(method, attributeTypes.Tag);
                if (!HasSupportedValueParameterShape(kind, valueParameter.Count) ||
                    (valueParameter.Type is not null && !IsSupportedMetricValueType(valueParameter.Type)))
                {
                    continue;
                }

                valueTypeName = valueParameter.TypeName;
                valueParameterName = valueParameter.Name;
                callbackKind = ObservableCallbackKind.None;
            }

            if (metricName is null || (isObservable && valueTypeName is null))
                continue;

            var tags = isObservable ? default : ExtractTags(method, attributeTypes);

            methods.Add(new MetricMethodDefinition(
                EscapeIdentifier(method.Name),
                GetAccessibility(method.DeclaredAccessibility),
                kind,
                metricName,
                unit,
                description,
                valueTypeName,
                valueParameterName,
                callbackKind,
                tags));
        }

        return methods.ToEquatableArray();
    }

    private static bool TryGetMetricAttribute(
        IMethodSymbol method,
        MetricAttributeTypes attributeTypes,
        out MetricKind kind,
        [NotNullWhen(true)] out AttributeData? attribute)
    {
        var attributes = method.GetAttributes();

        if (FindAttribute(attributes, attributeTypes.Counter) is { } counterAttr)
        {
            kind = MetricKind.Counter;
            attribute = counterAttr;
            return true;
        }

        if (FindAttribute(attributes, attributeTypes.Histogram) is { } histogramAttr)
        {
            kind = MetricKind.Histogram;
            attribute = histogramAttr;
            return true;
        }

        if (FindAttribute(attributes, attributeTypes.Gauge) is { } gaugeAttr)
        {
            kind = MetricKind.Gauge;
            attribute = gaugeAttr;
            return true;
        }

        if (FindAttribute(attributes, attributeTypes.UpDownCounter) is { } upDownCounterAttr)
        {
            kind = MetricKind.UpDownCounter;
            attribute = upDownCounterAttr;
            return true;
        }

        if (FindAttribute(attributes, attributeTypes.ObservableCounter) is { } observableCounterAttr)
        {
            kind = MetricKind.ObservableCounter;
            attribute = observableCounterAttr;
            return true;
        }

        if (FindAttribute(attributes, attributeTypes.ObservableGauge) is { } observableGaugeAttr)
        {
            kind = MetricKind.ObservableGauge;
            attribute = observableGaugeAttr;
            return true;
        }

        if (FindAttribute(attributes, attributeTypes.ObservableUpDownCounter) is { } observableUpDownCounterAttr)
        {
            kind = MetricKind.ObservableUpDownCounter;
            attribute = observableUpDownCounterAttr;
            return true;
        }

        kind = default;
        attribute = null;
        return false;
    }

    private static AttributeData? FindAttribute(
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
            return null;

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass.IsEqualTo(attributeType))
                return attribute;
        }

        return null;
    }

    private static bool HasUnsupportedMetricParameters(IMethodSymbol method)
    {
        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind is not RefKind.None || parameter.IsParams)
                return true;
        }

        return false;
    }

    private static string GetAccessibility(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private"
        };

    private static (int Count, string? TypeName, string? Name, ITypeSymbol? Type) FindMetricValueParameter(
        IMethodSymbol method,
        INamedTypeSymbol? tagAttributeType)
    {
        var count = 0;
        string? typeName = null;
        string? name = null;
        ITypeSymbol? type = null;

        foreach (var param in method.Parameters)
        {
            if (FindAttribute(param.GetAttributes(), tagAttributeType) is not null)
                continue;

            count++;
            typeName ??= param.Type.ToDisplayString();
            name ??= EscapeIdentifier(param.Name);
            type ??= param.Type;
        }

        return (count, typeName, name, type);
    }

    private static bool HasSupportedValueParameterShape(MetricKind kind, int valueParameterCount) =>
        kind switch
        {
            MetricKind.Counter => valueParameterCount is 0 or 1,
            MetricKind.Histogram or MetricKind.Gauge or MetricKind.UpDownCounter => valueParameterCount is 1,
            _ => false
        };

    private static (string? ValueTypeName, ObservableCallbackKind CallbackKind) FindObservableValueTypeName(
        IMethodSymbol method)
    {
        if (method.Parameters.Length is not 0 || method.ReturnsVoid)
            return (null, ObservableCallbackKind.None);

        if (TryGetMeasurementValueType(method.ReturnType) is { } measuredValueType &&
            IsSupportedMetricValueType(measuredValueType))
        {
            return (measuredValueType.ToDisplayString(), ObservableCallbackKind.Measurement);
        }

        if (TryGetMeasurementEnumerableValueType(method.ReturnType) is { } measuredElementType &&
            IsSupportedMetricValueType(measuredElementType))
        {
            return (measuredElementType.ToDisplayString(), ObservableCallbackKind.Measurements);
        }

        return IsSupportedMetricValueType(method.ReturnType)
            ? (method.ReturnType.ToDisplayString(), ObservableCallbackKind.Value)
            : (null, ObservableCallbackKind.None);
    }

    private static ITypeSymbol? TryGetMeasurementEnumerableValueType(ITypeSymbol returnType)
    {
        if (returnType is INamedTypeSymbol namedReturnType &&
            TryGetMeasurementValueTypeFromEnumerableType(namedReturnType) is { } directValueType)
            return directValueType;

        foreach (var interfaceType in returnType.AllInterfaces)
        {
            if (TryGetMeasurementValueTypeFromEnumerableType(interfaceType) is { } interfaceValueType)
                return interfaceValueType;
        }

        return null;
    }

    private static ITypeSymbol? TryGetMeasurementValueTypeFromEnumerableType(INamedTypeSymbol type)
    {
        if (!IsGenericEnumerable(type) ||
            type.TypeArguments.Length is not 1 ||
            TryGetMeasurementValueType(type.TypeArguments[0]) is not { } valueType)
            return null;

        return valueType;
    }

    private static bool IsGenericEnumerable(INamedTypeSymbol type)
    {
        if (type.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T)
            return true;

        var namespaceName = type.ContainingNamespace.ToDisplayString();
        return string.Equals(type.Name, "IEnumerable", StringComparison.Ordinal) &&
               string.Equals(namespaceName, "System.Collections.Generic", StringComparison.Ordinal);
    }

    private static ITypeSymbol? TryGetMeasurementValueType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType &&
            string.Equals(namedType.Name, "Measurement", StringComparison.Ordinal) &&
            IsMetricsMeasurementType(namedType) &&
            namedType.TypeArguments.Length is 1)
            return namedType.TypeArguments[0];

        return null;
    }

    private static bool IsMetricsMeasurementType(INamedTypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Error)
            return true;

        return IsMetricsMeasurementNamespace(type.ContainingNamespace);
    }

    private static bool IsMetricsMeasurementNamespace(INamespaceSymbol? namespaceSymbol)
    {
        var namespaceName = namespaceSymbol?.ToDisplayString() ?? string.Empty;
        return string.Equals(namespaceName, "System.Diagnostics.Metrics", StringComparison.Ordinal);
    }

    private static bool IsSupportedMetricValueType(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Byte or
                SpecialType.System_Int16 or
                SpecialType.System_Int32 or
                SpecialType.System_Int64 or
                SpecialType.System_Single or
                SpecialType.System_Double or
                SpecialType.System_Decimal => true,
            _ => false
        };
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

    private static EquatableArray<MetricTagParameter> ExtractTags(
        IMethodSymbol method,
        MetricAttributeTypes attributeTypes)
    {
        var tags = new List<MetricTagParameter>();

        foreach (var param in method.Parameters)
        {
            if (FindAttribute(param.GetAttributes(), attributeTypes.Tag) is not { } tagAttr)
                continue;

            string? explicitTagName = null;
            if (tagAttr.TryGetConstructorArgument<string>(0, out var tagNameValue))
                explicitTagName = tagNameValue;

            var tagName =
                TelemetryTagNameResolver.ResolveName(param, attributeTypes.Otel, explicitTagName, param.Name);

            tags.Add(new MetricTagParameter(
                EscapeIdentifier(param.Name),
                tagName,
                param.Type.ToDisplayString()));
        }

        return tags.ToEquatableArray();
    }

    private static bool IsObservable(MetricKind kind) =>
        kind is MetricKind.ObservableCounter or MetricKind.ObservableGauge or MetricKind.ObservableUpDownCounter;

    private readonly record struct MetricAttributeTypes(
        INamedTypeSymbol? Meter,
        INamedTypeSymbol? Counter,
        INamedTypeSymbol? Histogram,
        INamedTypeSymbol? Gauge,
        INamedTypeSymbol? UpDownCounter,
        INamedTypeSymbol? ObservableCounter,
        INamedTypeSymbol? ObservableGauge,
        INamedTypeSymbol? ObservableUpDownCounter,
        INamedTypeSymbol? Tag,
        INamedTypeSymbol? Otel)
    {
        public static MetricAttributeTypes Create(Compilation compilation) =>
            new(
                compilation.GetTypeByMetadataName(MeterAttributeMetadataName),
                compilation.GetTypeByMetadataName(CounterAttributeFullName),
                compilation.GetTypeByMetadataName(HistogramAttributeFullName),
                compilation.GetTypeByMetadataName(GaugeAttributeFullName),
                compilation.GetTypeByMetadataName(UpDownCounterAttributeFullName),
                compilation.GetTypeByMetadataName(ObservableCounterAttributeFullName),
                compilation.GetTypeByMetadataName(ObservableGaugeAttributeFullName),
                compilation.GetTypeByMetadataName(ObservableUpDownCounterAttributeFullName),
                compilation.GetTypeByMetadataName(TagAttributeFullName),
                TelemetryTagNameResolver.GetOtelAttributeType(compilation));
    }
}
