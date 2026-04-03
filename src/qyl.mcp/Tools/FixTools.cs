using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using Qyl.Contracts.Loom;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tool that submits and tracks Loom fix jobs at the collector boundary.
///     All generation work is executed in collector-side Loom workers.
/// </summary>
[McpServerToolType]
internal sealed class FixTools(HttpClient http)
{
    [McpServerTool(Name = "qyl.generate_fix", Title = "Generate Fix",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("""
                 Enqueue the Loom autofix job in the collector.

                 The fix run is created in the collector and processed asynchronously by
                 Loom background workers. This tool does not execute RCA or patch generation.

                 Returns: Typed envelope containing created run details.
                 """)]
    public async Task<LoomToolEnvelope<LoomFixRunDto>> GenerateFixAsync(
        [Description("The error issue ID to generate a fix for")]
        string issueId,
        [Description("Fix policy: auto_apply, require_review (default), dry_run")]
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
            return LoomToolEnvelope.Fail<LoomFixRunDto>(
                $"Failed to create fix run: {(int)createResp.StatusCode} {createResp.ReasonPhrase}");

        var run = await createResp.Content.ReadFromJsonAsync(
            LoomMcpJsonContext.Default.LoomFixRunDto, ct).ConfigureAwait(false);

        return run is null
            ? LoomToolEnvelope.Fail<LoomFixRunDto>("Failed to parse fix run response from collector.")
            : LoomToolEnvelope.Ok(run);
    }
}
