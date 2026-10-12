using System.Text.Json.Nodes;

namespace Qyl.Collector.Analysis;

internal static class SemanticMetadata
{
    private static readonly JsonObject Metadata = Load();
    private static readonly JsonObject Attributes = Metadata["attributes"]!.AsObject();
    public static JsonArray SpanRequirements => Metadata["span_requirements"]!.AsArray();
    private static JsonObject Load()
    {
        using var stream = typeof(SemanticMetadata).Assembly.GetManifestResourceStream("Qyl.Collector.Analysis.Generated.semconv-metadata.json")
            ?? throw new InvalidOperationException("Generated semantic metadata is missing.");
        return JsonNode.Parse(stream)!.AsObject();
    }
    public static JsonNode? Find(string key) => Attributes[key]?.DeepClone();
}
