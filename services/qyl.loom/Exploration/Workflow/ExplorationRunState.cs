
using Microsoft.Agents.AI.Workflows;
using Qyl.Contracts.Copilot;

namespace Qyl.Loom.Exploration.Workflow;

public sealed record StartExplore(string IssueId, string? UserContext);

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

public sealed class ExplorationStreamEvent(StreamUpdate update) : WorkflowEvent(update)
{
    public StreamUpdate Update { get; } = update;
}
