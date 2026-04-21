using System.ComponentModel;
using System.Net.Http.Json;
using qyl.mcp.Formatting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Metrics;

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
        OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
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
        var url = ANcpLua.Roslyn.Utilities.Web.QueryString.AppendPairs(
            $"/api/v1/mcp/metrics/{Uri.EscapeDataString(name)}/query",
            ("filter", filter), ("from", from), ("to", to), ("interval", interval));

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
