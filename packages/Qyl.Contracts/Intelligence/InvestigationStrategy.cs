using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

public sealed record InvestigationStrategy
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("trigger_pattern")]
    public required string TriggerPattern { get; init; }

    [JsonPropertyName("steps")]
    public required IReadOnlyList<InvestigationStep> Steps { get; init; }
}
