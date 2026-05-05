
using System.ComponentModel;

namespace Qyl.Loom.Autofix.Workflow;

public sealed partial record AutofixWorkflowConfig(
    int HypothesisFanOut,
    double HypothesisTemperatureSpread,
    string? HypothesisAlternateModel,
    int ConfidenceRetryThreshold,
    int MaxConfidenceRetries,
    bool StoppingPointAfterHypothesis,
    bool StoppingPointBeforeCommit,
    bool ToolUsingContext,
    int ContextToolBudget,
    TimeSpan? StoppingPointTimeout);

public sealed partial record AutofixWorkflowRequest(
    string RunId,
    string IssueId,
    string Policy,
    string? CallerInstruction,
    string? StoppingPoint,
    AutofixWorkflowConfig Config);

public sealed partial record FixabilityVerdict(
    string RunId,
    [property: Description("0..5")] int Score,
    [property: Description("'continue' | 'need_more_signal'")] string Decision,
    string? MissingSignal);

public sealed partial record ContextSummary(
    string RunId,
    string Summary,
    IReadOnlyList<string> SignalsFound,
    IReadOnlyList<string> SignalsAbsent);

public sealed partial record HypothesisCandidate(
    string RunId,
    string BranchId,
    string Primary,
    string? Alternative,
    double SelfReportedConfidence);

public sealed partial record HypothesisVerdict(
    string RunId,
    string Primary,
    string? Alternative,
    string JudgeRationale,
    int RetryIteration);

public sealed partial record SolutionDraft(
    string RunId,
    string? Repo,
    string? Diff,
    string? RegressionTest);

public sealed partial record ConfidenceAudit(
    string RunId,
    string Level,
    int ScoreSum,
    int EvidenceGate,
    int RegressionGate,
    int CompletenessGate,
    int SelfChallengeGate,
    bool RetryRequested);

public sealed partial record AutofixWorkflowResult(
    string RunId,
    AutofixReport Report);
