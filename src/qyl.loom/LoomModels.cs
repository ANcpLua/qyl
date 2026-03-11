namespace Qyl.Loom;

// =============================================================================
// Pre-Investigation (Stage 1)
// =============================================================================

/// <summary>
///     Pre-investigation insight shown in the Loom sidebar before the user
///     starts the interactive explorer. Generated from DuckDB data + optional LLM.
/// </summary>
public sealed record LoomInsight
{
    public required string IssueId { get; init; }
    public required string WhatHappened { get; init; }
    public required string InitialGuess { get; init; }
    public string? InTheTrace { get; init; }
    public IReadOnlyList<LoomResource> Resources { get; init; } = [];
}

public sealed record LoomResource(string Title, string? Url, string Description);

// =============================================================================
// Root Cause Synthesis (Stage 4)
// =============================================================================

/// <summary>
///     Structured root cause breakdown — chronological causal chain
///     rendered as an expandable step list in the Loom panel.
/// </summary>
public sealed record LoomRootCause
{
    public required string Summary { get; init; }
    public required IReadOnlyList<LoomCausalStep> Steps { get; init; }
}

public sealed record LoomCausalStep(int Order, string Description, bool IsRootCause);

// =============================================================================
// Resolution Planning (Stage 5)
// =============================================================================

/// <summary>
///     Structured solution plan with individual implementation steps.
///     Each step can be expanded/removed in the UI before "Code It Up".
/// </summary>
public sealed record LoomSolution
{
    public required string Summary { get; init; }
    public required IReadOnlyList<LoomSolutionStep> Steps { get; init; }
}

public sealed record LoomSolutionStep(string Title, string Description);

// =============================================================================
// API request/response
// =============================================================================

public sealed record LoomExploreRequest(string? UserContext);

public sealed record LoomCodeItUpRequest(
    string Repo,
    string? BaseBranch);

public sealed record LoomCodeItUpResponse(
    bool Success,
    string? RunId,
    string? PrUrl,
    string? Error);

// =============================================================================
// JSON source generation
// =============================================================================

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoomInsight))]
[JsonSerializable(typeof(LoomRootCause))]
[JsonSerializable(typeof(LoomSolution))]
[JsonSerializable(typeof(LoomExploreRequest))]
[JsonSerializable(typeof(LoomCodeItUpRequest))]
[JsonSerializable(typeof(LoomCodeItUpResponse))]
[JsonSerializable(typeof(LoomResource[]))]
[JsonSerializable(typeof(LoomCausalStep[]))]
[JsonSerializable(typeof(LoomSolutionStep[]))]
public partial class LoomJsonContext : JsonSerializerContext;
