using System.Net;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Loom)]
internal sealed partial class ExportForAgentTools(HttpClient http)
{
    [QylCapability("loom_triage_and_fix", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.export_for_agent", Title = "Export Issue for Coding Agent",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    public async partial Task<string> ExportForAgentAsync(
        string issueId,
        bool includeFix = true,
        CancellationToken ct = default) =>
        await CollectorHelper.ExecuteAsync(async () =>
        {
            using var issueResp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}", ct)
                .ConfigureAwait(false);

            if (issueResp.StatusCode == HttpStatusCode.NotFound)
                return $"Error issue '{issueId}' not found.";

            issueResp.EnsureSuccessStatusCode();
            var issueJson = await issueResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            using var eventsResp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/events?limit=3", ct)
                .ConfigureAwait(false);
            var eventsJson = eventsResp.IsSuccessStatusCode
                ? await eventsResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : "{}";

            using var triageResp = await http
                .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/triage", ct)
                .ConfigureAwait(false);
            var triageJson = triageResp.IsSuccessStatusCode
                ? await triageResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : null;

            string? fixRunJson = null;
            if (includeFix)
            {
                using var fixRunsResp = await http
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
