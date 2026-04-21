using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using qyl.mcp.Formatting;
using qyl.mcp.Errors;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Auth;

/// <summary>
///     MCP tool that returns the authenticated user's identity including name, email, user ID, and roles.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class WhoamiTool(HttpClient client)
{
    /// <summary>
    ///     Retrieves the authenticated user's identity details from the qyl API.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown string containing the user's identity information.</returns>
    [QylCapability("server_introspection", QylCapabilityRole.FollowUp)]
    [QylCapability("project_and_access_management")]
    [McpServerTool(Name = "whoami", Title = "Who Am I",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns the authenticated user's identity including name, email, user ID, and assigned roles.")]
    public async Task<string> WhoamiAsync(CancellationToken ct = default)
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
