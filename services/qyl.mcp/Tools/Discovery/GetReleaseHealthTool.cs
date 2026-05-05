using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Discovery;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class GetReleaseHealthTool(HttpClient client)
{
    [QylCapability("service_discovery", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_release_health", Title = "Get Release Health",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public async partial Task<string> GetReleaseHealthAsync(
        string version,
        string? projectSlug = null,
        CancellationToken ct = default)
    {
        var url = QueryString.AppendPairs(
            $"/api/v1/mcp/releases/{Uri.EscapeDataString(version)}/health",
            ("projectSlug", projectSlug));

        var response = await client.GetAsync(url, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Release");

        response.EnsureSuccessStatusCode();

        var health = await response.Content
            .ReadFromJsonAsync<ReleaseHealthDto>(ct).ConfigureAwait(false);

        if (health is null)
            throw new QylNotFoundException("Release");

        return ResponseFormatter.FormatDetail(
            $"Release Health: {health.Version}",
            [
                ("Version", $"`{health.Version}`"),
                ("Crash-free rate", $"{health.CrashFreeRate:F2}%"),
                ("Error count", health.ErrorCount.ToString()),
                ("Sessions", health.SessionCount.ToString()),
                ("Adoption %", $"{health.AdoptionPercent:F1}%"),
                ("First seen", health.FirstSeen),
                ("Last seen", health.LastSeen)
            ]);
    }
}

internal sealed record ReleaseHealthDto(
    [property: JsonPropertyName("version")]
    string Version,
    [property: JsonPropertyName("crash_free_rate")]
    double CrashFreeRate,
    [property: JsonPropertyName("error_count")]
    long ErrorCount,
    [property: JsonPropertyName("session_count")]
    long SessionCount,
    [property: JsonPropertyName("adoption_percent")]
    double AdoptionPercent,
    [property: JsonPropertyName("first_seen")]
    string FirstSeen,
    [property: JsonPropertyName("last_seen")]
    string LastSeen);
