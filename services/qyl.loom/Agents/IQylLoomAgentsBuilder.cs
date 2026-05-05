
using Qyl.Loom.Autofix.Workflow;

namespace Qyl.Loom.Agents;

public interface IQylLoomAgentsBuilder
{
    bool IsConfigured { get; }

    AIAgent BuildFixabilityStageAgent();

    AIAgent BuildContextStageAgent(AutofixWorkflowConfig config);

    AIAgent BuildHypothesisBranchAgent(string perspective);

    AIAgent BuildHypothesisJudgeAgent();

    AIAgent BuildSolutionStageAgent();

    AIAgent BuildConfidenceStageAgent();

    AIAgent BuildReportStageAgent();

    AIAgent BuildTriageScoringAgent();

    AIAgent BuildCodeReviewAgent();

    AIAgent BuildExplorationInsightAgent();

    AIAgent BuildExplorationStrategistAgent();
}
