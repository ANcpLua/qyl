using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

public sealed record Signal
{
    [JsonPropertyName("attribute")]
    public required string Attribute { get; init; }

    [JsonPropertyName("operator")]
    public required SignalOperator Operator { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}
