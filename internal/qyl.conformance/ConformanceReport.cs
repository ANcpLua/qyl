using System.Text.Json.Serialization;

namespace Qyl.Conformance;

[JsonConverter(typeof(JsonStringEnumConverter<ConformanceFindingKind>))]
public enum ConformanceFindingKind
{
    [JsonStringEnumMemberName("undeclared_emitted")]
    UndeclaredEmitted,

    [JsonStringEnumMemberName("declared_missing")]
    DeclaredMissing,

    [JsonStringEnumMemberName("attribute_drift")]
    AttributeDrift,
}

[JsonConverter(typeof(JsonStringEnumConverter<ConformanceSeverity>))]
public enum ConformanceSeverity
{
    [JsonStringEnumMemberName("error")]
    Error,

    [JsonStringEnumMemberName("warning")]
    Warning,
}

/// <summary>A single declared-vs-observed difference.</summary>
public sealed record ConformanceFinding
{
    [JsonPropertyName("kind")]
    public required ConformanceFindingKind Kind { get; init; }

    [JsonPropertyName("severity")]
    public required ConformanceSeverity Severity { get; init; }

    [JsonPropertyName("service_name")]
    public required string ServiceName { get; init; }

    [JsonPropertyName("signal_kind")]
    public string? SignalKind { get; init; }

    [JsonPropertyName("signal_name")]
    public string? SignalName { get; init; }

    [JsonPropertyName("attribute_key")]
    public string? AttributeKey { get; init; }

    [JsonPropertyName("detail")]
    public required string Detail { get; init; }
}

/// <summary>
/// Result of one verifier run. <see cref="Conformant"/> (no error findings) is the
/// gate for exporter-config generation — config is generated from the graph only
/// after this is true.
/// </summary>
public sealed record ConformanceReport
{
    [JsonPropertyName("graph_schema_version")]
    public required string GraphSchemaVersion { get; init; }

    [JsonPropertyName("findings")]
    public required IReadOnlyList<ConformanceFinding> Findings { get; init; }

    [JsonPropertyName("conformant")]
    public required bool Conformant { get; init; }
}
