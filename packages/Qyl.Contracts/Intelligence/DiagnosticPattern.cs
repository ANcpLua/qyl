using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

public sealed record DiagnosticPattern
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("category")]
    public required PatternCategory Category { get; init; }

    [JsonPropertyName("signals")]
    public required IReadOnlyList<Signal> Signals { get; init; }

    [JsonPropertyName("hypothesis")]
    public required string Hypothesis { get; init; }

    [JsonPropertyName("confidence")]
    public required double Confidence { get; init; }
}
