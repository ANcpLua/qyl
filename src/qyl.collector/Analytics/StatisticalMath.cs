namespace Qyl.Collector.Analytics;

/// <summary>
///     Pure statistical math functions ported from Sentry Seer (BSD-3-Clause, SciPy vendored).
///     No external dependencies — all self-contained numerical algorithms.
/// </summary>
public static class StatisticalMath
{
    // =========================================================================
    // SciPy Vendored — Convex Analysis (BSD-3-Clause)
    // https://github.com/scipy/scipy/blob/ce4b4309/scipy/special/_convex_analysis.pxd
    // =========================================================================

    /// <summary>
    ///     Elementwise function for computing entropy: <c>-x * ln(x)</c>.
    /// </summary>
    public static double Entr(double x) =>
        double.IsNaN(x) ? x :
        x > 0 ? -x * Math.Log(x) :
        x == 0 ? 0 :
        double.NegativeInfinity;

    /// <summary>
    ///     Relative entropy: <c>x * ln(x / y)</c>.
    /// </summary>
    public static double RelEntr(double x, double y) =>
        double.IsNaN(x) || double.IsNaN(y) ? double.NaN :
        x > 0 && y > 0 ? x * Math.Log(x / y) :
        x == 0 && y >= 0 ? 0 :
        double.PositiveInfinity;

    // =========================================================================
    // Probability & Information Theory
    // =========================================================================

    /// <summary>
    ///     Laplace smooth a probability distribution to remove zeros while
    ///     preserving relative proportions. Default alpha = 1e-3.
    /// </summary>
    public static double[] LaplaceSmooth(ReadOnlySpan<double> probabilities, double alpha = 1e-3)
    {
        double total = 0;
        for (var i = 0; i < probabilities.Length; i++)
            total += probabilities[i];

        var result = new double[probabilities.Length];
        var denominator = total + (alpha * probabilities.Length);
        for (var i = 0; i < probabilities.Length; i++)
            result[i] = (probabilities[i] + alpha) / denominator;

        return result;
    }

    /// <summary>
    ///     Shannon entropy: <c>H = -Σ p(x) log(p(x))</c>.
    ///     Input is normalized to sum to 1.
    /// </summary>
    public static double Entropy(ReadOnlySpan<double> xs)
    {
        double total = 0;
        for (var i = 0; i < xs.Length; i++)
            total += xs[i];

        if (total == 0) return 0;

        double result = 0;
        for (var i = 0; i < xs.Length; i++)
            result += Entr(xs[i] / total);

        return result;
    }

    /// <summary>
    ///     Elementwise relative entropy between two distributions.
    /// </summary>
    public static double[] RelativeEntropy(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Mismatched distribution lengths");

        var result = new double[a.Length];
        var idx = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != 0)
                result[idx++] = RelEntr(a[i], b[i]);
        }

        return result.AsSpan(0, idx).ToArray();
    }

    /// <summary>
    ///     Kullback–Leibler divergence: <c>D_KL(a || b) = Σ rel_entr(a_i, b_i)</c>.
    /// </summary>
    public static double KlDivergence(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var re = RelativeEntropy(a, b);
        double sum = 0;
        for (var i = 0; i < re.Length; i++)
            sum += re[i];
        return sum;
    }

    // =========================================================================
    // Ranking & Fusion
    // =========================================================================

    /// <summary>
    ///     Reciprocal Rank Fusion score combining entropy and KL rankings.
    ///     Alphas must sum to 1.0.
    /// </summary>
    public static double[] RrfScore(
        ReadOnlySpan<double> entropyScores,
        ReadOnlySpan<double> klScores,
        double entropyAlpha = 0.2,
        double klAlpha = 0.8,
        int offset = 60)
    {
        if (!double.IsNormal(entropyAlpha + klAlpha - 1.0)
            && Math.Abs(entropyAlpha + klAlpha - 1.0) > 1e-9)
            throw new ArgumentException("Entropy alpha and KL alpha must sum to 1.");

        var entropyRanks = RankMin(entropyScores, true);
        var klRanks = RankMin(klScores);

        var result = new double[entropyScores.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var a = klAlpha * (1.0 / (offset + klRanks[i]));
            var b = entropyAlpha * (1.0 / (offset + entropyRanks[i]));
            result[i] = a + b;
        }

        return result;
    }

    /// <summary>
    ///     Assign dense ranks to values using min-rank strategy.
    /// </summary>
    public static int[] RankMin(ReadOnlySpan<double> xs, bool ascending = false)
    {
        var sorted = new SortedSet<double>(xs.ToArray());
        var ranks = new Dictionary<double, int>();
        var rank = 1;

        var ordered = ascending
            ? sorted
            : sorted.Reverse();

        foreach (var val in ordered)
            ranks[val] = rank++;

        var result = new int[xs.Length];
        for (var i = 0; i < xs.Length; i++)
            result[i] = ranks[xs[i]];

        return result;
    }

    // =========================================================================
    // Box-Cox Transform
    // =========================================================================

    /// <summary>
    ///     Apply Box-Cox transformation. If <paramref name="lambdaParam" /> is null,
    ///     finds the optimal lambda via MLE with ternary search.
    /// </summary>
    public static (double[] Transformed, double Lambda) BoxCoxTransform(
        ReadOnlySpan<double> values, double? lambdaParam = null)
    {
        if (values.IsEmpty)
            return ([], lambdaParam ?? 0.0);

        var minValue = double.MaxValue;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] < minValue)
                minValue = values[i];
        }

        Span<double> shifted = stackalloc double[values.Length];
        if (minValue <= 0)
        {
            var shiftAmount = -minValue + 1e-10;
            for (var i = 0; i < values.Length; i++)
                shifted[i] = values[i] + shiftAmount;
        }
        else
        {
            values.CopyTo(shifted);
        }

        var lambda = lambdaParam ?? BoxCoxNormMax(shifted);

        var transformed = new double[values.Length];
        if (lambda == 0.0)
        {
            for (var i = 0; i < shifted.Length; i++)
                transformed[i] = Math.Log(Math.Max(shifted[i], 1e-10));
        }
        else
        {
            for (var i = 0; i < shifted.Length; i++)
                transformed[i] = (Math.Pow(Math.Max(shifted[i], 1e-10), lambda) - 1) / lambda;
        }

        return (transformed, lambda);
    }

    /// <summary>
    ///     Box-Cox log-likelihood function using numerically stable log-space arithmetic.
    /// </summary>
    internal static double BoxCoxLlf(double lambdaParam, ReadOnlySpan<double> values)
    {
        var n = values.Length;
        if (n == 0) return 0.0;

        double logSum = 0;
        Span<double> logValues = stackalloc double[n];
        for (var i = 0; i < n; i++)
        {
            logValues[i] = Math.Log(Math.Max(values[i], 1e-10));
            logSum += logValues[i];
        }

        double logvar;
        if (lambdaParam == 0.0)
        {
            var logMean = logSum / n;
            double logVar = 0;
            for (var i = 0; i < n; i++)
                logVar += (logValues[i] - logMean) * (logValues[i] - logMean);
            logVar /= n;
            logvar = Math.Log(Math.Max(logVar, 1e-10));
        }
        else
        {
            Span<double> logx = stackalloc double[n];
            double logxMean = 0;
            for (var i = 0; i < n; i++)
            {
                logx[i] = lambdaParam * logValues[i];
                logxMean += logx[i];
            }

            logxMean /= n;
            double logxVar = 0;
            for (var i = 0; i < n; i++)
                logxVar += (logx[i] - logxMean) * (logx[i] - logxMean);
            logxVar /= n;
            logvar = Math.Log(Math.Max(logxVar, 1e-10)) - (2 * Math.Log(Math.Abs(lambdaParam)));
        }

        return ((lambdaParam - 1) * logSum) - (n / 2.0 * logvar);
    }

    /// <summary>
    ///     Find optimal Box-Cox lambda via MLE with ternary search over [-2, 2].
    /// </summary>
    internal static double BoxCoxNormMax(ReadOnlySpan<double> values, int maxIters = 100)
    {
        if (values.IsEmpty) return 0.0;

        var left = -2.0;
        var right = 2.0;
        const double tolerance = 1e-6;

        for (var i = 0; i < maxIters && right - left > tolerance; i++)
        {
            var m1 = left + ((right - left) / 3);
            var m2 = right - ((right - left) / 3);

            if (BoxCoxLlf(m1, values) > BoxCoxLlf(m2, values))
                right = m2;
            else
                left = m1;
        }

        return (left + right) / 2;
    }

    // =========================================================================
    // Z-Scores
    // =========================================================================

    /// <summary>
    ///     Calculate Z-scores for a list of values: <c>(x - μ) / σ</c>.
    /// </summary>
    public static double[] CalculateZScores(ReadOnlySpan<double> values)
    {
        if (values.IsEmpty) return [];

        double sum = 0;
        for (var i = 0; i < values.Length; i++)
            sum += values[i];

        var mean = sum / values.Length;

        double variance = 0;
        for (var i = 0; i < values.Length; i++)
            variance += (values[i] - mean) * (values[i] - mean);
        variance /= values.Length;

        var stdDev = Math.Sqrt(variance);
        if (stdDev == 0)
        {
            var zeros = new double[values.Length];
            return zeros;
        }

        var result = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
            result[i] = (values[i] - mean) / stdDev;

        return result;
    }
}
