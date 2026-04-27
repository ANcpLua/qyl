// Copyright (c) 2025-2026 ancplua

using Qyl.Loom.Autofix.Workflow;

namespace Qyl.Loom.Agents;

public interface IQylLoomAgentsBuilder
{
    bool IsConfigured { get; }

    AIAgent BuildTriageScoringAgent();
    AIAgent BuildCodeReviewAgent();
    AIAgent BuildExplorationInsightAgent();
    AIAgent BuildExplorationStrategistAgent();

    // Autofix workflow stage agents — replace the deleted single-agent BuildAutofixAgent.
    AIAgent BuildFixabilityStageAgent();
    AIAgent BuildContextStageAgent(AutofixWorkflowConfig config);
    AIAgent BuildHypothesisStageAgent();
    AIAgent BuildSolutionStageAgent();
    AIAgent BuildConfidenceStageAgent();
    AIAgent BuildReportStageAgent();
}
