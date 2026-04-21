using System.ComponentModel;
using System.Net.Http.Json;
using qyl.mcp.Formatting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Metrics;

/// <summary>
///     Lists all available metrics, optionally filtered by project, showing name, type, unit, and description.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class ListMetricsTool(HttpClient client)
{
    [McpServerTool(
        Name = "list_metrics",
        Title = "List Metrics",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    [Description(
        "List all available metrics, optionally filtered by project. Shows name, type, unit, and description.")]
    public async Task<string> ListMetrics(
        [Description("Filter to a specific project slug")]
        string? projectSlug = null,
        CancellationToken ct = default)
    {
        var url = ANcpLua.Roslyn.Utilities.Web.QueryString.AppendPairs(
            "/api/v1/mcp/metrics", ("project", projectSlug));

        var metrics = await client.GetFromJsonAsync<IReadOnlyList<MetricInfoDto>>(url, ct).ConfigureAwait(false);

        if (metrics is not { Count: > 0 })
            return "No metrics found. Metrics are auto-discovered from incoming OTLP data.";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Available Metrics ({metrics.Count})");
        sb.AppendLine();
        sb.AppendLine("| Name | Type | Unit | Description |");
        sb.AppendLine("|------|------|------|-------------|");

        foreach (var m in metrics)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| `{m.Name}` | {m.Type} | {m.Unit ?? "\u2014"} | {m.Description ?? "\u2014"} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Next steps");
        sb.AppendLine("- Use `query_metrics(name: '<metric_name>')` to get time-series data");

        return sb.ToString();
    }
}
