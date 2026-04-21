using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

/// <summary>
///     Named combination of signals identifying a known failure mode.
///     All signals must match (conjunction). Multiple patterns can match
///     the same telemetry — the engine returns all matches ranked by confidence.
/// </summary>
public sealed record DiagnosticPattern
{
    /// <summary>Unique pattern identifier (e.g. genai_rate_limit)</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Pattern classification</summary>
    [JsonPropertyName("category")]
    public required PatternCategory Category { get; init; }

    /// <summary>Signals that must all match (conjunction)</summary>
    [JsonPropertyName("signals")]
    public required IReadOnlyList<Signal> Signals { get; init; }

    /// <summary>What this pattern means diagnostically</summary>
    [JsonPropertyName("hypothesis")]
    public required string Hypothesis { get; init; }

    /// <summary>Base confidence weight (0.0-1.0)</summary>
    [JsonPropertyName("confidence")]
    public required double Confidence { get; init; }
}
