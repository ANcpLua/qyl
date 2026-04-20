namespace qyl.mcp.Tools.Metrics;

using System.ComponentModel;
using System.Net.Http.Json;
using Formatting;
using ModelContextProtocol.Server;

/// <summary>
///     Queries time-series data for a specific metric with optional filtering and time range.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class QueryMetricsTool(HttpClient client)
{
    [McpServerTool(
        Name = "query_metrics",
        Title = "Query Metrics",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = true)]
    [Description("Query time-series data for a specific metric with optional filtering and time range.")]
    public async Task<string> QueryMetrics(
        [Description("Metric name (e.g. 'http.server.request.duration')")]
        string name,
        [Description("Filter expression (e.g. 'service=api-gateway')")]
        string? filter = null,
        [Description("Start time in ISO 8601 format")]
        string? from = null,
        [Description("End time in ISO 8601 format")]
        string? to = null,
        [Description("Aggregation interval (e.g. '5m', '1h', '1d')")]
        string? interval = null,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/mcp/metrics/{Uri.EscapeDataString(name)}/query";
        var queryParts = new List<string>();

        if (filter is not null) queryParts.Add($"filter={Uri.EscapeDataString(filter)}");
        if (from is not null) queryParts.Add($"from={Uri.EscapeDataString(from)}");
        if (to is not null) queryParts.Add($"to={Uri.EscapeDataString(to)}");
        if (interval is not null) queryParts.Add($"interval={Uri.EscapeDataString(interval)}");

        if (queryParts.Count > 0)
            url += "?" + string.Join("&", queryParts);

        var series = await client.GetFromJsonAsync<TimeSeriesDto>(url, ct).ConfigureAwait(false);

        if (series is null or { Points.Count: 0 })
            return $"No data points found for metric `{name}`.";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Metric: `{series.Metric}`");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Points:** {series.Points.Count}");
        sb.AppendLine();
        sb.AppendLine("| Timestamp | Value |");
        sb.AppendLine("|-----------|-------|");

        foreach (var p in series.Points)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {p.Timestamp} | {p.Value:G6} |");
        }

        return sb.ToString();
    }
}
