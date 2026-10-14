using System.Text.Json.Nodes;

namespace Qyl.Collector.Storage;

// Shared by product metric queries and agent analysis; temporality and bucket math have one owner.
internal static class MetricHistogram
{
    // Cumulative exports overlap. Subtract within each series before merging streams.
    // Without a prior observation, only a sample started inside this window is attributable.
    internal static MetricPointRow? HistogramDelta(MetricPointRow point, MetricPointRow? previous, ulong windowStart)
    {
        if (point.HistogramCount is null) return null;
        if (previous is null) return point.StartTimeUnixNano >= windowStart ? point : null;
        if (point.StartTimeUnixNano != previous.StartTimeUnixNano || point.HistogramCount < previous.HistogramCount) return point;
        var current = JsonNode.Parse(point.BucketCountsJson ?? "[]")!.AsArray().Select(v => v!.GetValue<ulong>()).ToArray();
        var before = JsonNode.Parse(previous.BucketCountsJson ?? "[]")!.AsArray().Select(v => v!.GetValue<ulong>()).ToArray();
        if (point.BucketBoundsJson != previous.BucketBoundsJson || current.Length != before.Length) return null;
        if (current.Where((count, index) => count < before[index]).Any()) return point;
        return point with
        {
            StartTimeUnixNano = previous.TimeUnixNano,
            HistogramCount = point.HistogramCount - previous.HistogramCount,
            HistogramSum = point.HistogramSum - previous.HistogramSum,
            // Extrema of an interval cannot be recovered by subtracting cumulative extrema.
            HistogramMin = null, HistogramMax = null,
            BucketCountsJson = "[" + string.Join(",", current.Select((count, index) => (count - before[index]).ToString(CultureInfo.InvariantCulture))) + "]"
        };
    }
    internal static double? Histogram(List<MetricPointRow> points, string aggregation)
    {
        if (points.Count is 0) return null;
        var count = points.Sum(static p => (double)(p.HistogramCount ?? 0)); var sum = points.Sum(static p => p.HistogramSum ?? 0);
        if (aggregation == "count") return count;
        if (aggregation == "sum") return sum;
        if (aggregation == "avg") return count is 0 ? null : sum / count;
        if (aggregation == "min") return points.Min(static p => p.HistogramMin);
        if (aggregation == "max") return points.Max(static p => p.HistogramMax);
        if (!double.TryParse(aggregation.AsSpan(1), CultureInfo.InvariantCulture, out var percentile)) throw new ArgumentOutOfRangeException(nameof(aggregation), "Choose a supported histogram aggregation.");
        if (points.Select(static p => p.BucketBoundsJson).Distinct(StringComparer.Ordinal).Skip(1).Any()) return null;
        var merged = new SortedDictionary<double, double>();
        foreach (var point in points)
        {
            var bounds = JsonNode.Parse(point.BucketBoundsJson ?? "[]")!.AsArray().Select(static v => v!.GetValue<double>()).ToArray();
            var counts = JsonNode.Parse(point.BucketCountsJson ?? "[]")!.AsArray().Select(static v => v!.GetValue<double>()).ToArray();
            for (var i = 0; i < counts.Length; i++) { var bound = i < bounds.Length ? bounds[i] : double.PositiveInfinity; merged[bound] = merged.GetValueOrDefault(bound) + counts[i]; }
        }
        var target = count * percentile / 100; double cumulative = 0, lower = 0;
        foreach (var (upper, amount) in merged)
        {
            if (cumulative + amount >= target && amount > 0)
                return double.IsInfinity(upper) ? points.Max(static p => p.HistogramMax) ?? lower : lower + (upper - lower) * (target - cumulative) / amount;
            cumulative += amount; lower = upper;
        }
        return null;
    }
}
