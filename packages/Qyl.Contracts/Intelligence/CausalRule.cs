using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

public sealed record CausalRule
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("cause_pattern")]
    public required string CausePattern { get; init; }

    [JsonPropertyName("effect_pattern")]
    public required string EffectPattern { get; init; }

    [JsonPropertyName("strength")]
    public required double Strength { get; init; }

    [JsonPropertyName("temporal_window")]
    public string? TemporalWindow { get; init; }
}
