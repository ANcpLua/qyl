using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Analysis;

[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
public sealed partial class SuggestFixTool(HttpClient client)
{
    [McpServerTool(
        Name = "suggest_fix",
        Title = "Suggest Fix",
        ReadOnly = false,
        Destructive = false,
        OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> SuggestFixAsync(
        string traceId,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            $"/api/v1/mcp/traces/{Uri.EscapeDataString(traceId)}", ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Trace");

        response.EnsureSuccessStatusCode();
        var spans = await response.Content.ReadFromJsonAsync<IReadOnlyList<SpanDetailDto>>(ct).ConfigureAwait(false);

        var errorSpans = spans!.Where(s => s.Status is "error").ToList();

        if (errorMessage is not null)
        {
            errorSpans = errorSpans
                .Where(s => s.Attributes?.Values.Any(v =>
                    v.ContainsIgnoreCase(errorMessage)) is true)
                .ToList();
        }

        if (errorSpans.Count is 0)
            return ResponseFormatter.FormatSuccess("No error spans found in this trace.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Fix Suggestions for Trace `{traceId}`");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Found **{errorSpans.Count}** error span(s):");
        sb.AppendLine();

        foreach (var span in errorSpans)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### `{span.SpanName}` ({span.ServiceName})");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Duration:** {span.DurationMs:N0}ms");

            if (span.Attributes is not null)
            {
                foreach (var (key, value) in span.Attributes)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"- **{key}:** {value}");
            }

            sb.AppendLine();
            sb.AppendLine("**Suggested actions:**");
            sb.AppendLine(BuildSuggestion(span));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildSuggestion(SpanDetailDto span)
    {
        var sb = new StringBuilder();

        switch (span.SpanName)
        {
            case var name when name.ContainsIgnoreCase("http"):
                sb.AppendLine("- Check HTTP endpoint availability and response codes");
                sb.AppendLine("- Verify network connectivity and DNS resolution");
                sb.AppendLine("- Review request timeout configuration");
                break;
            case var name when name.ContainsIgnoreCase("db")
                               || name.ContainsIgnoreCase("query")
                               || name.ContainsIgnoreCase("sql"):
                sb.AppendLine("- Check database connection pool and availability");
                sb.AppendLine("- Review query for performance issues or deadlocks");
                sb.AppendLine("- Verify database credentials and permissions");
                break;
            case var name when name.ContainsIgnoreCase("auth"):
                sb.AppendLine("- Verify authentication credentials and token expiry");
                sb.AppendLine("- Check identity provider availability");
                sb.AppendLine("- Review permission grants and role assignments");
                break;
            default:
                sb.AppendLine("- Review span attributes for error details");
                sb.AppendLine("- Check service logs for additional context");
                sb.AppendLine("- Verify upstream dependencies are healthy");
                break;
        }

        if (span.DurationMs > 5000)
            sb.AppendLine("- **High latency detected** — consider adding timeouts or circuit breakers");

        return sb.ToString();
    }
}
