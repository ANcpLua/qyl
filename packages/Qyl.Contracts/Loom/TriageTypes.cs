using System.Text.Json.Serialization;

namespace Qyl.Contracts.Loom;

public sealed record TriageResult
{
    public required string TriageId { get; init; }
    public required string IssueId { get; init; }
    public required double FixabilityScore { get; init; }
    public required string AutomationLevel { get; init; }
    public string? AiSummary { get; init; }
    public string? RootCauseHypothesis { get; init; }
    public required string TriggeredBy { get; init; }
    public string? FixRunId { get; init; }
    public required string ScoringMethod { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed record LlmTriageResponse
{
    [JsonPropertyName("fixability_score")] public double FixabilityScore { get; init; }

    [JsonPropertyName("automation_level")] public string? AutomationLevel { get; init; }

    [JsonPropertyName("root_cause_hypothesis")]
    public string? RootCauseHypothesis { get; init; }

    [JsonPropertyName("summary")] public string? Summary { get; init; }
}

[JsonSerializable(typeof(LlmTriageResponse))]
public sealed partial class TriageJsonContext : JsonSerializerContext;
