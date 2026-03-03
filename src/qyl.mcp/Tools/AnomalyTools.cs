using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for anomaly detection analytics.
///     Provides z-score based anomaly detection, baseline statistics, and period comparison.
/// </summary>
[McpServerToolType]
public sealed class AnomalyTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.detect_anomalies", Title = "Detect Anomalies",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Detect anomalous metric spikes or drops using z-score analysis.

                 Compares each time bucket against the baseline mean/stddev
                 to identify statistically significant deviations.

                 Supported metrics:
                 - error_rate, latency_p50, latency_p95, latency_p99
                 - request_count, token_usage, cost

                 Example queries:
                 - Error rate anomalies: detect_anomalies(metric="error_rate")
                 - Latency spikes: detect_anomalies(metric="latency_p99", sensitivity=1.5)
                 - Service-specific: detect_anomalies(metric="error_rate", service="api-gateway")

                 Returns: Baseline stats and list of anomalous time buckets with z-scores
                 """)]
    public Task<string> DetectAnomaliesAsync(
        [Description("Metric to analyze: 'error_rate', 'latency_p50', 'latency_p95', 'latency_p99', 'request_count', 'token_usage', 'cost'")] string metric,
        [Description("Time window in hours (default: 24)")] int hours = 24,
        [Description("Z-score threshold for anomaly detection (default: 2.0)")] double sensitivity = 2.0,
        [Description("Filter by service name")] string? service = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            string url = $"/api/v1/analytics/anomaly/anomalies?metric={Uri.EscapeDataString(metric)}&hours={hours}&sensitivity={sensitivity}";
            if (!string.IsNullOrEmpty(service))
                url += $"&service={Uri.EscapeDataString(service)}";

            AnomalyDetectionResponse? response = await client.GetFromJsonAsync<AnomalyDetectionResponse>(
                url, AnomalyJsonContext.Default.AnomalyDetectionResponse).ConfigureAwait(false);

            if (response is null)
                return "No anomaly detection data available.";

            StringBuilder sb = new();
            sb.AppendLine($"# Anomaly Detection — {response.Metric}");
            sb.AppendLine($"Window: {response.Hours}h, Sensitivity: {response.Sensitivity} sigma");
            sb.AppendLine();
            sb.AppendLine("## Baseline");
            sb.AppendLine($"- **Mean:** {response.Mean:F4}");
            sb.AppendLine($"- **Std Dev:** {response.StdDev:F4}");
            sb.AppendLine();

            if (response.Anomalies is null || response.Anomalies.Count is 0)
            {
                sb.AppendLine("No anomalies detected in the specified time window.");
                return sb.ToString();
            }

            sb.AppendLine($"## Anomalies ({response.Anomalies.Count} detected)");
            sb.AppendLine();
            sb.AppendLine("| Time | Value | Z-Score | Direction |");
            sb.AppendLine("|------|-------|---------|-----------|");

            foreach (AnomalyPointDto anomaly in response.Anomalies)
            {
                sb.AppendLine(
                    $"| {anomaly.Bucket:yyyy-MM-dd HH:mm} | {anomaly.Value:F4} | {anomaly.ZScore:F2} | {anomaly.Direction} |");
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.get_metric_baseline", Title = "Get Metric Baseline",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Get baseline statistics (mean, percentiles) for a metric.

                 Computes statistical summary over the time window.
                 Useful for understanding normal operating ranges.

                 Supported metrics:
                 - error_rate, latency_p50, latency_p95, latency_p99
                 - request_count, token_usage, cost

                 Example queries:
                 - Latency baseline: get_metric_baseline(metric="latency_p95")
                 - Error rate stats: get_metric_baseline(metric="error_rate", hours=168)

                 Returns: Statistical summary with mean, stddev, and percentiles
                 """)]
    public Task<string> GetMetricBaselineAsync(
        [Description("Metric to analyze: 'error_rate', 'latency_p50', 'latency_p95', 'latency_p99', 'request_count', 'token_usage', 'cost'")] string metric,
        [Description("Time window in hours (default: 24)")] int hours = 24,
        [Description("Filter by service name")] string? service = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            string url = $"/api/v1/analytics/anomaly/baseline?metric={Uri.EscapeDataString(metric)}&hours={hours}";
            if (!string.IsNullOrEmpty(service))
                url += $"&service={Uri.EscapeDataString(service)}";

            BaselineResponse? response = await client.GetFromJsonAsync<BaselineResponse>(
                url, AnomalyJsonContext.Default.BaselineResponse).ConfigureAwait(false);

            if (response is null)
                return "No baseline data available.";

            StringBuilder sb = new();
            sb.AppendLine($"# Metric Baseline — {response.Metric}");
            sb.AppendLine($"Window: {response.Hours}h, Samples: {response.SampleCount:N0}");
            sb.AppendLine();
            sb.AppendLine($"- **Mean:** {response.Mean:F4}");
            sb.AppendLine($"- **Std Dev:** {response.StdDev:F4}");
            sb.AppendLine($"- **P50:** {response.P50:F4}");
            sb.AppendLine($"- **P95:** {response.P95:F4}");
            sb.AppendLine($"- **P99:** {response.P99:F4}");

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.compare_periods", Title = "Compare Periods",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Compare metrics between two time periods (e.g., before/after deploy).

                 Computes baseline stats for each period and shows the delta.
                 Useful for measuring the impact of deployments, config changes, etc.

                 Supported metrics:
                 - error_rate, latency_p50, latency_p95, latency_p99
                 - request_count, token_usage, cost

                 Example queries:
                 - Before/after deploy: compare_periods(metric="error_rate",
                     period1Start="2024-01-01T00:00:00Z", period1End="2024-01-01T12:00:00Z",
                     period2Start="2024-01-01T12:00:00Z", period2End="2024-01-02T00:00:00Z")

                 Returns: Side-by-side comparison with delta and percentage change
                 """)]
    public Task<string> ComparePeriodsAsync(
        [Description("Metric to compare: 'error_rate', 'latency_p50', 'latency_p95', 'latency_p99', 'request_count', 'token_usage', 'cost'")] string metric,
        [Description("Start of first period (ISO 8601, e.g., '2024-01-01T00:00:00Z')")] string period1Start,
        [Description("End of first period (ISO 8601)")] string period1End,
        [Description("Start of second period (ISO 8601)")] string period2Start,
        [Description("End of second period (ISO 8601)")] string period2End,
        [Description("Filter by service name")] string? service = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            string url = $"/api/v1/analytics/anomaly/compare?metric={Uri.EscapeDataString(metric)}"
                         + $"&period1Start={Uri.EscapeDataString(period1Start)}"
                         + $"&period1End={Uri.EscapeDataString(period1End)}"
                         + $"&period2Start={Uri.EscapeDataString(period2Start)}"
                         + $"&period2End={Uri.EscapeDataString(period2End)}";
            if (!string.IsNullOrEmpty(service))
                url += $"&service={Uri.EscapeDataString(service)}";

            PeriodComparisonResponse? response = await client.GetFromJsonAsync<PeriodComparisonResponse>(
                url, AnomalyJsonContext.Default.PeriodComparisonResponse).ConfigureAwait(false);

            if (response is null)
                return "No comparison data available.";

            StringBuilder sb = new();
            sb.AppendLine($"# Period Comparison — {response.Metric}");
            sb.AppendLine();
            sb.AppendLine("| Stat | Period 1 | Period 2 | Delta | Change |");
            sb.AppendLine("|------|----------|----------|-------|--------|");
            sb.AppendLine(
                $"| Mean | {response.Period1.Mean:F4} | {response.Period2.Mean:F4} | {response.MeanDelta:+0.0000;-0.0000;0.0000} | {response.MeanDeltaPercent:+0.0;-0.0;0.0}% |");
            sb.AppendLine(
                $"| Std Dev | {response.Period1.StdDev:F4} | {response.Period2.StdDev:F4} | — | — |");
            sb.AppendLine(
                $"| P50 | {response.Period1.P50:F4} | {response.Period2.P50:F4} | — | — |");
            sb.AppendLine(
                $"| P95 | {response.Period1.P95:F4} | {response.Period2.P95:F4} | — | — |");
            sb.AppendLine(
                $"| P99 | {response.Period1.P99:F4} | {response.Period2.P99:F4} | — | — |");
            sb.AppendLine(
                $"| Samples | {response.Period1.SampleCount:N0} | {response.Period2.SampleCount:N0} | — | — |");

            return sb.ToString();
        });
}

#region DTOs

internal sealed record AnomalyDetectionResponse(
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("hours")] int Hours,
    [property: JsonPropertyName("sensitivity")] double Sensitivity,
    [property: JsonPropertyName("mean")] double Mean,
    [property: JsonPropertyName("std_dev")] double StdDev,
    [property: JsonPropertyName("anomalies")] List<AnomalyPointDto>? Anomalies);

internal sealed record AnomalyPointDto(
    [property: JsonPropertyName("bucket")] DateTime Bucket,
    [property: JsonPropertyName("value")] double Value,
    [property: JsonPropertyName("z_score")] double ZScore,
    [property: JsonPropertyName("direction")] string Direction);

internal sealed record BaselineResponse(
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("hours")] int Hours,
    [property: JsonPropertyName("mean")] double Mean,
    [property: JsonPropertyName("std_dev")] double StdDev,
    [property: JsonPropertyName("p50")] double P50,
    [property: JsonPropertyName("p95")] double P95,
    [property: JsonPropertyName("p99")] double P99,
    [property: JsonPropertyName("sample_count")] long SampleCount);

internal sealed record PeriodComparisonResponse(
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("period1")] BaselineResponse Period1,
    [property: JsonPropertyName("period2")] BaselineResponse Period2,
    [property: JsonPropertyName("mean_delta")] double MeanDelta,
    [property: JsonPropertyName("mean_delta_percent")] double MeanDeltaPercent);

#endregion

[JsonSerializable(typeof(AnomalyDetectionResponse))]
[JsonSerializable(typeof(BaselineResponse))]
[JsonSerializable(typeof(PeriodComparisonResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class AnomalyJsonContext : JsonSerializerContext;
