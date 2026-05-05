using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Anomaly)]
public sealed partial class AnomalyTools(HttpClient client)
{
    [QylCapability("metrics_analysis", QylCapabilityRole.FollowUp)]
    [QylCapability("anomaly_detection")]
    [McpServerTool(Name = "qyl.detect_anomalies", Title = "Detect Anomalies",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> DetectAnomaliesAsync(
        string metric,
        int hours = 24,
        double sensitivity = 2.0,
        string? service = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url = QueryString.AppendPairs(
                $"/api/v1/analytics/anomaly/anomalies?metric={Uri.EscapeDataString(metric)}&hours={hours}&sensitivity={sensitivity}",
                ("service", service));

            var response = await client.GetFromJsonAsync<AnomalyDetectionResponse>(
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

            foreach (var anomaly in response.Anomalies)
            {
                sb.AppendLine(
                    $"| {anomaly.Bucket:yyyy-MM-dd HH:mm} | {anomaly.Value:F4} | {anomaly.ZScore:F2} | {anomaly.Direction} |");
            }

            return sb.ToString();
        });

    [QylCapability("metrics_analysis", QylCapabilityRole.FollowUp)]
    [QylCapability("anomaly_detection")]
    [McpServerTool(Name = "qyl.get_metric_baseline", Title = "Get Metric Baseline",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public partial Task<string> GetMetricBaselineAsync(
        string metric,
        int hours = 24,
        string? service = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url = QueryString.AppendPairs(
                $"/api/v1/analytics/anomaly/baseline?metric={Uri.EscapeDataString(metric)}&hours={hours}",
                ("service", service));

            var response = await client.GetFromJsonAsync<BaselineResponse>(
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

    [QylCapability("anomaly_detection", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.compare_periods", Title = "Compare Periods",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> ComparePeriodsAsync(
        string metric,
        string period1Start,
        string period1End,
        string period2Start,
        string period2End,
        string? service = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url = QueryString.AppendPairs(
                $"/api/v1/analytics/anomaly/compare?metric={Uri.EscapeDataString(metric)}",
                ("period1Start", period1Start), ("period1End", period1End),
                ("period2Start", period2Start), ("period2End", period2End),
                ("service", service));

            var response = await client.GetFromJsonAsync<PeriodComparisonResponse>(
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
    [property: JsonPropertyName("sensitivity")]
    double Sensitivity,
    [property: JsonPropertyName("mean")] double Mean,
    [property: JsonPropertyName("std_dev")]
    double StdDev,
    [property: JsonPropertyName("anomalies")]
    List<AnomalyPointDto>? Anomalies);

internal sealed record AnomalyPointDto(
    [property: JsonPropertyName("bucket")] DateTime Bucket,
    [property: JsonPropertyName("value")] double Value,
    [property: JsonPropertyName("z_score")]
    double ZScore,
    [property: JsonPropertyName("direction")]
    string Direction);

internal sealed record BaselineResponse(
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("hours")] int Hours,
    [property: JsonPropertyName("mean")] double Mean,
    [property: JsonPropertyName("std_dev")]
    double StdDev,
    [property: JsonPropertyName("p50")] double P50,
    [property: JsonPropertyName("p95")] double P95,
    [property: JsonPropertyName("p99")] double P99,
    [property: JsonPropertyName("sample_count")]
    long SampleCount);

internal sealed record PeriodComparisonResponse(
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("period1")]
    BaselineResponse Period1,
    [property: JsonPropertyName("period2")]
    BaselineResponse Period2,
    [property: JsonPropertyName("mean_delta")]
    double MeanDelta,
    [property: JsonPropertyName("mean_delta_percent")]
    double MeanDeltaPercent);

#endregion

[JsonSerializable(typeof(AnomalyDetectionResponse))]
[JsonSerializable(typeof(BaselineResponse))]
[JsonSerializable(typeof(PeriodComparisonResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class AnomalyJsonContext : JsonSerializerContext;
