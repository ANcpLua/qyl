using Qyl.Contracts.Copilot;

namespace Qyl.Loom.Exploration;

public sealed record ExplorationInsight
{
    public required string IssueId { get; init; }
    public required string WhatHappened { get; init; }
    public required string InitialGuess { get; init; }
    public string? InTheTrace { get; init; }
}

public sealed record ExplorationRootCause
{
    [JsonPropertyName("summary")] public required string Summary { get; init; }
    [JsonPropertyName("steps")] public required ExplorationCausalStep[] Steps { get; init; }
}

public sealed record ExplorationCausalStep(
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("is_root_cause")] bool IsRootCause);

public sealed record ExplorationSolution
{
    [JsonPropertyName("summary")] public required string Summary { get; init; }
    [JsonPropertyName("steps")] public required ExplorationSolutionStep[] Steps { get; init; }
}

public sealed record ExplorationSolutionStep(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description);

public sealed record ExplorationExploreRequest(
    [property: JsonPropertyName("user_context")] string? UserContext);

public sealed record ExplorationCodeItUpRequest(
    [property: JsonPropertyName("repo")] string? Repo,
    [property: JsonPropertyName("base_branch")] string? BaseBranch);

public sealed record ExplorationCodeItUpResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("pr_url")] string? PrUrl,
    [property: JsonPropertyName("error")] string? Error);

public sealed record ExplorationDiagnosisResult(
    string Monologue,
    ExplorationRootCause? RootCause,
    IReadOnlyList<StreamUpdate> Updates,
    bool IsInterrupted)
{
    public static ExplorationDiagnosisResult Unconfigured { get; } = new(string.Empty, null, [], false);
}

internal sealed record InsightLlmResponse
{
    [JsonPropertyName("what_happened")] public string? WhatHappened { get; init; }
    [JsonPropertyName("initial_guess")] public string? InitialGuess { get; init; }
    [JsonPropertyName("in_the_trace")] public string? InTheTrace { get; init; }
}

[JsonSerializable(typeof(InsightLlmResponse))]
[JsonSerializable(typeof(ExplorationInsight))]
[JsonSerializable(typeof(ExplorationRootCause))]
[JsonSerializable(typeof(ExplorationSolution))]
[JsonSerializable(typeof(ExplorationExploreRequest))]
[JsonSerializable(typeof(ExplorationCodeItUpResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StreamUpdate))]
internal partial class ExplorationJsonContext : JsonSerializerContext;
