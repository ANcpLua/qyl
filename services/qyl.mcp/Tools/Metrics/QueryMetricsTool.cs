using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Metrics;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class QueryMetricsTool(HttpClient client)
{
    [McpServerTool(
        Name = "query_metrics",
        Title = "Query Metrics",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> QueryMetrics(
        string name,
        string? filter = null,
        string? from = null,
        string? to = null,
        string? interval = null,
        CancellationToken ct = default)
    {
        var url = QueryString.AppendPairs(
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
