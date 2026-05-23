using System.Net;
using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;
using qyl.mcp.Tools;

namespace qyl.mcp.Tools.Metrics;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class ListMetricsTool(HttpClient client)
{
    [McpServerTool(
        Name = "list_metrics",
        Title = "List Metrics",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> ListMetrics(
        string? projectSlug = null,
        string? serviceName = null,
        string? namePattern = null,
        int? limit = null,
        int? serviceLimit = null,
        string? cursor = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(projectSlug))
            return "Project-scoped metrics are not available yet. Omit `projectSlug` to list collector-wide derived metrics.";

        var url = QueryString.AppendPairs(
            "/api/v1/metrics",
            ("serviceName", serviceName),
            ("namePattern", namePattern),
            ("limit", limit?.ToString(CultureInfo.InvariantCulture)),
            ("serviceLimit", serviceLimit?.ToString(CultureInfo.InvariantCulture)),
            ("cursor", cursor));

        using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return await FormatListFailureAsync(response, ct).ConfigureAwait(false);

        var page = await response.Content.ReadFromJsonAsync(
            CollectorDtosJsonContext.Default.PublicMetricMetadataPageDto,
            ct).ConfigureAwait(false);
        var metrics = page?.Items;

        if (metrics is not { Count: > 0 })
            return "No derived metrics found. qyl currently exposes collector-wide metrics derived from stored spans.";

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Available Metrics ({metrics.Count})");
        if (page is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Has more:** {FormatBoolean(page.HasMore)}");
            if (!string.IsNullOrWhiteSpace(page.NextCursor))
                sb.AppendLine(CultureInfo.InvariantCulture, $"**Next cursor:** `{page.NextCursor}`");
            if (!string.IsNullOrWhiteSpace(page.PrevCursor))
                sb.AppendLine(CultureInfo.InvariantCulture, $"**Previous cursor:** `{page.PrevCursor}`");
        }

        sb.AppendLine();
        sb.AppendLine("| Name | Type | Unit | Labels | Services | Description |");
        sb.AppendLine("|------|------|------|--------|----------|-------------|");

        foreach (var m in metrics)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| `{m.Name}` | {m.Type} | {m.Unit ?? "n/a"} | {FormatLabels(m.LabelKeys)} | {FormatServices(m)} | {m.Description ?? "n/a"} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Next steps");
        sb.AppendLine("- Use `query_metrics(name: '<metric_name>')` to get time-series data");

        return sb.ToString();
    }

    private static string FormatBoolean(bool value)
    {
        return value ? "yes" : "no";
    }

    private static string FormatLabels(IReadOnlyList<string>? labelKeys)
    {
        if (labelKeys is not { Count: > 0 })
            return "n/a";

        var sb = new StringBuilder();
        for (var i = 0; i < labelKeys.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");

            sb.Append('`');
            sb.Append(labelKeys[i]);
            sb.Append('`');
        }

        return sb.ToString();
    }

    private static string FormatServices(MetricInfoDto metric)
    {
        var services = metric.Services;
        if (services is not { Count: > 0 })
            return metric.ServicesTruncated ? FormatServiceTruncation(metric.ServiceLimit) : "n/a";

        var sb = new StringBuilder();
        for (var i = 0; i < services.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");

            sb.Append('`');
            sb.Append(services[i]);
            sb.Append('`');
        }

        if (metric.ServicesTruncated)
        {
            sb.Append(" ... ");
            sb.Append(FormatServiceTruncation(metric.ServiceLimit));
        }

        return sb.ToString();
    }

    private static string FormatServiceTruncation(int? serviceLimit)
    {
        return serviceLimit is { } limit
            ? $"truncated at {limit.ToString(CultureInfo.InvariantCulture)}"
            : "truncated";
    }

    private static async Task<string> FormatListFailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var message = await CollectorHelper.ReadCollectorErrorMessageAsync(response, ct).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => $"List metrics rejected: {message}",
            _ => $"List metrics failed ({(int)response.StatusCode} {response.StatusCode}): {message}"
        };
    }
}
