using Microsoft.CodeAnalysis;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class TelemetryTagNameResolver
{
    public const string OTelAttributeMetadataName = "Qyl.Instrumentation.Instrumentation.OTelAttribute";

    public static INamedTypeSymbol? GetOtelAttributeType(Compilation compilation) =>
        compilation.GetTypeByMetadataName(OTelAttributeMetadataName);

    public static string ResolveName(
        ISymbol symbol,
        INamedTypeSymbol? otelAttributeType,
        string? explicitName,
        string fallbackName)
    {
        if (explicitName is { } resolvedExplicitName && !string.IsNullOrWhiteSpace(resolvedExplicitName))
            return resolvedExplicitName;

        var otel = ReadOtelOverride(symbol, otelAttributeType);
        return otel.Name is { } resolvedOtelName && !string.IsNullOrWhiteSpace(resolvedOtelName)
            ? resolvedOtelName
            : fallbackName;
    }

    public static string ResolveName(
        ISymbol symbol,
        string otelAttributeMetadataName,
        string? explicitName,
        string fallbackName)
    {
        if (explicitName is { } resolvedExplicitName && !string.IsNullOrWhiteSpace(resolvedExplicitName))
            return resolvedExplicitName;

        var otel = ReadOtelOverride(symbol, otelAttributeMetadataName);
        return otel.Name is { } resolvedOtelName && !string.IsNullOrWhiteSpace(resolvedOtelName)
            ? resolvedOtelName
            : fallbackName;
    }

    public static bool ResolveSkipIfNull(
        ISymbol symbol,
        INamedTypeSymbol? otelAttributeType,
        bool? explicitSkipIfNull,
        bool defaultValue = true)
    {
        if (explicitSkipIfNull.HasValue)
            return explicitSkipIfNull.Value;

        var otel = ReadOtelOverride(symbol, otelAttributeType);
        return otel.SkipIfNull ?? defaultValue;
    }

    private static (string? Name, bool? SkipIfNull) ReadOtelOverride(
        ISymbol symbol,
        INamedTypeSymbol? otelAttributeType)
    {
        if (otelAttributeType is null)
            return default;

        AttributeData? attribute = null;
        foreach (var candidate in symbol.GetAttributes())
        {
            if (!candidate.AttributeClass.IsEqualTo(otelAttributeType))
                continue;

            attribute = candidate;
            break;
        }

        if (attribute is null)
            return default;

        string? name = null;
        bool? skipIfNull = null;

        if (attribute.TryGetConstructorArgument<string>(0, out var attributeName) &&
            !string.IsNullOrWhiteSpace(attributeName))
        {
            name = attributeName;
        }

        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg is { Key: "SkipIfNull", Value.Value: bool skipValue })
            {
                skipIfNull = skipValue;
                break;
            }
        }

        return (name, skipIfNull);
    }

    private static (string? Name, bool? SkipIfNull) ReadOtelOverride(
        ISymbol symbol,
        string otelAttributeMetadataName)
    {
        AttributeData? attribute = null;
        foreach (var candidate in symbol.GetAttributes())
        {
            if (!HasAttributeMetadataName(candidate, otelAttributeMetadataName))
                continue;

            attribute = candidate;
            break;
        }

        if (attribute is null)
            return default;

        string? name = null;
        bool? skipIfNull = null;

        if (attribute.TryGetConstructorArgument<string>(0, out var attributeName) &&
            !string.IsNullOrWhiteSpace(attributeName))
        {
            name = attributeName;
        }

        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg is { Key: "SkipIfNull", Value.Value: bool skipValue })
            {
                skipIfNull = skipValue;
                break;
            }
        }

        return (name, skipIfNull);
    }

    internal static bool HasAttributeMetadataName(AttributeData attribute, string metadataName) =>
        attribute.AttributeClass is { } attributeClass &&
        HasMetadataName(attributeClass, metadataName);

    private static bool HasMetadataName(INamedTypeSymbol symbol, string metadataName)
    {
        var namespaceName = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();
        var candidate = namespaceName.Length is 0
            ? symbol.MetadataName
            : string.Concat(namespaceName, ".", symbol.MetadataName);
        return string.Equals(candidate, metadataName, StringComparison.Ordinal);
    }
}
