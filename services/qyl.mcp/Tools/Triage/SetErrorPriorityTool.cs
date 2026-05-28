using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class SetErrorPriorityTool(HttpClient client)
{
    private static readonly HashSet<string> s_validPriorities = ["P0", "P1", "P2", "P3", "P4"];

    [McpServerTool(
        Name = "set_error_priority",
        Title = "Set Error Priority",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true)]
    public async partial Task<string> SetErrorPriority(
        string issueId,
        string priority,
        CancellationToken ct = default)
    {
        if (!s_validPriorities.Contains(priority))
            throw new QylQueryException($"Invalid priority '{priority}'. Must be one of: P0, P1, P2, P3, P4.");

        using var response = await client.PostAsJsonAsync(
                $"/api/v1/mcp/errors/{Uri.EscapeDataString(issueId)}/priority",
                new { priority },
                ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Error issue");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess($"Priority set to `{priority}` on error issue `{issueId}`.");
    }
}
