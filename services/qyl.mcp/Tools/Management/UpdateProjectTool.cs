using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
[QylSkill(QylSkillKind.Build)]
public sealed partial class UpdateProjectTool(HttpClient client)
{
    [McpServerTool(
        Name = "update_project",
        Title = "Update Project",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true)]
    public async partial Task<string> UpdateProjectAsync(
        string slug,
        string? name = null,
        string? description = null,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/mcp/projects/{Uri.EscapeDataString(slug)}");
        request.Content = JsonContent.Create(new { name, description });

        var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Project");

        response.EnsureSuccessStatusCode();

        return ResponseFormatter.FormatSuccess($"Project `{slug}` updated.");
    }
}
