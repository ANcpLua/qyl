using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;


/// <summary>
/// Sets the priority level (P0-P4) on an error issue for triage.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class SetErrorPriorityTool(HttpClient client)
{
    private static readonly HashSet<string> ValidPriorities = ["P0", "P1", "P2", "P3", "P4"];

    [McpServerTool(
        Name = "set_error_priority",
        Title = "Set Error Priority",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Set the priority level on an error issue for triage. Valid priorities: P0 (critical), P1 (high), P2 (medium), P3 (low), P4 (info).")]
    /// <summary>
    /// Validates and assigns a priority level to the specified error issue.
    /// </summary>
    /// <param name="issueId">The error issue ID to set priority on.</param>
    /// <param name="priority">Priority level: P0 (critical), P1 (high), P2 (medium), P3 (low), P4 (info).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success confirmation message with the assigned priority.</returns>
    public async Task<string> SetErrorPriority(
        [Description("Error issue ID to set priority on")] string issueId,
        [Description("Priority level: P0 (critical), P1 (high), P2 (medium), P3 (low), P4 (info)")]
        string priority,
        CancellationToken ct = default)
    {
        if (!ValidPriorities.Contains(priority))
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
