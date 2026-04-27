using Qyl.Collector.Analytics;

namespace Qyl.Collector.Intelligence;

/// <summary>
///     Context for a single semantic diff ("semantic XOR") analysis run.
/// </summary>
public sealed record SemanticDiffContext(
    string EntityId,
    string EntityKind,
    DateTime BaselineStart,
    DateTime BaselineEnd,
    DateTime ComparisonStart,
    DateTime ComparisonEnd,
    IReadOnlyList<SemanticSignal>? Signals = null);

/// <summary>
///     Optional external hints that boost, dampen, or suppress a specific dimension/value pair.
///     Null key/value means the signal applies globally to all candidates in the analysis run.
/// </summary>
public sealed record SemanticSignal(
    string? DimensionKey = null,
    string? DimensionValue = null,
    double NoveltyBoost = 0.0,
    double CausalBoost = 0.0,
    double ActionabilityBoost = 0.0,
    bool Suppress = false,
    string? Reason = null)
{
    public bool AppliesTo(string key, string value) =>
        (DimensionKey is null || string.Equals(DimensionKey, key, StringComparison.Ordinal)) &&
        (DimensionValue is null || string.Equals(DimensionValue, value, StringComparison.Ordinal));
}

/// <summary>
///     Tunable parameters for semantic diff scoring.
/// </summary>
public sealed record SemanticDiffOptions
{
    public static SemanticDiffOptions Default { get; } = new();

    public double LaplaceAlpha { get; init; } = 1e-3;
    public int MinSupportCount { get; init; } = 2;
    public int SupportSaturationCount { get; init; } = 10;
    public double ConcentrationThreshold { get; init; } = 0.50;
    public bool IncludeDecreases { get; init; }
    public double NoveltyWeight { get; init; } = 0.50;
    public double CausalWeight { get; init; } = 0.30;
    public double ActionabilityWeight { get; init; } = 0.20;
    public double MinimumSemanticXorScore { get; init; } = 0.25;
    public double MinimumNoveltyScore { get; init; } = 0.20;
    public double MinimumCausalOverrideScore { get; init; } = 0.35;
    public double EntropyAlpha { get; init; } = 0.20;
    public double KlAlpha { get; init; } = 0.80;
    public int RrfOffset { get; init; } = 60;
    public double DimensionZThreshold { get; init; } = 1.5;
    public string[] NoiseDimensionKeys { get; init; } = [];
    public string[] NoiseDimensionValues { get; init; } = [];
}

/// <summary>
///     One ranked semantic diff candidate.
/// </summary>
public sealed record SemanticDiffRecord(
    string EntityId,
    string EntityKind,
    string DimensionKey,
    string DimensionValue,
    double BaselineCount,
    double ComparisonCount,
    double BaselineShare,
    double ComparisonShare,
    double NoveltyScore,
    double CausalScore,
    double ActionabilityScore,
    double SemanticXorScore,
    bool Suppressed,
    string? WhySuppressed,
    string Summary,
    string WhyItMatters,
    string[] Evidence,
    DateTime BaselineStart,
    DateTime BaselineEnd,
    DateTime ComparisonStart,
    DateTime ComparisonEnd);

/// <summary>
///     Pure semantic diff analyzer.
///     Input = keyed counts for baseline and comparison windows.
///     Output = ranked "what changed that reduces my search space?" candidates.
/// </summary>
public static class SemanticDiffService
{
    public static IReadOnlyList<SemanticDiffRecord> Analyze(
        DistributionComparer.KeyedValueCount[] baseline,
        DistributionComparer.KeyedValueCount[] comparison,
        int totalBaseline,
        int totalComparison,
        SemanticDiffContext context,
        SemanticDiffOptions? options = null,
        int limit = 10,
        bool includeSuppressed = false)
    {
        Guard.NotNull(baseline);
        Guard.NotNull(comparison);
        Guard.NotNull(context);

        ArgumentOutOfRangeException.ThrowIfNegative(totalBaseline);
        ArgumentOutOfRangeException.ThrowIfNegative(totalComparison);
        if (limit <= 0) return [];

        options ??= SemanticDiffOptions.Default;

        var baselineByKey = ToAttributeDict(baseline);
        var comparisonByKey = ToAttributeDict(comparison);
        var dimensionScores = DistributionComparer.KeyedRrfScoreWithFilter(
            baseline, comparison, totalBaseline, totalComparison,
            options.EntropyAlpha, options.KlAlpha, options.RrfOffset, options.DimensionZThreshold);

        var normalizedDimensionScores = NormalizeDimensionScores(dimensionScores);
        var dimensionScoreByKey = new Dictionary<string, DistributionComparer.KeyScoreFiltered>(StringComparer.Ordinal);
        foreach (var score in dimensionScores)
            dimensionScoreByKey[score.Key] = score;

        var signals = context.Signals ?? [];
        var seeds = new List<CandidateSeed>();

        foreach (var key in UnionKeys(baselineByKey, comparisonByKey))
        {
            if (!comparisonByKey.TryGetValue(key, out var comparisonValues))
                continue;

            baselineByKey.TryGetValue(key, out var baselineValues);

            var normalizedDimensionScore = normalizedDimensionScores.GetValueOrDefault(key, 0.0);
            var dimensionFiltered = dimensionScoreByKey.TryGetValue(key, out var ds) && ds.Filtered;
            var valueCount = CountDistinctValues(baselineValues, comparisonValues);

            foreach (var value in UnionValues(baselineValues, comparisonValues))
            {
                var bCount = GetCount(baselineValues, value);
                var cCount = GetCount(comparisonValues, value);
                var bShare = totalBaseline > 0 ? bCount / totalBaseline : 0.0;
                var cShare = totalComparison > 0 ? cCount / totalComparison : 0.0;
                var bProb = SmoothedProbability(bCount, totalBaseline, valueCount, options.LaplaceAlpha);
                var cProb = SmoothedProbability(cCount, totalComparison, valueCount, options.LaplaceAlpha);

                var positiveDelta = Math.Max(0, cProb - bProb);
                if (!options.IncludeDecreases && positiveDelta <= 0 && cCount <= bCount)
                    continue;

                var firstAppearance = bCount <= 0 && cCount > 0 ? 1.0 : 0.0;
                var positiveKl = cProb > bProb ? StatisticalMath.RelEntr(cProb, bProb) : 0.0;

                var rawNovelty = positiveKl + positiveDelta + (firstAppearance * 0.50) +
                                 (normalizedDimensionScore * 0.35);
                var supportScore = options.SupportSaturationCount > 0
                    ? Clamp01(cCount / options.SupportSaturationCount)
                    : cCount > 0
                        ? 1.0
                        : 0.0;
                var concentrationScore = options.ConcentrationThreshold > 0
                    ? Clamp01(cShare / options.ConcentrationThreshold)
                    : cShare > 0
                        ? 1.0
                        : 0.0;
                var actionablePattern = IsKnownActionablePattern(key, value) ? 1.0 : 0.0;

                var rawActionability = (concentrationScore * 0.40) + (supportScore * 0.25) +
                                       (normalizedDimensionScore * 0.20) + (actionablePattern * 0.15);
                var rawCausal = firstAppearance * 0.10;
                if (IsLocalizedDimension(key) && cShare >= options.ConcentrationThreshold) rawCausal += 0.10;
                if (normalizedDimensionScore >= 0.80) rawCausal += 0.05;

                var evidence = new List<string>(4);
                if (firstAppearance > 0) evidence.Add("Value did not appear in the baseline window.");
                if (cShare >= options.ConcentrationThreshold)
                    evidence.Add("Value is concentrated in the comparison window.");
                if (positiveKl > 0) evidence.Add("Distributional surprise is elevated for this value.");

                double noveltyBoost = 0, causalBoost = 0, actionabilityBoost = 0;
                string? forcedSuppression = null;

                foreach (var signal in signals)
                {
                    if (!signal.AppliesTo(key, value)) continue;
                    noveltyBoost += signal.NoveltyBoost;
                    causalBoost += signal.CausalBoost;
                    actionabilityBoost += signal.ActionabilityBoost;
                    if (signal.Suppress && forcedSuppression is null)
                        forcedSuppression = signal.Reason ?? "suppressed by semantic signal";
                    if (!string.IsNullOrWhiteSpace(signal.Reason))
                        evidence.Add(signal.Reason);
                }

                seeds.Add(new CandidateSeed(key, value, bCount, cCount, bShare, cShare,
                    rawNovelty, rawCausal, rawActionability, noveltyBoost, causalBoost, actionabilityBoost,
                    dimensionFiltered, forcedSuppression, evidence.ToArray()));
            }
        }

        if (seeds.Count is 0) return [];

        var normalizedNovelty = NormalizeArray(seeds, static seed => seed.RawNovelty);
        var results = new List<SemanticDiffRecord>(seeds.Count);

        for (var i = 0; i < seeds.Count; i++)
        {
            var seed = seeds[i];
            var noveltyScore = Clamp01(normalizedNovelty[i] + seed.NoveltyBoost);
            var causalScore = Clamp01(seed.RawCausal + seed.CausalBoost);
            var actionabilityScore = Clamp01(seed.RawActionability + seed.ActionabilityBoost);
            var xorScore = (noveltyScore * options.NoveltyWeight) + (causalScore * options.CausalWeight) +
                           (actionabilityScore * options.ActionabilityWeight);

            var (suppressed, why) = DecideSuppression(seed, noveltyScore, causalScore, xorScore, options);

            results.Add(new SemanticDiffRecord(
                context.EntityId, context.EntityKind, seed.Key, seed.Value,
                seed.BaselineCount, seed.ComparisonCount, seed.BaselineShare, seed.ComparisonShare,
                noveltyScore, causalScore, actionabilityScore, xorScore,
                suppressed, why,
                string.Create(CultureInfo.InvariantCulture,
                    $"{seed.Key}={DisplayValue(seed.Value)} shifted from {seed.BaselineShare:P1} to {seed.ComparisonShare:P1}."),
                BuildWhyItMatters(seed, noveltyScore, causalScore, actionabilityScore, options),
                seed.Evidence,
                context.BaselineStart, context.BaselineEnd, context.ComparisonStart, context.ComparisonEnd));
        }

        results.Sort(static (x, y) =>
        {
            var s = x.Suppressed.CompareTo(y.Suppressed);
            return s is not 0 ? s
                : y.SemanticXorScore.CompareTo(x.SemanticXorScore) is var sc && sc is not 0 ? sc
                : y.NoveltyScore.CompareTo(x.NoveltyScore) is var n && n is not 0 ? n
                : string.CompareOrdinal(x.DimensionKey, y.DimensionKey);
        });

        if (!includeSuppressed) results.RemoveAll(static d => d.Suppressed);
        if (results.Count > limit) results.RemoveRange(limit, results.Count - limit);
        return results;
    }

    private static (bool, string?) DecideSuppression(CandidateSeed seed, double novelty, double causal, double xor,
        SemanticDiffOptions o) =>
        seed.ForcedSuppression is not null
            ? (true, seed.ForcedSuppression)
            : Array.Exists(o.NoiseDimensionKeys, k => string.Equals(k, seed.Key, StringComparison.Ordinal))
                ? (true, "configured noise dimension")
                : Array.Exists(o.NoiseDimensionValues, v => string.Equals(v, seed.Value, StringComparison.Ordinal))
                    ? (true, "configured noise value")
                    : seed.ComparisonCount < o.MinSupportCount
                        ? (true, "low comparison support")
                        : seed.DimensionFiltered && causal < o.MinimumCausalOverrideScore
                            ? (true, "dimension stayed within expected variance")
                            : novelty < o.MinimumNoveltyScore && causal < o.MinimumCausalOverrideScore
                                ? (true, "low novelty and weak causal alignment")
                                : xor < o.MinimumSemanticXorScore
                                    ? (true, "semantic XOR score below threshold")
                                    : (false, null);

    private static string BuildWhyItMatters(CandidateSeed seed, double novelty, double causal, double actionability,
        SemanticDiffOptions o)
    {
        var r = new List<string>(4);
        if (seed is { BaselineCount: <= 0, ComparisonCount: > 0 }) r.Add("It is new in the comparison window.");
        if (seed.ComparisonShare >= o.ConcentrationThreshold)
            r.Add("It materially narrows the search space because it is concentrated.");
        if (causal >= o.MinimumCausalOverrideScore) r.Add("It aligns with external causal evidence.");
        if (actionability >= 0.65) r.Add("It is actionable enough to guide a targeted fix.");
        if (r.Count is 0)
        {
            r.Add(novelty >= 0.50
                ? "It represents a meaningful behavioral shift."
                : "It is one of the least noisy surviving changes.");
        }

        return string.Join(" ", r);
    }

    private static Dictionary<string, Dictionary<string, double>> ToAttributeDict(
        DistributionComparer.KeyedValueCount[] rows)
    {
        var result = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        foreach (ref readonly var row in rows.AsSpan())
        {
            if (!result.TryGetValue(row.Key, out var values))
                result[row.Key] = values = new Dictionary<string, double>(StringComparer.Ordinal);
            values[row.Value] = row.Count;
        }

        return result;
    }

    private static HashSet<string> UnionKeys(Dictionary<string, Dictionary<string, double>> a,
        Dictionary<string, Dictionary<string, double>> b)
    {
        var keys = new HashSet<string>(a.Keys, StringComparer.Ordinal);
        keys.UnionWith(b.Keys);
        return keys;
    }

    private static HashSet<string> UnionValues(Dictionary<string, double>? a, Dictionary<string, double>? b)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        if (a is not null) values.UnionWith(a.Keys);
        if (b is not null) values.UnionWith(b.Keys);
        return values;
    }

    private static int CountDistinctValues(Dictionary<string, double>? a, Dictionary<string, double>? b) =>
        Math.Max(1, UnionValues(a, b).Count);

    private static double GetCount(Dictionary<string, double>? values, string value) =>
        values is not null && values.TryGetValue(value, out var count) ? count : 0.0;

    private static double SmoothedProbability(double count, int total, int valueCount, double alpha)
    {
        var vc = Math.Max(1, valueCount);
        var denominator = total + (alpha * vc);
        return denominator <= 0 ? 1.0 / vc : (count + alpha) / denominator;
    }

    private static double Clamp01(double v) => double.IsNaN(v) ? 0.0 : Math.Clamp(v, 0.0, 1.0);

    private static Dictionary<string, double> NormalizeDimensionScores(
        List<DistributionComparer.KeyScoreFiltered> scores)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (scores.Count is 0) return result;
        var min = scores.Min(static s => s.Score);
        var max = scores.Max(static s => s.Score);
        foreach (var s in scores) result[s.Key] = Normalize(s.Score, min, max);
        return result;
    }

    private static double[] NormalizeArray(List<CandidateSeed> seeds, Func<CandidateSeed, double> selector)
    {
        var result = new double[seeds.Count];
        var min = double.MaxValue;
        var max = double.MinValue;
        for (var i = 0; i < seeds.Count; i++)
        {
            var v = selector(seeds[i]);
            if (v < min) min = v;
            if (v > max) max = v;
        }

        for (var i = 0; i < seeds.Count; i++) result[i] = Normalize(selector(seeds[i]), min, max);
        return result;
    }

    private static double Normalize(double v, double min, double max) =>
        max - min <= 1e-12 ? v > 0 ? 1.0 : 0.0 : Clamp01((v - min) / (max - min));

    private static bool IsLocalizedDimension(string key) =>
        key.ContainsIgnoreCase("route") || key.ContainsIgnoreCase("endpoint") ||
        key.ContainsIgnoreCase("service") || key.ContainsIgnoreCase("version") ||
        key.ContainsIgnoreCase("exception");

    private static bool IsKnownActionablePattern(string key, string value) =>
        (key.ContainsIgnoreCase("exception") || key.ContainsIgnoreCase("error")) &&
        (value.ContainsIgnoreCase("NullReference") || value.ContainsIgnoreCase("ArgumentException") ||
         value.ContainsIgnoreCase("InvalidOperation") || value.ContainsIgnoreCase("Timeout") ||
         value.ContainsIgnoreCase("Failed to fetch"));

    private static string DisplayValue(string value) => string.IsNullOrEmpty(value) ? "(missing)" : value;

    private sealed record CandidateSeed(
        string Key,
        string Value,
        double BaselineCount,
        double ComparisonCount,
        double BaselineShare,
        double ComparisonShare,
        double RawNovelty,
        double RawCausal,
        double RawActionability,
        double NoveltyBoost,
        double CausalBoost,
        double ActionabilityBoost,
        bool DimensionFiltered,
        string? ForcedSuppression,
        string[] Evidence);
}
