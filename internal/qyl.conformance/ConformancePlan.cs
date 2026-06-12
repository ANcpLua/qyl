using System.Text.Json.Serialization;

namespace Qyl.Conformance;

/// <summary>
/// Wire model of <c>conformance-plan.json</c> as emitted by @qyl/telemetry-control-graph —
/// the condensed checklist of what each service must provably emit and export.
/// </summary>
public sealed record ConformancePlan
{
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("graph_schema_version")]
    public required string GraphSchemaVersion { get; init; }

    [JsonPropertyName("services")]
    public required IReadOnlyList<PlannedService> Services { get; init; }
}

public sealed record PlannedService
{
    [JsonPropertyName("service_name")]
    public required string ServiceName { get; init; }

    [JsonPropertyName("profile_id")]
    public string? ProfileId { get; init; }

    [JsonPropertyName("expected_signals")]
    public required IReadOnlyList<ExpectedSignal> ExpectedSignals { get; init; }
}

public sealed record ExpectedSignal
{
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("required_attributes")]
    public IReadOnlyList<string> RequiredAttributes { get; init; } = [];

    [JsonPropertyName("recommended_attributes")]
    public IReadOnlyList<string> RecommendedAttributes { get; init; } = [];

    [JsonPropertyName("opt_in_attributes")]
    public IReadOnlyList<string> OptInAttributes { get; init; } = [];
}
