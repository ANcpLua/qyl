using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

/// <summary>
///     MCP tool that sets the data retention period for a project.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Build)]
public sealed partial class ConfigureRetentionTool(HttpClient client)
{
    /// <summary>
    ///     Sets the data retention period in days for the specified project.
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
    public async partial Task<string> ConfigureRetentionAsync(
        string projectSlug,
        int retentionDays,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/mcp/projects/{Uri.EscapeDataString(projectSlug)}/retention");
        request.Content = JsonContent.Create(new { retention_days = retentionDays });

        var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Project");

        response.EnsureSuccessStatusCode();

        return ResponseFormatter.FormatSuccess(
            $"Retention for project `{projectSlug}` set to {retentionDays} days.");
    }
}
