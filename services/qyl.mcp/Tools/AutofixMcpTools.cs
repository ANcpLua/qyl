using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Contracts.Loom;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Loom)]
internal sealed partial class AutofixMcpTools(HttpClient http)
{
    [QylCapability("loom_triage_and_fix")]
    [McpServerTool(Name = "qyl.list_fix_runs", Title = "List Fix Runs",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    public async partial Task<LoomToolEnvelope<LoomFixRunList>> ListFixRunsAsync(
        string issueId,
        int? limit = null,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(limit ?? 10, 1, 100);
        var uri = new Uri(
            $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs?limit={take}", UriKind.Relative);
        using var resp = await http.GetAsync(uri, ct).ConfigureAwait(false);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return LoomToolEnvelope.Fail<LoomFixRunList>($"Issue '{issueId}' not found.");

        if (!resp.IsSuccessStatusCode)
        {
            return LoomToolEnvelope.Fail<LoomFixRunList>(await ReadCollectorErrorAsync(
                resp,
                "Failed to list fix runs.",
                ct).ConfigureAwait(false));
        }

        var payload = await resp.Content.ReadFromJsonAsync(
            LoomMcpJsonContext.Default.LoomFixRunList, ct).ConfigureAwait(false);

        if (payload is null)
            return LoomToolEnvelope.Fail<LoomFixRunList>("Failed to parse fix run list from collector.");

        return LoomToolEnvelope.Ok(payload);
    }

    [QylCapability("loom_triage_and_fix", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.get_fix_run", Title = "Get Fix Run Details",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    public async partial Task<LoomToolEnvelope<LoomFixRunDto>> GetFixRunAsync(
        string issueId,
        string runId,
        CancellationToken ct = default)
    {
        var uri = new Uri(
            $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}",
            UriKind.Relative);
        using var resp = await http.GetAsync(uri, ct).ConfigureAwait(false);

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return LoomToolEnvelope.Fail<LoomFixRunDto>(
                $"Fix run '{runId}' not found for issue '{issueId}'.");
        }

        if (!resp.IsSuccessStatusCode)
        {
            return LoomToolEnvelope.Fail<LoomFixRunDto>(await ReadCollectorErrorAsync(
                resp,
                "Failed to load fix run details.",
                ct).ConfigureAwait(false));
        }

        var payload = await resp.Content.ReadFromJsonAsync(
            LoomMcpJsonContext.Default.LoomFixRunDto, ct).ConfigureAwait(false);

        return payload is null
            ? LoomToolEnvelope.Fail<LoomFixRunDto>("Failed to parse fix run details from collector.")
            : LoomToolEnvelope.Ok(payload);
    }

    [QylCapability("loom_triage_and_fix", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.get_fix_run_steps", Title = "Get Fix Run Pipeline Steps",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    public async partial Task<LoomToolEnvelope<LoomAutofixStepList>> GetFixRunStepsAsync(
        string issueId,
        string runId,
        CancellationToken ct = default)
    {
        var uri = new Uri(
            $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}/steps",
            UriKind.Relative);
        using var resp = await http.GetAsync(uri, ct).ConfigureAwait(false);

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return LoomToolEnvelope.Fail<LoomAutofixStepList>(
                $"Fix run '{runId}' not found for issue '{issueId}'.");
        }

        if (!resp.IsSuccessStatusCode)
        {
            return LoomToolEnvelope.Fail<LoomAutofixStepList>(await ReadCollectorErrorAsync(
                resp,
                "Failed to load fix run steps.",
                ct).ConfigureAwait(false));
        }

        var payload = await resp.Content.ReadFromJsonAsync(
            LoomMcpJsonContext.Default.LoomAutofixStepList, ct).ConfigureAwait(false);

        return payload is null
            ? LoomToolEnvelope.Fail<LoomAutofixStepList>("Failed to parse fix run steps from collector.")
            : LoomToolEnvelope.Ok(payload);
    }

    [QylCapability("loom_triage_and_fix", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.approve_fix_run", Title = "Approve Fix Run",
        ReadOnly = false, Destructive = true, Idempotent = true,
        TaskSupport = ToolTaskSupport.Required)]
    public async partial Task<LoomToolEnvelope<LoomFixRunTransitionResponse>> ApproveFixRunAsync(
        string issueId,
        string runId,
        CancellationToken ct = default)
    {
        var uri = new Uri(
            $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}/approve",
            UriKind.Relative);
        using var resp = await http.PostAsync(uri, null, ct).ConfigureAwait(false);

        if (resp.StatusCode is HttpStatusCode.NotFound)
            return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(
                $"Fix run '{runId}' not found for issue '{issueId}'.");

        if (resp.StatusCode is HttpStatusCode.BadRequest)
            return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(await ReadCollectorErrorAsync(
                resp,
                "Cannot approve fix run.",
                ct).ConfigureAwait(false));

        if (!resp.IsSuccessStatusCode)
        {
            return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(await ReadCollectorErrorAsync(
                resp,
                "Failed to approve fix run.",
                ct).ConfigureAwait(false));
        }

        var transition = await resp.Content.ReadFromJsonAsync(
            LoomMcpJsonContext.Default.LoomFixRunTransitionResponse, ct).ConfigureAwait(false);

        return transition is null
            ? LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>("Failed to parse approval response from collector.")
            : LoomToolEnvelope.Ok(transition);
    }

    [McpServerTool(Name = "qyl.reject_fix_run", Title = "Reject Fix Run",
        ReadOnly = false, Destructive = true, Idempotent = true)]
    public async partial Task<LoomToolEnvelope<LoomFixRunTransitionResponse>> RejectFixRunAsync(
        string issueId,
        string runId,
        string? reason = null,
        CancellationToken ct = default)
    {
        var uri = new Uri(
            $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}/reject",
            UriKind.Relative);
        using var resp = await http.PostAsJsonAsync(uri, new { reason }, ct).ConfigureAwait(false);

        if (resp.StatusCode is HttpStatusCode.NotFound)
            return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(
                $"Fix run '{runId}' not found for issue '{issueId}'.");

        if (resp.StatusCode is HttpStatusCode.BadRequest)
            return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(await ReadCollectorErrorAsync(
                resp,
                "Cannot reject fix run.",
                ct).ConfigureAwait(false));

        if (!resp.IsSuccessStatusCode)
        {
            return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(await ReadCollectorErrorAsync(
                resp,
                "Failed to reject fix run.",
                ct).ConfigureAwait(false));
        }

        var transition = await resp.Content.ReadFromJsonAsync(
            LoomMcpJsonContext.Default.LoomFixRunTransitionResponse, ct).ConfigureAwait(false);

        return transition is null
            ? LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>("Failed to parse rejection response from collector.")
            : LoomToolEnvelope.Ok(transition);
    }

    private static async Task<string> ReadCollectorErrorAsync(HttpResponseMessage resp, string fallback,
        CancellationToken ct)
    {
        var errorBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(errorBody))
            return fallback;

        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                return error.GetString() ?? fallback;
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }

        return errorBody.Trim();
    }
}
