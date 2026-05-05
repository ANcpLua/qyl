using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

public sealed record InvestigationStep
{
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
}
