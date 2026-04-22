// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI.Workflows;
using Qyl.Contracts.Copilot;

namespace Qyl.Loom.Exploration.Workflow;

/// <summary>Entry message for the exploration workflow.</summary>
public sealed record StartExplore(string IssueId, string? UserContext);

/// <summary>
///     Immutable shared state threaded through every exploration executor. Each executor returns a new
///     instance via <c>with</c>-expressions. Two orthogonal terminal flags match the autofix workflow:
///     <see cref="IsError" /> short-circuits to the finalize executor and surfaces an error update,
///     <see cref="IsInterrupted" /> matches the prior <c>Exploration interrupted.</c> path.
/// </summary>
public sealed record ExplorationRunState
{
    public required string IssueId { get; init; }
    public string? UserContext { get; init; }

    public string? SessionId { get; init; }
    public ExplorationContext? Context { get; init; }
    public ExplorationRootCause? RootCause { get; init; }
    public ExplorationSolution? Solution { get; init; }

    public bool IsError { get; init; }
    public string? ErrorMessage { get; init; }

    public bool IsInterrupted { get; init; }
}

/// <summary>
///     Workflow event carrying a <see cref="StreamUpdate" /> produced by an exploration executor. The
///     orchestrator observes these via <c>WatchStreamAsync</c> and republishes them to the SSE endpoint,
///     preserving the public <c>IAsyncEnumerable&lt;StreamUpdate&gt;</c> contract.
/// </summary>
public sealed class ExplorationStreamEvent(StreamUpdate update) : WorkflowEvent(update)
{
    public StreamUpdate Update { get; } = update;
}
