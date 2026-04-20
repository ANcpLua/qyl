namespace Qyl.Collector.Intelligence;

using System.Text.RegularExpressions;
using Qyl.Contracts.Intelligence;

/// <summary>
///     Pure computation engine for matching observed telemetry signals against
///     diagnostic patterns, building causal graphs, and selecting investigation strategies.
///     No I/O, no LLM, no side effects. Deterministic: same input → same output.
/// </summary>
public sealed class PatternEngine(
    IReadOnlyList<DiagnosticPattern> patterns,
    IReadOnlyList<CausalRule> causalRules,
    IReadOnlyList<InvestigationStrategy> strategies) : IPatternEngine
{
    public IReadOnlyList<PatternMatch> Evaluate(IReadOnlyList<Signal> observedSignals)
    {
        var matches = new List<PatternMatch>();

        foreach (var pattern in patterns)
        {
            var matchedSignals = new List<Signal>();
            var allSatisfied = true;

            foreach (var required in pattern.Signals)
            {
                if (EvaluateSignal(required, observedSignals))
                {
                    matchedSignals.Add(required);
                }
                else
                {
                    allSatisfied = false;
                    break;
                }
            }

            if (!allSatisfied || pattern.Signals.Count is 0)
                continue;

            var score = pattern.Confidence * ((double)matchedSignals.Count / pattern.Signals.Count);
            matches.Add(new PatternMatch(pattern, score, matchedSignals));
        }

        matches.Sort((a, b) => b.Score.CompareTo(a.Score));
        return matches;
    }

    public CausalGraph BuildCausalGraph(IReadOnlyList<PatternMatch> matches)
    {
        var matchedIds = new HashSet<string>(matches.Select(m => m.Pattern.Id), StringComparer.Ordinal);

        var edges = new List<CausalEdge>();
        var hasIncoming = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in causalRules)
        {
            if (!matchedIds.Contains(rule.CausePattern) || !matchedIds.Contains(rule.EffectPattern))
                continue;

            edges.Add(new CausalEdge(rule.CausePattern, rule.EffectPattern, rule.Strength));
            hasIncoming.Add(rule.EffectPattern);
        }

        var rootCauses = matchedIds
            .Where(id => !hasIncoming.Contains(id))
            .Order(StringComparer.Ordinal)
            .ToList();

        return new CausalGraph(edges, rootCauses);
    }

    public InvestigationStrategy? SelectStrategy(PatternMatch primaryMatch)
    {
        var patternId = primaryMatch.Pattern.Id;
        var categoryTrigger = $"category:{primaryMatch.Pattern.Category.ToString().ToLowerInvariant()}";

        // Exact pattern ID match first, then category-wide trigger
        return strategies.FirstOrDefault(s => s.TriggerPattern.EqualsOrdinal(patternId))
               ?? strategies.FirstOrDefault(s => s.TriggerPattern.EqualsIgnoreCase(categoryTrigger));
    }

    private static bool EvaluateSignal(Signal required, IReadOnlyList<Signal> observed)
    {
        switch (required.Operator)
        {
            case SignalOperator.NotExists:
                return !observed.Any(o => o.Attribute.EqualsOrdinal(required.Attribute));
            case SignalOperator.Exists:
                return observed.Any(o => o.Attribute.EqualsOrdinal(required.Attribute));
        }

        foreach (var obs in observed)
        {
            if (!obs.Attribute.EqualsOrdinal(required.Attribute))
                continue;

            if (EvaluateOperator(required.Operator, obs.Value, required.Value))
                return true;
        }

        return false;
    }

    private static bool EvaluateOperator(SignalOperator op, string? observedValue, string? expectedValue) =>
        op switch
        {
            SignalOperator.Eq => string.Equals(observedValue, expectedValue, StringComparison.Ordinal),
            SignalOperator.Neq => !string.Equals(observedValue, expectedValue, StringComparison.Ordinal),
            SignalOperator.Contains => observedValue is not null
                                       && expectedValue is not null
                                       && observedValue.ContainsIgnoreCase(expectedValue),
            SignalOperator.Matches => observedValue is not null
                                      && expectedValue is not null
                                      && Regex.IsMatch(observedValue, expectedValue, RegexOptions.None,
                                          TimeSpan.FromSeconds(1)),
            SignalOperator.InSet => observedValue is not null
                                    && expectedValue is not null
                                    && expectedValue.Split(',').Contains(observedValue, StringComparer.Ordinal),
            SignalOperator.Gt => CompareNumeric(observedValue, expectedValue) > 0,
            SignalOperator.Gte => CompareNumeric(observedValue, expectedValue) >= 0,
            SignalOperator.Lt => CompareNumeric(observedValue, expectedValue) < 0,
            SignalOperator.Lte => CompareNumeric(observedValue, expectedValue) <= 0,
            _ => false
        };

    private static int CompareNumeric(string? observed, string? expected)
    {
        if (double.TryParse(observed, CultureInfo.InvariantCulture, out var obsVal)
            && double.TryParse(expected, CultureInfo.InvariantCulture, out var expVal))
        {
            return obsVal.CompareTo(expVal);
        }

        return string.Compare(observed, expected, StringComparison.Ordinal);
    }
}
