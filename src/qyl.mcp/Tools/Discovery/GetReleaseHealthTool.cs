namespace qyl.mcp.Tools.Discovery;

using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Formatting;
using mcp.Errors;
using ModelContextProtocol.Server;

/// <summary>
///     MCP tool that retrieves health and adoption metrics for a specific release.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class GetReleaseHealthTool(HttpClient client)
{
    /// <summary>
    ///     Retrieves crash-free rate, error count, session count, and adoption percentage for a release.
    /// </summary>
    /// <param name="version">The release version to inspect (e.g. '1.2.3' or 'abc123').</param>
    /// <param name="projectSlug">Optional project slug filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown string containing release health metrics.</returns>
    [QylCapability("service_discovery", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "get_release_health", Title = "Get Release Health",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Get health and adoption metrics for a specific release — crash-free rate, error count, session count, and adoption percentage.")]
    public async Task<string> GetReleaseHealthAsync(
        [Description("The release version to inspect (e.g. '1.2.3' or 'abc123')")]
        string version,
        [Description("Filter by project slug")]
        string? projectSlug = null,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/mcp/releases/{Uri.EscapeDataString(version)}/health";
        if (projectSlug is not null)
            url += $"?projectSlug={Uri.EscapeDataString(projectSlug)}";

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
