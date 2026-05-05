
namespace Qyl.Loom.Patterns.Contracts;

public sealed record RootCauseHypothesis(
    string SignalId,
    string Hypothesis,
    double Confidence);

public sealed record SolutionPlan(
    string SignalId,
    string Approach,
    IReadOnlyList<string> Steps);

public sealed record ConfidenceVerdict(
    string SignalId,
    bool Approved,
    string Reason);

public sealed record IncidentSignal(
    string Id,
    string Service,
    string Severity,
    string Description);

public sealed record AutofixCombinedState(int SignalsSeen);
