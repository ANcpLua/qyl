using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class CreateTeamTool(HttpClient client)
{
    [McpServerTool(
        Name = "create_team",
        Title = "Create Team",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false)]
    public async partial Task<string> CreateTeamAsync(
        string name,
        string? slug = null,
        string? description = null,
        CancellationToken ct = default)
    {
        var body = new CreateTeamRequestDto(name, slug, description);
        var response = await client.PostAsJsonAsync("/api/v1/mcp/teams", body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TeamDto>(ct).ConfigureAwait(false);
        result = Guard.NotNull(result);

        return ResponseFormatter.FormatDetail(
            "Team Created",
            [
                ("Name", result.Name),
                ("Slug", $"`{result.Slug}`"),
                ("Description", result.Description)
            ]);
    }
}

internal sealed record CreateTeamRequestDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("description")]
    string? Description = null);

internal sealed record TeamDto(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")]
    string? Description = null,
    [property: JsonPropertyName("member_count")]
    int? MemberCount = null,
    [property: JsonPropertyName("created_at")]
    string? CreatedAt = null);
