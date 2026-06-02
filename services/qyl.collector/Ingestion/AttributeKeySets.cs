using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;
using McpAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Mcp.McpAttributes;
using SessionAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Session.SessionAttributes;

namespace Qyl.Collector.Ingestion;

internal static class AttributeKeySets
{
    internal static readonly string[] SessionCorrelation =
    [
        GenAiAttributes.ConversationId,
        McpAttributes.SessionId,
        SessionAttributes.Id
    ];

    internal static readonly string QylCapabilityPrefix = AttributeKeyPrefix.Of(QylAttr.Capability.Id);
}

internal static class AttributeLookupExtensions
{
    internal static string? GetFirstValueOrDefault(
        this IReadOnlyDictionary<string, string> attributes,
        IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            if (attributes.GetValueOrDefault(key) is { } value)
                return value;
        }

        return null;
    }

    internal static bool IsAny(
        this string key,
        IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (key.EqualsOrdinal(candidate))
                return true;
        }

        return false;
    }
}

internal static class AttributeKeyPrefix
{
    internal static string Of(string key)
    {
        var lastDot = key.LastIndexOf('.');
        return lastDot < 0 ? key : key[..(lastDot + 1)];
    }
}
