namespace Qyl.Collector.Storage;

internal static class PersistedAttributePolicy
{
    internal static string? SerializeSpanAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, AttributeKeySets.IsSafeSpanAttribute);

    internal static string? SerializeLogAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, AttributeKeySets.IsSafeLogAttribute);

    internal static string? SerializeProfileAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, AttributeKeySets.IsSafeProfileAttribute);

    internal static string? SerializeResourceAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, AttributeKeySets.IsSafeResourceAttribute);

    private static string? Serialize(
        IReadOnlyDictionary<string, string> attributes,
        Func<string, bool> shouldPersist)
    {
        Dictionary<string, string>? persisted = null;

        foreach (var (key, value) in attributes)
        {
            if (!shouldPersist(key))
                continue;

            persisted ??= new Dictionary<string, string>(StringComparer.Ordinal);
            persisted[key] = value;
        }

        return persisted is null
            ? null
            : JsonSerializer.Serialize(persisted, IngestionJsonSerializerContext.Default.DictionaryStringString);
    }
}
