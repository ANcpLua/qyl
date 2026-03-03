using System.ComponentModel;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tool that packages an error issue, its triage assessment, and the latest
///     fix run into a structured markdown context block for use by coding agents
///     (Claude Code, Cursor, etc.).  Read-only — no LLM call required.
/// </summary>
[McpServerToolType]
internal sealed class ExportForAgentTools(HttpClient http)
{
    [McpServerTool(Name = "qyl.export_for_agent", Title = "Export Issue for Coding Agent",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 Export a structured context block for a specific error issue.

                 Combines:
                 - Issue metadata (title, type, status, occurrences)
                 - Recent error events with stack traces
                 - AI triage assessment (fixability score, root cause hypothesis)
                 - Latest fix run with generated patch JSON (if available)

                 Returns a markdown block ready to paste into Claude Code, Cursor,
                 or any other coding agent session to provide full issue context.
                 """)]
    public async Task<string> ExportForAgentAsync(
        [Description("The error issue ID to export context for")] string issueId,
        [Description("Include the latest fix run and generated patch if available (default: true)")] bool includeFix = true,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            // 1. Issue details
            using HttpResponseMessage issueResp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}", ct)
                .ConfigureAwait(false);

            if (issueResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"Error issue '{issueId}' not found.";

            issueResp.EnsureSuccessStatusCode();
            string issueJson = await issueResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // 2. Recent events (stack traces)
            using HttpResponseMessage eventsResp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/events?limit=3", ct)
                .ConfigureAwait(false);
            string eventsJson = eventsResp.IsSuccessStatusCode
                ? await eventsResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : "{}";

            // 3. Triage assessment
            using HttpResponseMessage triageResp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/triage", ct)
                .ConfigureAwait(false);
            string? triageJson = triageResp.IsSuccessStatusCode
                ? await triageResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : null;

            // 4. Latest fix run (optional)
            string? fixRunJson = null;
            if (includeFix)
            {
                using HttpResponseMessage fixRunsResp = await http
                    .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs?limit=1", ct)
                    .ConfigureAwait(false);
                if (fixRunsResp.IsSuccessStatusCode)
                    fixRunJson = await fixRunsResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }

            return FormatContextBlock(issueId, issueJson, eventsJson, triageJson, fixRunJson);
        });

    private static string FormatContextBlock(
        string issueId, string issueJson, string eventsJson,
        string? triageJson, string? fixRunJson)
    {
        StringBuilder sb = new();
        sb.AppendLine("<!-- qyl:context -->");
        sb.AppendLine($"# qyl Issue Context: {issueId}");
        sb.AppendLine();
        sb.AppendLine("## Issue");
        sb.AppendLine("```json");
        sb.AppendLine(issueJson);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Recent Events (stack traces)");
        sb.AppendLine("```json");
        sb.AppendLine(eventsJson);
        sb.AppendLine("```");

        if (triageJson is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## AI Triage Assessment");
            sb.AppendLine("```json");
            sb.AppendLine(triageJson);
            sb.AppendLine("```");
        }

        if (fixRunJson is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Generated Fix (latest run)");
            sb.AppendLine("```json");
            sb.AppendLine(fixRunJson);
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("<!-- /qyl:context -->");
        sb.AppendLine();
        sb.AppendLine("> Paste this block into your coding agent session to provide full issue context.");
        return sb.ToString();
    }
}
