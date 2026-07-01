using System.Text.Json;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Storage;

/// <summary>Persisted JSON shape for a span event: name, timestamp, and its (safe) attributes JSON.</summary>
internal sealed class SpanEventJson
{
    public string Name { get; set; } = "";
    public ulong TimeUnixNano { get; set; }
    public string? AttributesJson { get; set; }
}

/// <summary>Persisted JSON shape for a span link: linked trace/span ids and its (safe) attributes JSON.</summary>
internal sealed class SpanLinkJson
{
    public string TraceId { get; set; } = "";
    public string SpanId { get; set; } = "";
    public string? AttributesJson { get; set; }
}

/// <summary>
/// Serializes a span's events and links to JSON for the spans row. Previously OTLP span Events (including
/// OTel exception events) and Links were dropped on ingest (CODE RED #4); they are now persisted so the
/// span-detail API can return them. Each child's attributes reuse the same safe-attribute JSON as the span.
/// </summary>
internal static class SpanChildStorage
{
    public static string? SerializeEvents(IReadOnlyList<SpanEventIngest> events)
    {
        if (events.Count == 0)
            return null;

        var list = new List<SpanEventJson>(events.Count);
        foreach (var e in events)
        {
            list.Add(new SpanEventJson
            {
                Name = e.Name,
                TimeUnixNano = e.TimeUnixNano,
                AttributesJson = PersistedAttributePolicy.SerializeSpanAttributes(e.Attributes)
            });
        }

        return JsonSerializer.Serialize(list, StorageJsonSerializerContext.Default.SpanEventJsonList);
    }

    public static string? SerializeLinks(IReadOnlyList<SpanLinkIngest> links)
    {
        if (links.Count == 0)
            return null;

        var list = new List<SpanLinkJson>(links.Count);
        foreach (var l in links)
        {
            list.Add(new SpanLinkJson
            {
                TraceId = l.TraceId,
                SpanId = l.SpanId,
                AttributesJson = PersistedAttributePolicy.SerializeSpanAttributes(l.Attributes)
            });
        }

        return JsonSerializer.Serialize(list, StorageJsonSerializerContext.Default.SpanLinkJsonList);
    }

    public static List<SpanEventJson>? DeserializeEvents(string? json) =>
        string.IsNullOrEmpty(json)
            ? null
            : JsonSerializer.Deserialize(json, StorageJsonSerializerContext.Default.SpanEventJsonList);

    public static List<SpanLinkJson>? DeserializeLinks(string? json) =>
        string.IsNullOrEmpty(json)
            ? null
            : JsonSerializer.Deserialize(json, StorageJsonSerializerContext.Default.SpanLinkJsonList);
}
