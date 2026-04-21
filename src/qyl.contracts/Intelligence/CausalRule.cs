using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

/// <summary>
///     Directed causal relationship between two diagnostic patterns.
///     If cause is observed, effect is likely. Causal rules build a directed
///     graph — root causes are patterns with no incoming causal edges.
/// </summary>
public sealed record CausalRule
{
    /// <summary>Unique rule identifier</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>ID of the cause DiagnosticPattern</summary>
    [JsonPropertyName("cause_pattern")]
    public required string CausePattern { get; init; }

    /// <summary>ID of the effect DiagnosticPattern</summary>
    [JsonPropertyName("effect_pattern")]
    public required string EffectPattern { get; init; }

    /// <summary>Causal confidence (0.0-1.0)</summary>
    [JsonPropertyName("strength")]
    public required double Strength { get; init; }

    /// <summary>Time window for correlation (e.g. 5m, 1h)</summary>
    [JsonPropertyName("temporal_window")]
    public string? TemporalWindow { get; init; }
}
