using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

/// <summary>
/// MCP tool that sets the data retention period for a project.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
public sealed class ConfigureRetentionTool(HttpClient client)
{
    /// <summary>
    /// Sets the data retention period in days for the specified project.
    /// </summary>
    /// <param name="projectSlug">The project slug to configure.</param>
    /// <param name="retentionDays">Number of days to retain data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success message confirming the retention configuration.</returns>
    [McpServerTool(
        Name = "configure_retention",
        Title = "Configure Retention",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true)]
    [Description("Set data retention period for a project.")]
    public async Task<string> ConfigureRetentionAsync(
        [Description("Project slug")] string projectSlug,
        [Description("Number of days to retain data")]
        int retentionDays,
        CancellationToken ct = default)
    {
        var body = new { retention_days = retentionDays };
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/mcp/projects/{Uri.EscapeDataString(projectSlug)}/retention")
        {
            Content = JsonContent.Create(body)
        };

        var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Project");

        response.EnsureSuccessStatusCode();

        return ResponseFormatter.FormatSuccess(
            $"Retention for project `{projectSlug}` set to {retentionDays} days.");
    }
}
