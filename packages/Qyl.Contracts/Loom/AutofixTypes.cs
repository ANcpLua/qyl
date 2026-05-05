using System.Text.Json.Serialization;

namespace Qyl.Contracts.Loom;

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

    public string? Instruction { get; init; }

    public string? StoppingPoint { get; init; }

    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

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

public sealed record ConfidenceResult
{
    [JsonPropertyName("confidence")] public double Confidence { get; init; }

    [JsonPropertyName("reasoning")] public string? Reasoning { get; init; }

    [JsonPropertyName("risks")] public string[]? Risks { get; init; }

    [JsonPropertyName("recommendation")] public string? Recommendation { get; init; }
}

public sealed record ImpactAssessmentResult
{
    [JsonPropertyName("one_line_description")]
    public string? OneLineDescription { get; init; }

    [JsonPropertyName("impacts")] public ImpactEntry[]? Impacts { get; init; }
}

public sealed record ImpactEntry
{
    [JsonPropertyName("label")] public string? Label { get; init; }
    [JsonPropertyName("rating")] public string? Rating { get; init; }

    [JsonPropertyName("impact_description")]
    public string? ImpactDescription { get; init; }

    [JsonPropertyName("evidence")] public string? Evidence { get; init; }
}

public sealed record AutofixTriageInfo
{
    [JsonPropertyName("suspect_commit")] public SuspectCommit? SuspectCommit { get; init; }

    [JsonPropertyName("suggested_assignee")]
    public SuggestedAssignee? SuggestedAssignee { get; init; }
}

public sealed record SuspectCommit
{
    [JsonPropertyName("sha")] public string? Sha { get; init; }
    [JsonPropertyName("repo_name")] public string? RepoName { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("author_name")] public string? AuthorName { get; init; }
    [JsonPropertyName("author_email")] public string? AuthorEmail { get; init; }
    [JsonPropertyName("committed_date")] public string? CommittedDate { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
}

public sealed record SuggestedAssignee
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("why")] public string? Why { get; init; }
}

[JsonSerializable(typeof(ConfidenceResult))]
[JsonSerializable(typeof(ImpactAssessmentResult))]
[JsonSerializable(typeof(AutofixTriageInfo))]
public sealed partial class AutofixJsonContext : JsonSerializerContext;
