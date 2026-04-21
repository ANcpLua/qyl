using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using qyl.mcp.Formatting;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Management;

/// <summary>
///     MCP tool that creates a new team for organizing projects and members.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class CreateTeamTool(HttpClient client)
{
    /// <summary>
    ///     Creates a new team with the specified name, optional slug, and optional description.
    /// </summary>
    /// <param name="name">Display name for the team.</param>
    /// <param name="slug">Optional URL-safe slug identifier; auto-generated from name if omitted.</param>
    /// <param name="description">Optional team description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown string containing the created team details.</returns>
    [McpServerTool(
        Name = "create_team",
        Title = "Create Team",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false)]
    [Description("Create a new team for organizing projects and members.")]
    public async Task<string> CreateTeamAsync(
        [Description("Display name for the team")]
        string name,
        [Description("URL-safe slug identifier (auto-generated from name if omitted)")]
        string? slug = null,
        [Description("Optional team description")]
        string? description = null,
        CancellationToken ct = default)
    {
        var body = new CreateTeamRequestDto(name, slug, description);
        var response = await client.PostAsJsonAsync("/api/v1/mcp/teams", body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TeamDto>(ct).ConfigureAwait(false);

        return ResponseFormatter.FormatDetail(
            "Team Created",
            [
                ("Name", result!.Name),
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
