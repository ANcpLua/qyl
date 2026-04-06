using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
public sealed class CreateProjectTool(HttpClient client)
{
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

        if (result is null)
            return "Failed to parse project response.";

        return ResponseFormatter.FormatDetail(
            "Project Created",
            [
                ("Name", result.Name),
                ("Slug", $"`{result.Slug}`"),
                ("Description", result.Description),
                ("Retention Days", result.RetentionDays?.ToString())
            ]);
    }
}
