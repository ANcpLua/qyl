using ANcpLua.Roslyn.Utilities;
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

    public static string ResolveName(ISymbol symbol, Compilation compilation, string? explicitName, string fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(explicitName))
            return explicitName!;

        var otel = ReadOtelOverride(symbol, compilation);
        return !string.IsNullOrWhiteSpace(otel.Name) ? otel.Name! : fallbackName;
    }

    public static bool ResolveSkipIfNull(ISymbol symbol, Compilation compilation, bool? explicitSkipIfNull, bool defaultValue = true)
    {
        if (explicitSkipIfNull.HasValue)
            return explicitSkipIfNull.Value;

        var otel = ReadOtelOverride(symbol, compilation);
        return otel.SkipIfNull ?? defaultValue;
    }

    private static (string? Name, bool? SkipIfNull) ReadOtelOverride(ISymbol symbol, Compilation compilation)
    {
        var attribute = AnalyzerHelpers.FindAttributeByName(symbol.GetAttributes(), compilation, OTelAttributeMetadataName);
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
