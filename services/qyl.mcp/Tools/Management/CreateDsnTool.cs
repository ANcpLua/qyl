using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class CreateDsnTool(HttpClient client)
{
    [McpServerTool(
        Name = "create_dsn",
        Title = "Create DSN",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false)]
    public async partial Task<string> CreateDsnAsync(
        string projectSlug,
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
        result = Guard.NotNull(result);

        return ResponseFormatter.FormatDetail(
            "DSN Created",
            [
                ("DSN", $"`{result.Dsn}` (save this — it may not be shown again)"),
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
