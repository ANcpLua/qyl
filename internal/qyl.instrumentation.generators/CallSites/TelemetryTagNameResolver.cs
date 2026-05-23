using Microsoft.CodeAnalysis;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class TelemetryTagNameResolver
{
    private const string OTelAttributeMetadataName = "Qyl.Instrumentation.Instrumentation.OTelAttribute";

    public static string ResolveName(ISymbol symbol, Compilation compilation, string? explicitName, string fallbackName)
    {
        if (explicitName is { } resolvedExplicitName && !string.IsNullOrWhiteSpace(resolvedExplicitName))
            return resolvedExplicitName;

        var otel = ReadOtelOverride(symbol, compilation);
        return otel.Name is { } resolvedOtelName && !string.IsNullOrWhiteSpace(resolvedOtelName)
            ? resolvedOtelName
            : fallbackName;
    }

    public static bool ResolveSkipIfNull(ISymbol symbol, Compilation compilation, bool? explicitSkipIfNull,
        bool defaultValue = true)
    {
        if (explicitSkipIfNull.HasValue)
            return explicitSkipIfNull.Value;

        var otel = ReadOtelOverride(symbol, compilation);
        return otel.SkipIfNull ?? defaultValue;
    }

    private static (string? Name, bool? SkipIfNull) ReadOtelOverride(ISymbol symbol, Compilation compilation)
    {
        var attribute =
            IncrementalPipelineHelpers.FindAttributeByName(symbol.GetAttributes(), compilation,
                OTelAttributeMetadataName);
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
}
