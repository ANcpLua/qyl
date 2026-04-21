using System.Text.Json.Serialization;

namespace Qyl.Contracts.Loom;

/// <summary>
///     Storage record for a fix run. Maps to the fix_runs DuckDB table.
/// </summary>
public sealed record FixRunRecord
{
    public required string RunId { get; init; }
    public required string IssueId { get; init; }
    public string? ExecutionId { get; init; }
    public required string Status { get; init; }
    public required string Policy { get; init; }
    public string? FixDescription { get; init; }
    public double? ConfidenceScore { get; init; }
    public string? ChangesJson { get; init; }

    /// <summary>
    ///     Optional user-provided hint injected into the RCA prompt.
    ///     Inspired by Sentry Seer's <c>instruction</c> parameter.
    /// </summary>
    public string? Instruction { get; init; }

    /// <summary>
    ///     Controls how far the pipeline runs: <c>root_cause</c>, <c>solution</c>,
    ///     <c>code_changes</c>, or <c>null</c> for the full pipeline.
    ///     Inspired by Sentry Seer's <c>stopping_point</c> parameter.
    /// </summary>
    public string? StoppingPoint { get; init; }

    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

/// <summary>
///     Storage record for an individual step within a fix run.
///     Maps to the autofix_steps DuckDB table.
/// </summary>
public sealed record AutofixStepRecord
{
    public required string StepId { get; init; }
    public required string RunId { get; init; }
    public required int StepNumber { get; init; }
    public required string StepName { get; init; }
    public required string Status { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CreatedAt { get; init; }
}

/// <summary>LLM confidence scoring result.</summary>
public sealed record ConfidenceResult
{
    [JsonPropertyName("confidence")] public double Confidence { get; init; }

    [JsonPropertyName("reasoning")] public string? Reasoning { get; init; }

    [JsonPropertyName("risks")] public string[]? Risks { get; init; }

    [JsonPropertyName("recommendation")] public string? Recommendation { get; init; }
}

[JsonSerializable(typeof(ConfidenceResult))]
public sealed partial class AutofixJsonContext : JsonSerializerContext;
