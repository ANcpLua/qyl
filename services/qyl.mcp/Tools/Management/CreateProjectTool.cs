using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
[QylSkill(QylSkillKind.Build)]
public sealed partial class CreateProjectTool(HttpClient client)
{
    [McpServerTool(
        Name = "create_project",
        Title = "Create Project",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false)]
    public async partial Task<string> CreateProjectAsync(
        string name,
        string slug,
        string? description = null,
        CancellationToken ct = default)
    {
        var body = new { name, slug, description };
        var response = await client.PostAsJsonAsync("/api/v1/mcp/projects", body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectInfoDto>(ct).ConfigureAwait(false);
        result = Guard.NotNull(result);

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
