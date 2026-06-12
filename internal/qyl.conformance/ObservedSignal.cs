using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Conformance;

/// <summary>
/// One observed telemetry occurrence, normalized to the conformance surface:
/// who emitted, what kind, which name, which attribute keys were present.
/// Snapshots are JSON Lines of this shape — produced from collector storage
/// (or any OTLP tap) by the snapshot adapter of the calling service.
/// </summary>
public sealed record ObservedSignal
{
    [JsonPropertyName("service_name")]
    public required string ServiceName { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("attribute_keys")]
    public IReadOnlyList<string> AttributeKeys { get; init; } = [];

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Parses a JSON-Lines snapshot; blank lines are skipped.</summary>
    public static IReadOnlyList<ObservedSignal> FromJsonLines(IEnumerable<string> lines) =>
        lines.Where(static l => !string.IsNullOrWhiteSpace(l))
            .Select(static l => JsonSerializer.Deserialize<ObservedSignal>(l, Options)
                         ?? throw new JsonException("Snapshot line deserialized to null."))
            .ToList();
}
