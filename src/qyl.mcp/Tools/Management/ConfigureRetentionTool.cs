using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
public sealed class ConfigureRetentionTool(HttpClient client)
{
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
