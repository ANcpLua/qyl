namespace Qyl.Contracts.Intelligence;

using System.Text.Json.Serialization;

/// <summary>
///     Atomic telemetry observation — a single attribute condition.
///     Signals reference semconv attributes and promoted DuckDB columns only.
/// </summary>
public sealed record Signal
{
    /// <summary>Telemetry attribute name (semconv or promoted column)</summary>
    [JsonPropertyName("attribute")]
    public required string Attribute { get; init; }

    /// <summary>Comparison operator</summary>
    [JsonPropertyName("operator")]
    public required SignalOperator Operator { get; init; }

    /// <summary>Expected value (type-coerced at evaluation time). Null for exists/not_exists.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }
}
