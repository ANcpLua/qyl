using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Auth;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class WhoamiTool(HttpClient client)
{
    [QylCapability("server_introspection", QylCapabilityRole.FollowUp)]
    [QylCapability("project_and_access_management")]
    [McpServerTool(Name = "whoami", Title = "Who Am I",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public async partial Task<string> WhoamiAsync(CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            "/api/v1/mcp/auth/whoami", ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Identity");

        response.EnsureSuccessStatusCode();

        var identity = await response.Content
            .ReadFromJsonAsync<WhoamiDto>(ct).ConfigureAwait(false);

        if (identity is null)
            throw new QylNotFoundException("Identity");

        var fields = new List<(string Label, string? Value)>
        {
            ("User ID", $"`{identity.UserId}`"),
            ("Name", identity.Name),
            ("Email", identity.Email),
            ("Roles", identity.Roles is { Count: > 0 }
                ? string.Join(", ", identity.Roles)
                : "none")
        };

        return ResponseFormatter.FormatDetail("Authenticated Identity", fields);
    }
}

internal sealed record WhoamiDto(
    [property: JsonPropertyName("user_id")]
    string UserId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("roles")] List<string>? Roles);
