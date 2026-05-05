using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
[QylSkill(QylSkillKind.Build)]
public sealed partial class ConfigureRetentionTool(HttpClient client)
{
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
