using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using qyl.mcp.Formatting;
using qyl.mcp.Errors;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Management;

/// <summary>
///     MCP tool that updates an existing project's name or description.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Build)]
public sealed class UpdateProjectTool(HttpClient client)
{
    /// <summary>
    ///     Updates the name and/or description of an existing project.
    /// </summary>
    /// <param name="slug">The project slug to update.</param>
    /// <param name="name">Optional new display name.</param>
    /// <param name="description">Optional new description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success message confirming the project was updated.</returns>
    [McpServerTool(
        Name = "update_project",
        Title = "Update Project",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true)]
    [Description("Update an existing project's name or description.")]
    public async Task<string> UpdateProjectAsync(
        [Description("Project slug to update")]
        string slug,
        [Description("New display name")] string? name = null,
        [Description("New description")] string? description = null,
        CancellationToken ct = default)
    {
        var body = new { name, description };
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/mcp/projects/{Uri.EscapeDataString(slug)}") { Content = JsonContent.Create(body) };

        var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Project");

        response.EnsureSuccessStatusCode();

        return ResponseFormatter.FormatSuccess($"Project `{slug}` updated.");
    }
}
