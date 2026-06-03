namespace Qyl.Collector.Ingestion;

internal static class PersistedAttributePolicy
{
    internal static string? SerializeSpanAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, AttributeKeySets.ShouldPersistSpanAttribute);

    internal static string? SerializeLogAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, AttributeKeySets.ShouldPersistLogAttribute);

    internal static string? SerializeProfileAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, AttributeKeySets.ShouldPersistProfileAttribute);

    internal static string? SerializeResourceAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, AttributeKeySets.ShouldPersistResourceAttribute);

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
