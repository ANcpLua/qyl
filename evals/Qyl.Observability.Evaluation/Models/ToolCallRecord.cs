using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Observability.Evaluation.Models;

public sealed record ToolCallRecord
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("arguments")]
    public IReadOnlyDictionary<string, JsonElement> Arguments { get; init; } = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    [JsonPropertyName("resultSummary")]
    public string? ResultSummary { get; init; }
}
