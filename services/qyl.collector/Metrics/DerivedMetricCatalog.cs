namespace Qyl.Collector.Metrics;

internal static class DerivedMetricCatalog
{
    internal const string ServiceNameLabel = "service.name";
    internal const string GenAiTokenUsageMetricName = "gen_ai.client.token.usage";
    internal const string GenAiTokenTypeLabel = "gen_ai.token.type";
    internal const string GenAiProviderNameLabel = "gen_ai.provider.name";
    internal const string GenAiRequestModelLabel = "gen_ai.request.model";
    internal const string InputTokenType = "input";
    internal const string OutputTokenType = "output";

    private const string GenAiSpanPredicate =
        "gen_ai_provider_name IS NOT NULL OR gen_ai_request_model IS NOT NULL OR gen_ai_input_tokens IS NOT NULL OR gen_ai_output_tokens IS NOT NULL";

    private static readonly FrozenDictionary<string, DerivedMetricDefinition> s_metrics =
        new[]
        {
            new DerivedMetricDefinition(
                Name: "request_count",
                Type: "sum",
                Description: "Count of stored spans per time bucket.",
                Unit: "{span}",
                Expression: "COUNT(*)",
                Predicate: null),
            new DerivedMetricDefinition(
                Name: "error_rate",
                Type: "gauge",
                Description: "Ratio of stored spans whose status_code is error.",
                Unit: "1",
                Expression:
                "COALESCE(CAST(SUM(CASE WHEN TRY_CAST(status_code AS INTEGER) = 2 THEN 1 ELSE 0 END) AS DOUBLE) / NULLIF(COUNT(*), 0), 0)",
                Predicate: null),
            new DerivedMetricDefinition(
                Name: "latency_p50_ms",
                Type: "gauge",
                Description: "Median span duration derived from stored spans.",
                Unit: "ms",
                Expression: "COALESCE(PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY duration_ns / 1000000.0), 0)",
                Predicate: null),
            new DerivedMetricDefinition(
                Name: "latency_p95_ms",
                Type: "gauge",
                Description: "95th percentile span duration derived from stored spans.",
                Unit: "ms",
                Expression: "COALESCE(PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns / 1000000.0), 0)",
                Predicate: null),
            new DerivedMetricDefinition(
                Name: "latency_p99_ms",
                Type: "gauge",
                Description: "99th percentile span duration derived from stored spans.",
                Unit: "ms",
                Expression: "COALESCE(PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ns / 1000000.0), 0)",
                Predicate: null),
            new DerivedMetricDefinition(
                Name: GenAiTokenUsageMetricName,
                Type: "histogram",
                Description: "Number of input and output tokens used, derived from stored span token columns.",
                Unit: "{token}",
                Expression: "COALESCE(SUM(COALESCE(gen_ai_input_tokens, 0) + COALESCE(gen_ai_output_tokens, 0)), 0)",
                Predicate: "gen_ai_input_tokens IS NOT NULL OR gen_ai_output_tokens IS NOT NULL"),
            new DerivedMetricDefinition(
                Name: "gen_ai.client.operation.duration",
                Type: "histogram",
                Description: "Average GenAI operation duration derived from stored spans.",
                Unit: "s",
                Expression: "COALESCE(AVG(duration_ns / 1000000000.0), 0)",
                Predicate: GenAiSpanPredicate),
            new DerivedMetricDefinition(
                Name: "gen_ai.client.cost",
                Type: "sum",
                Description: "Estimated GenAI cost derived from stored span cost columns.",
                Unit: "USD",
                Expression: "COALESCE(SUM(gen_ai_cost_usd), 0)",
                Predicate: "gen_ai_cost_usd IS NOT NULL")
        }.ToFrozenDictionary(static metric => metric.Name, StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, AnomalyMetricSelection> s_anomalyAliases =
        new Dictionary<string, AnomalyMetricSelection>(StringComparer.Ordinal)
        {
            ["latency"] = new(
                "PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns)",
                Predicate: null),
            ["latency_p50"] = new(
                "PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY duration_ns)",
                Predicate: null),
            ["latency_p95"] = new(
                "PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns)",
                Predicate: null),
            ["latency_p99"] = new(
                "PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ns)",
                Predicate: null),
            ["token_usage"] = new(
                "COALESCE(SUM(COALESCE(gen_ai_input_tokens, 0) + COALESCE(gen_ai_output_tokens, 0)), 0)",
                "gen_ai_input_tokens IS NOT NULL OR gen_ai_output_tokens IS NOT NULL"),
            ["cost"] = new(
                "COALESCE(SUM(gen_ai_cost_usd), 0)",
                "gen_ai_cost_usd IS NOT NULL")
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static IEnumerable<DerivedMetricDefinition> GetDefinitions()
    {
        return s_metrics.Values.OrderBy(static metric => metric.Name, StringComparer.Ordinal);
    }

    internal static DerivedMetricDefinition? Find(string name)
    {
        return s_metrics.TryGetValue(name, out var metric) ? metric : null;
    }

    internal static bool TryGetAnomalyMetric(
        string name,
        out AnomalyMetricSelection metric)
    {
        if (s_metrics.TryGetValue(name, out var definition))
        {
            metric = new AnomalyMetricSelection(definition.Expression, definition.Predicate);
            return true;
        }

        if (s_anomalyAliases.TryGetValue(name, out metric))
            return true;

        metric = default;
        return false;
    }

    internal static IEnumerable<string> GetAnomalyMetricNames()
    {
        foreach (var metricName in s_metrics.Keys)
            yield return metricName;

        foreach (var alias in s_anomalyAliases.Keys)
            yield return alias;
    }
}

internal sealed record DerivedMetricDefinition(
    string Name,
    string Type,
    string Description,
    string Unit,
    string Expression,
    string? Predicate);

internal readonly record struct AnomalyMetricSelection(
    string Expression,
    string? Predicate);
