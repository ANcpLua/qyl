using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Contracts.Loom;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Loom)]
internal sealed partial class FixTools(HttpClient http)
{
    [McpServerTool(Name = "qyl.generate_fix", Title = "Generate Fix",
        ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Required)]
    public async partial Task<LoomToolEnvelope<LoomFixRunDto>> GenerateFixAsync(
        string issueId,
        string? policy = null,
        CancellationToken ct = default)
    {
        using var createResp = await http.PostAsJsonAsync(
            $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs",
            new LoomFixRunCreateRequest(policy),
            ct).ConfigureAwait(false);

        if (createResp.StatusCode == HttpStatusCode.NotFound)
            return LoomToolEnvelope.Fail<LoomFixRunDto>($"Issue '{issueId}' not found.");

        if (!createResp.IsSuccessStatusCode)
        {
            return LoomToolEnvelope.Fail<LoomFixRunDto>(
                $"Failed to create fix run: {(int)createResp.StatusCode} {createResp.ReasonPhrase}");
        }

        var run = await createResp.Content.ReadFromJsonAsync(
            LoomMcpJsonContext.Default.LoomFixRunDto, ct).ConfigureAwait(false);

        return run is null
            ? LoomToolEnvelope.Fail<LoomFixRunDto>("Failed to parse fix run response from collector.")
            : LoomToolEnvelope.Ok(run);
    }
}
