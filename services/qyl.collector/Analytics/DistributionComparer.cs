namespace Qyl.Collector.Analytics;

/// <summary>
///     Compares multi-dimensional keyed distributions using KL divergence,
///     Shannon entropy, and Reciprocal Rank Fusion scoring.
/// </summary>
public static class DistributionComparer
{
    /// <summary>
    ///     RRF-score with Box-Cox + Z-score filtering. Keys below the Z threshold
    ///     on both entropy and KL are marked as filtered.
    /// </summary>
    public static List<KeyScoreFiltered> KeyedRrfScoreWithFilter(
        KeyedValueCount[] baseline,
        KeyedValueCount[] outliers,
        int totalBaseline,
        int totalOutliers,
        double entropyAlpha = 0.2,
        double klAlpha = 0.8,
        int offset = 60,
        double zThreshold = 1.5)
    {
        List<string> keys = [];
        List<double> entropyScores = [];
        List<double> klScores = [];

        foreach (var (key, a, b) in GenNormalizedDistributions(baseline, outliers, totalBaseline, totalOutliers))
        {
            keys.Add(key);
            entropyScores.Add(StatisticalMath.Entropy(b));
            klScores.Add(StatisticalMath.KlDivergence(a, b));
        }

        double[] eArr = [.. entropyScores];
        double[] klArr = [.. klScores];
        var (normalizedEntropy, _) = StatisticalMath.BoxCoxTransform(eArr);
        var (normalizedKl, _) = StatisticalMath.BoxCoxTransform(klArr);
        var entropyZ = StatisticalMath.CalculateZScores(normalizedEntropy);
        var klZ = StatisticalMath.CalculateZScores(normalizedKl);
        var rrfScores = StatisticalMath.RrfScore(eArr, klArr, entropyAlpha, klAlpha, offset);

        var results = new List<KeyScoreFiltered>(keys.Count);
        for (var i = 0; i < keys.Count; i++)
        {
            var filtered = entropyZ[i] <= zThreshold && klZ[i] <= zThreshold;
            results.Add(new KeyScoreFiltered(keys[i], rrfScores[i], filtered));
        }

        results.Sort(static (x, y) => y.Score.CompareTo(x.Score));
        return results;
    }

    // =========================================================================
    // Internal distribution normalization pipeline
    // =========================================================================

    private static List<(string Key, double[] Baseline, double[] Outliers)>
        GenNormalizedDistributions(
            KeyedValueCount[] baseline,
            KeyedValueCount[] outliers,
            int totalBaseline,
            int totalOutliers)
    {
        var keyedBaseline = ToAttributeDict(baseline);
        var keyedOutliers = ToAttributeDict(outliers);
        var results = new List<(string, double[], double[])>();

        foreach (var key in keyedOutliers.Keys)
        {
            if (!keyedBaseline.TryGetValue(key, out var baselineDist))
                continue;

            var outliersDist = keyedOutliers[key];

            AddUnseenValue(baselineDist, totalBaseline);
            AddUnseenValue(outliersDist, totalOutliers);
            EnsureSymmetry(baselineDist, outliersDist);

            var smoothedBaseline = StatisticalMath.LaplaceSmooth(
                [.. baselineDist.Values]);
            var smoothedOutliers = StatisticalMath.LaplaceSmooth(
                [.. outliersDist.Values]);

            results.Add((key, smoothedBaseline, smoothedOutliers));
        }

        return results;
    }

    private static Dictionary<string, Dictionary<string, double>> ToAttributeDict(
        KeyedValueCount[] rows)
    {
        var result = new Dictionary<string, Dictionary<string, double>>();
        foreach (ref readonly var row in rows.AsSpan())
        {
            if (!result.TryGetValue(row.Key, out var dict))
                result[row.Key] = dict = [];
            dict[row.Value] = row.Count;
        }

        return result;
    }

    private static void AddUnseenValue(Dictionary<string, double> dist, int total)
    {
        double count = 0;
        foreach (var v in dist.Values)
            count += v;

        var delta = total - count;
        if (delta > 0)
            dist[""] = delta;
    }

    private static void EnsureSymmetry(
        Dictionary<string, double> a, Dictionary<string, double> b)
    {
        foreach (var key in b.Keys)
            a.TryAdd(key, 0);
        foreach (var key in a.Keys)
            b.TryAdd(key, 0);
    }

    /// <summary>Key, value, count triple representing one observation.</summary>
    public readonly record struct KeyedValueCount(string Key, string Value, double Count);

    /// <summary>Key with score and filter flag.</summary>
    public readonly record struct KeyScoreFiltered(string Key, double Score, bool Filtered);
}
