namespace Qyl.Collector.Analytics;

public static class StatisticalMath
{

    private static double Entr(double x) =>
        double.IsNaN(x) ? x :
        x > 0 ? -x * Math.Log(x) :
        x is 0 ? 0 :
        double.NegativeInfinity;

    public static double RelEntr(double x, double y) =>
        double.IsNaN(x) || double.IsNaN(y) ? double.NaN :
        x > 0 && y > 0 ? x * Math.Log(x / y) :
        x is 0 && y >= 0 ? 0 :
        double.PositiveInfinity;


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

    public static double Entropy(ReadOnlySpan<double> xs)
    {
        double total = 0;
        for (var i = 0; i < xs.Length; i++)
            total += xs[i];

        if (total is 0) return 0;

        double result = 0;
        for (var i = 0; i < xs.Length; i++)
            result += Entr(xs[i] / total);

        return result;
    }

    private static double[] RelativeEntropy(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Mismatched distribution lengths");

        var result = new double[a.Length];
        var idx = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] is not 0)
                result[idx++] = RelEntr(a[i], b[i]);
        }

        return result.AsSpan(0, idx).ToArray();
    }

    public static double KlDivergence(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var re = RelativeEntropy(a, b);
        double sum = 0;
        for (var i = 0; i < re.Length; i++)
            sum += re[i];
        return sum;
    }


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

    private static int[] RankMin(ReadOnlySpan<double> xs, bool ascending = false)
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
        if (lambda is 0.0)
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

    internal static double BoxCoxLlf(double lambdaParam, ReadOnlySpan<double> values)
    {
        var n = values.Length;
        if (n is 0) return 0.0;

        double logSum = 0;
        Span<double> logValues = stackalloc double[n];
        for (var i = 0; i < n; i++)
        {
            logValues[i] = Math.Log(Math.Max(values[i], 1e-10));
            logSum += logValues[i];
        }

        double logvar;
        if (lambdaParam is 0.0)
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
        if (stdDev is 0)
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
