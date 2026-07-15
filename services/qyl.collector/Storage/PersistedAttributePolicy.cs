using System.Text;

namespace Qyl.Collector.Storage;

internal static class PersistedAttributePolicy
{
    internal static string? SerializeSpanAttributes(IReadOnlyDictionary<string, OtlpAttributeValue> attributes) =>
        Serialize(attributes, static key =>
            AttributeKeySets.IsSafeSpanAttribute(key) &&
            key != CollectorSemanticAttributeCatalog.GenAiOperationName &&
            key != CollectorSemanticAttributeCatalog.GenAiOutputType);

    internal static string? SerializeSpanAttributes(
        IReadOnlyDictionary<string, OtlpAttributeValue> attributes,
        SpanHotAttributeProjection projection) =>
        SerializeSpanAttributesCore(attributes, projection);

    internal static string? SerializeLogAttributes(IReadOnlyDictionary<string, OtlpAttributeValue> attributes) =>
        Serialize(attributes, AttributeKeySets.IsSafeLogAttribute);

    internal static string? SerializeProfileAttributes(IReadOnlyDictionary<string, OtlpAttributeValue> attributes) =>
        Serialize(attributes, AttributeKeySets.IsSafeProfileAttribute);

    internal static string? SerializeResourceAttributes(IReadOnlyDictionary<string, OtlpAttributeValue> attributes) =>
        Serialize(attributes, AttributeKeySets.IsSafeResourceAttribute);

    private static string? SerializeSpanAttributesCore(
        IReadOnlyDictionary<string, OtlpAttributeValue> attributes,
        SpanHotAttributeProjection projection)
    {
        var persisted = attributes
            .Where(static item => AttributeKeySets.IsSafeSpanAttribute(item.Key))
            .Where(item =>
                item.Key != CollectorSemanticAttributeCatalog.GenAiOperationName ||
                projection.GenAiOperationName is not null)
            .Where(item =>
                item.Key != CollectorSemanticAttributeCatalog.GenAiOutputType ||
                projection.GenAiOutputType is not null)
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .ToArray();

        if (persisted.Length is 0)
            return null;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in persisted)
            {
                writer.WritePropertyName(key);
                if (key == CollectorSemanticAttributeCatalog.GenAiOperationName)
                    writer.WriteStringValue(projection.GenAiOperationName);
                else if (key == CollectorSemanticAttributeCatalog.GenAiOutputType)
                    writer.WriteStringValue(projection.GenAiOutputType);
                else
                    value.WriteJsonValue(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string? Serialize(
        IReadOnlyDictionary<string, OtlpAttributeValue> attributes,
        Func<string, bool> shouldPersist)
    {
        var persisted = attributes
            .Where(item => shouldPersist(item.Key))
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .ToArray();

        if (persisted.Length is 0)
            return null;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in persisted)
            {
                writer.WritePropertyName(key);
                value.WriteJsonValue(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
