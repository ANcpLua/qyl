using System.ComponentModel;
using System.Net.Http.Json;
using qyl.mcp.Formatting;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Management;

/// <summary>
///     MCP tool that creates a new observability project.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Build)]
public sealed class CreateProjectTool(HttpClient client)
{
    /// <summary>
    ///     Creates a new observability project with the specified name, slug, and optional description.
    /// </summary>
    /// <param name="name">Display name for the project.</param>
    /// <param name="slug">URL-safe slug identifier.</param>
    /// <param name="description">Optional project description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown string containing the created project details.</returns>
    [McpServerTool(
        Name = "create_project",
        Title = "Create Project",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false)]
    [Description("Create a new observability project.")]
    public async Task<string> CreateProjectAsync(
        [Description("Display name for the project")]
        string name,
        [Description("URL-safe slug identifier")]
        string slug,
        [Description("Optional project description")]
        string? description = null,
        CancellationToken ct = default)
    {
        var body = new { name, slug, description };
        var response = await client.PostAsJsonAsync("/api/v1/mcp/projects", body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectInfoDto>(ct).ConfigureAwait(false);

        return ResponseFormatter.FormatDetail(
            "Project Created",
            [
                ("Name", result!.Name),
                ("Slug", $"`{result.Slug}`"),
                ("Description", result.Description),
                ("Retention Days", result.RetentionDays?.ToString())
            ]);
    }
}
