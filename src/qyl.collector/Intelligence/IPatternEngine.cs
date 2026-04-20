namespace Qyl.Collector.Intelligence;

using Qyl.Contracts.Intelligence;

/// <summary>
///     Deterministic pattern matching over telemetry signals.
///     Pure computation — no I/O, no LLM, no side effects.
/// </summary>
public interface IPatternEngine
{
    /// <summary>Match observed signals against all diagnostic patterns. Returns matches ranked by score.</summary>
    IReadOnlyList<PatternMatch> Evaluate(IReadOnlyList<Signal> observedSignals);

    /// <summary>Build a directed causal graph from matched patterns using causal rules.</summary>
    CausalGraph BuildCausalGraph(IReadOnlyList<PatternMatch> matches);

    /// <summary>Find the investigation strategy for a matched pattern (exact ID then category fallback).</summary>
    InvestigationStrategy? SelectStrategy(PatternMatch primaryMatch);
}

/// <summary>A diagnostic pattern matched against observed signals.</summary>
public sealed record PatternMatch(
    DiagnosticPattern Pattern,
    double Score,
    IReadOnlyList<Signal> MatchedSignals);

/// <summary>Directed causal graph with identified root causes.</summary>
public sealed record CausalGraph(
    IReadOnlyList<CausalEdge> Edges,
    IReadOnlyList<string> RootCauses);

/// <summary>Directed causal edge between two pattern IDs.</summary>
public sealed record CausalEdge(
    string CausePatternId,
    string EffectPatternId,
    double Strength);
