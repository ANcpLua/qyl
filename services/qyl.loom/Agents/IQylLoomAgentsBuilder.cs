// Copyright (c) 2025-2026 ancplua

using Qyl.Loom.Autofix.Workflow;

namespace Qyl.Loom.Agents;

/// <summary>
///     Factory contract that constructs per-stage <see cref="AIAgent" /> instances for the
///     Loom autofix pipeline. Implementations must wrap every returned agent with
///     <c>UseQylAgentTelemetry()</c> at the composition root.
/// </summary>
public interface IQylLoomAgentsBuilder
{
    /// <summary>Gets a value indicating whether the builder has been configured with an LLM provider.</summary>
    bool IsConfigured { get; }

    /// <summary>Builds the agent used to score fixability of an error issue.</summary>
    AIAgent BuildFixabilityStageAgent();

    /// <summary>Builds the context-gathering agent, optionally wired with tool-using mode from <paramref name="config"/>.</summary>
    /// <param name="config">Workflow configuration controlling <c>ToolUsingContext</c> and <c>ContextToolBudget</c>.</param>
    AIAgent BuildContextStageAgent(AutofixWorkflowConfig config);

    /// <summary>Builds the hypothesis generation agent (multi-perspective single call).</summary>
    AIAgent BuildHypothesisStageAgent();

    /// <summary>Builds the solution drafting agent.</summary>
    AIAgent BuildSolutionStageAgent();

    /// <summary>Builds the confidence auditing agent.</summary>
    AIAgent BuildConfidenceStageAgent();

    /// <summary>Builds the final report assembly agent.</summary>
    AIAgent BuildReportStageAgent();

    /// <summary>Builds the triage scoring agent.</summary>
    AIAgent BuildTriageScoringAgent();

    /// <summary>Builds the code review agent.</summary>
    AIAgent BuildCodeReviewAgent();

    /// <summary>Builds the exploration insight agent.</summary>
    AIAgent BuildExplorationInsightAgent();

    /// <summary>Builds the exploration strategist agent.</summary>
    AIAgent BuildExplorationStrategistAgent();
}
