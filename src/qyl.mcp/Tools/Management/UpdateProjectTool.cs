using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
public sealed class UpdateProjectTool(HttpClient client)
{
    [McpServerTool(
        Name = "update_project",
        Title = "Update Project",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true)]
    [Description("Update an existing project's name or description.")]
    public async Task<string> UpdateProjectAsync(
        [Description("Project slug to update")] string slug,
        [Description("New display name")] string? name = null,
        [Description("New description")] string? description = null,
        CancellationToken ct = default)
    {
        var body = new { name, description };
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/mcp/projects/{Uri.EscapeDataString(slug)}")
        {
            Content = JsonContent.Create(body)
        };

        var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.NotFound)
            throw new QylNotFoundException("Project");

        response.EnsureSuccessStatusCode();

        return ResponseFormatter.FormatSuccess($"Project `{slug}` updated.");
    }
}
