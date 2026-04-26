using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Traces;

/// <summary>
///     Retrieves CPU/memory profile data for a span, including thread info, hot functions, and stack frame summary.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class GetProfileTool(HttpClient client)
{
    [McpServerTool(Name = "get_profile", Title = "Get Span Profile",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Get CPU/memory profile data for a span. Returns thread info, hot functions, and stack frame summary.")]
    public async Task<string> GetProfileAsync(
        [Description("The span ID to fetch profile data for")]
        string spanId,
        CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            $"/api/v1/mcp/spans/{Uri.EscapeDataString(spanId)}/profile", ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Span profile");

        response.EnsureSuccessStatusCode();

        var profile = await response.Content
            .ReadFromJsonAsync<ProfileDto>(ct).ConfigureAwait(false);

        if (profile is null)
            throw new QylNotFoundException("Span profile");

        var header = ResponseFormatter.FormatDetail($"Profile for span `{spanId}`",
        [
            ("Threads", profile.ThreadCount.ToString(CultureInfo.InvariantCulture)),
            ("Total Samples", profile.TotalSamples.ToString(CultureInfo.InvariantCulture))
        ]);

        var sb = new StringBuilder(header);

        if (profile.TopFunctions is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Hot Functions");
            sb.AppendLine();
            sb.AppendLine("| # | Function | Module | Self Time | Self % |");
            sb.AppendLine("|---|----------|--------|-----------|--------|");

            var rank = 0;
            foreach (var fn in profile.TopFunctions)
            {
                rank++;
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"| {rank} | `{fn.Name}` | {fn.Module} | {fn.SelfTimeMs:F1}ms | {fn.SelfTimePercent:F1}% |");
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("No hot functions recorded for this span.");
        }

        sb.AppendLine();
        sb.AppendLine("## Next steps");
        sb.AppendLine($"- Use `get_span(spanId: '{spanId}')` for full span attributes");
        sb.AppendLine("- Use `search_traces(query: '<service or operation>')` to find related traces");

        return sb.ToString();
    }
}

internal sealed record ProfileDto(
    [property: JsonPropertyName("thread_count")]
    int ThreadCount,
    [property: JsonPropertyName("total_samples")]
    int TotalSamples,
    [property: JsonPropertyName("top_functions")]
    List<ProfileFunctionDto>? TopFunctions = null);

internal sealed record ProfileFunctionDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("module")] string Module,
    [property: JsonPropertyName("self_time_ms")]
    double SelfTimeMs,
    [property: JsonPropertyName("self_time_percent")]
    double SelfTimePercent);
