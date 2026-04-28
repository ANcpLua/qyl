using System.Net;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying and triggering Loom triage assessments.
///     Communicates with qyl.collector via HTTP REST API.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Loom)]
internal sealed partial class TriageTools(HttpClient http)
{
    [QylCapability("loom_triage_and_fix")]
    [McpServerTool(Name = "qyl.get_triage", Title = "Get Triage Result",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    public async partial Task<string> GetTriageAsync(
        string issueId,
        CancellationToken ct = default)
    {
        var uri = new Uri($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/triage", UriKind.Relative);
        using var response = await http.GetAsync(uri, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return $"No triage result found for issue {issueId}. Use qyl.trigger_triage to assess it.";

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    [McpServerTool(Name = "qyl.list_triage", Title = "List Triage Results",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    public async partial Task<string> ListTriageAsync(
        string? automationLevel = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        var path = $"/api/v1/triage?limit={Math.Clamp(limit, 1, 100)}";
        if (automationLevel is not null)
            path += $"&automationLevel={Uri.EscapeDataString(automationLevel)}";

        var uri = new Uri(path, UriKind.Relative);
        using var response = await http.GetAsync(uri, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    [McpServerTool(Name = "qyl.trigger_triage", Title = "Trigger Triage",
        ReadOnly = false, Destructive = false, Idempotent = false,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> TriggerTriageAsync(
        string issueId,
        CancellationToken ct = default)
    {
        var uri = new Uri($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/triage", UriKind.Relative);
        using var response = await http.PostAsync(uri, null, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return $"Issue {issueId} not found.";

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }
}
