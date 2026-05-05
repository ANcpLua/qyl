using Qyl.Contracts.Intelligence;

namespace Qyl.Collector.Intelligence;

public interface IPatternEngine
{
    IReadOnlyList<PatternMatch> Evaluate(IReadOnlyList<Signal> observedSignals);

    CausalGraph BuildCausalGraph(IReadOnlyList<PatternMatch> matches);

    InvestigationStrategy? SelectStrategy(PatternMatch primaryMatch);
}

public sealed record PatternMatch(
    DiagnosticPattern Pattern,
    double Score,
    IReadOnlyList<Signal> MatchedSignals);

public sealed record CausalGraph(
    IReadOnlyList<CausalEdge> Edges,
    IReadOnlyList<string> RootCauses);

public sealed record CausalEdge(
    string CausePatternId,
    string EffectPatternId,
    double Strength);
