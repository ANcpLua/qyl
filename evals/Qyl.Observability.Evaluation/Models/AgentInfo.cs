using System.Text.Json.Serialization;

namespace Qyl.Observability.Evaluation.Models;

public sealed record AgentInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("modelProvider")]
    public required string ModelProvider { get; init; }

    [JsonPropertyName("modelName")]
    public required string ModelName { get; init; }

    [JsonPropertyName("instructions")]
    public required string Instructions { get; init; }

    [JsonPropertyName("tools")]
    public IReadOnlyList<string> Tools { get; init; } = [];
}
