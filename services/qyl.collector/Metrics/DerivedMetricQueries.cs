namespace Qyl.Collector.Metrics;

internal static class DerivedMetricQueries
{
    internal static IEnumerable<DerivedMetricDefinition> GetMetricDefinitions()
    {
        return DerivedMetricCatalog.GetDefinitions();
    }

    internal static DerivedMetricDefinition? FindMetric(string name)
    {
        return DerivedMetricCatalog.Find(name);
    }

    internal static IReadOnlyList<string> GetLabelKeys(DerivedMetricDefinition metric)
    {
        if (IsGenAiTokenUsageMetric(metric))
            return
            [
                DerivedMetricCatalog.ServiceNameLabel,
                DerivedMetricCatalog.GenAiProviderNameLabel,
                DerivedMetricCatalog.GenAiRequestModelLabel,
                DerivedMetricCatalog.GenAiTokenTypeLabel
            ];

        if (SupportsGenAiDimensions(metric))
        {
            return
            [
                DerivedMetricCatalog.ServiceNameLabel,
                DerivedMetricCatalog.GenAiProviderNameLabel,
                DerivedMetricCatalog.GenAiRequestModelLabel
            ];
        }

        return [DerivedMetricCatalog.ServiceNameLabel];
    }

    internal static bool TryResolveInterval(string? interval, out string intervalSql, out string error)
    {
        intervalSql = "INTERVAL '1 hour'";
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(interval))
            return true;

        intervalSql = interval.Trim().ToLowerInvariant() switch
        {
            "1m" or "1min" or "1 minute" => "INTERVAL '1 minute'",
            "5m" or "5min" or "5 minutes" => "INTERVAL '5 minutes'",
            "15m" or "15min" or "15 minutes" => "INTERVAL '15 minutes'",
            "1h" or "1hr" or "1 hour" => "INTERVAL '1 hour'",
            "1d" or "1 day" => "INTERVAL '1 day'",
            "1w" or "1 week" => "INTERVAL '1 week'",
            _ => string.Empty
        };

        if (intervalSql.Length > 0)
            return true;

        error = "Metric step must be one of 1m, 5m, 15m, 1h, 1d, or 1w.";
        return false;
    }

    internal static bool IsGenAiTokenUsageMetric(DerivedMetricDefinition metric)
    {
        return metric.Name.Equals(DerivedMetricCatalog.GenAiTokenUsageMetricName, StringComparison.Ordinal);
    }

    internal static bool SupportsGenAiDimensions(DerivedMetricDefinition metric)
    {
        return IsGenAiTokenUsageMetric(metric) ||
               metric.Name.StartsWith("gen_ai.client.", StringComparison.Ordinal);
    }
}
