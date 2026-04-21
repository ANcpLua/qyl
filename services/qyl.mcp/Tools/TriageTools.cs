using System.ComponentModel;
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
internal sealed class TriageTools(HttpClient http)
{
    [QylCapability("loom_triage_and_fix")]
    [McpServerTool(Name = "qyl.get_triage", Title = "Get Triage Result",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 Get the latest AI triage assessment for an error issue.
                 Returns fixability score, automation level, AI summary, and root cause hypothesis.
                 """)]
    public async Task<string> GetTriageAsync(
        [Description("The error issue ID")] string issueId,
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
    [Description("""
                 List recent triage assessments, optionally filtered by automation level.
                 Automation levels: auto, assisted, manual, skip.
                 """)]
    public async Task<string> ListTriageAsync(
        [Description("Filter by automation level: auto, assisted, manual, skip")]
        string? automationLevel = null,
        [Description("Max results (default 20)")]
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
    [Description("""
                 Trigger an AI triage assessment for a specific error issue.
                 Scores fixability, generates a summary, and may auto-route to the autofix pipeline.
                 If no LLM is configured, uses heuristic scoring as fallback.
                 """)]
    public async Task<string> TriggerTriageAsync(
        [Description("The error issue ID to triage")]
        string issueId,
        CancellationToken ct = default)
    {
        var uri = new Uri($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/triage", UriKind.Relative);
        using var response = await http.PostAsync(uri, content: null, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return $"Issue {issueId} not found.";

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }
}
