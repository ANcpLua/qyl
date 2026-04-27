using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

/// <summary>
///     MCP tool that lists all DSNs for a project.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class ListDsnsTool(HttpClient client)
{
    /// <summary>
    ///     Lists all DSNs configured for a project, used by client SDKs to send telemetry data.
    /// </summary>
    /// <param name="projectSlug">The project slug to list DSNs for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown list of DSNs with labels and creation dates.</returns>
    [McpServerTool(
        Name = "list_dsns",
        Title = "List DSNs",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    public async partial Task<string> ListDsnsAsync(
        string projectSlug,
        CancellationToken ct = default)
    {
        var response = await client.GetAsync(
            $"/api/v1/mcp/projects/{Uri.EscapeDataString(projectSlug)}/dsns", ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Project");

        response.EnsureSuccessStatusCode();

        var dsns = await response.Content.ReadFromJsonAsync<List<DsnDto>>(ct).ConfigureAwait(false);

        if (dsns is null or [])
            return ResponseFormatter.FormatSuccess($"No DSNs found for project `{projectSlug}`.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# DSNs for `{projectSlug}` ({dsns.Count})");
        sb.AppendLine();

        foreach (var dsn in dsns)
        {
            var label = dsn.Label is not null ? $" -- {dsn.Label}" : "";
            var created = dsn.DateCreated is not null ? $" (created {dsn.DateCreated})" : "";
            sb.AppendLine(CultureInfo.InvariantCulture, $"- `{dsn.Dsn}`{label}{created}");
        }

        sb.AppendLine();
        sb.AppendLine("## Next steps");
        sb.AppendLine($"- Use `create_dsn(projectSlug: '{projectSlug}')` to create a new DSN");

        return sb.ToString();
    }
}
