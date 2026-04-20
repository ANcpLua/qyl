namespace qyl.mcp.Tools.Management;

using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Formatting;
using mcp.Errors;
using ModelContextProtocol.Server;

/// <summary>
///     MCP tool that creates a new DSN (Data Source Name) for a project.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class CreateDsnTool(HttpClient client)
{
    /// <summary>
    ///     Creates a new DSN for a project and returns the full DSN value.
    /// </summary>
    /// <param name="projectSlug">The project slug to create the DSN for.</param>
    /// <param name="label">Optional human-readable label for the DSN.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown string containing the created DSN details.</returns>
    [McpServerTool(
        Name = "create_dsn",
        Title = "Create DSN",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false)]
    [Description(
        "Create a new DSN (Data Source Name) for a project. Returns the full DSN value — save it, it may not be shown again.")]
    public async Task<string> CreateDsnAsync(
        [Description("Project slug to create the DSN for")]
        string projectSlug,
        [Description("Optional human-readable label for the DSN")]
        string? label = null,
        CancellationToken ct = default)
    {
        var body = new CreateDsnRequestDto(label);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/mcp/projects/{Uri.EscapeDataString(projectSlug)}/dsns", body, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Project");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DsnDto>(ct).ConfigureAwait(false);

        return ResponseFormatter.FormatDetail(
            "DSN Created",
            [
                ("DSN", $"`{result!.Dsn}` (save this — it may not be shown again)"),
                ("Label", result.Label),
                ("Created", result.DateCreated)
            ]);
    }
}

internal sealed record CreateDsnRequestDto(
    [property: JsonPropertyName("label")] string? Label);

internal sealed record DsnDto(
    [property: JsonPropertyName("dsn")] string Dsn,
    [property: JsonPropertyName("label")] string? Label = null,
    [property: JsonPropertyName("date_created")]
    string? DateCreated = null);
