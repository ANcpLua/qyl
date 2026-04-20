namespace Qyl.Contracts.Intelligence;

using System.Text.Json.Serialization;

/// <summary>
///     Deterministic investigation sequence triggered by a matched pattern.
///     The LLM does not invent investigation paths — it selects from known
///     strategies and interprets results.
/// </summary>
public sealed record InvestigationStrategy
{
    /// <summary>Unique strategy identifier</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Trigger — pattern ID or category:X for category-wide triggers</summary>
    [JsonPropertyName("trigger_pattern")]
    public required string TriggerPattern { get; init; }

    /// <summary>Ordered investigation steps</summary>
    [JsonPropertyName("steps")]
    public required IReadOnlyList<InvestigationStep> Steps { get; init; }
}
