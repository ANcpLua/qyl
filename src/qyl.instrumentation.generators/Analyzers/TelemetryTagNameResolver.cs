using Microsoft.CodeAnalysis;

namespace Qyl.Instrumentation.Generators.Analyzers;

/// <summary>
///     Centralized name resolution for telemetry tags.
/// </summary>
/// <remarks>
///     <para>
///         The generator supports two orthogonal concerns:
///     </para>
///     <para>
///         1. Capture markers such as <c>[TracedTag]</c> and <c>[Tag]</c> decide whether a member
///         participates in emitted telemetry.
///     </para>
///     <para>
///         2. <c>[OTel]</c> provides the canonical OpenTelemetry semantic-convention key when the
///         capture marker does not specify an explicit name.
///     </para>
/// </remarks>
internal static class TelemetryTagNameResolver
{
    private const string OTelAttributeMetadataName = "Qyl.Instrumentation.Instrumentation.OTelAttribute";

    public static string ResolveName(ISymbol symbol, string? explicitName, string fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            var resolvedName = explicitName!;
            return resolvedName;
        }

        var otel = ReadOtelOverride(symbol);
        if (!string.IsNullOrWhiteSpace(otel.Name))
        {
            var resolvedName = otel.Name!;
            return resolvedName;
        }

        return fallbackName;
    }

    public static bool ResolveSkipIfNull(ISymbol symbol, bool? explicitSkipIfNull, bool defaultValue = true)
    {
        if (explicitSkipIfNull.HasValue)
            return explicitSkipIfNull.Value;

        var otel = ReadOtelOverride(symbol);
        return otel.SkipIfNull ?? defaultValue;
    }

    private static (string? Name, bool? SkipIfNull) ReadOtelOverride(ISymbol symbol)
    {
        var attribute = AnalyzerHelpers.FindAttributeByName(symbol.GetAttributes(), OTelAttributeMetadataName);
        if (attribute is null)
            return default;

        string? name = null;
        bool? skipIfNull = null;

        if (attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is string attributeName &&
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
