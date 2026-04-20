namespace qyl.mcp.Tools;

using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;
using Qyl.Contracts.Loom;

/// <summary>
///     MCP tools for managing autofix fix runs:
///     list, detail, steps, approve, and reject.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Loom)]
internal sealed class AutofixMcpTools(HttpClient http)
{
    [QylCapability("loom_triage_and_fix")]
    [McpServerTool(Name = "qyl.list_fix_runs", Title = "List Fix Runs",
        ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("""
                 List fix runs for an error issue, ordered by most recent first.
                 Returns run metadata as typed JSON for machine-readability.
                 """)]
    public async Task<LoomToolEnvelope<LoomFixRunList>> ListFixRunsAsync(
        [Description("The error issue ID")] string issueId,
        [Description("Maximum number of runs to return (default: 10)")]
        int? limit = null,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(limit ?? 10, 1, 100);
        using var resp = await http.GetAsync(
            $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs?limit={take}", ct).ConfigureAwait(false);

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
    [Description("""
                 Get full details of a specific fix run.
                 Includes status, policy, confidence score, and patch metadata.
                 """)]
    public async Task<LoomToolEnvelope<LoomFixRunDto>> GetFixRunAsync(
        [Description("The error issue ID")] string issueId,
        [Description("The fix run ID")] string runId,
        CancellationToken ct = default)
    {
        using var resp = await http.GetAsync(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}", ct)
            .ConfigureAwait(false);

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
    [Description("""
                 Get the individual pipeline steps for a fix run.
                 Shows status, timing, and input/output metadata.
                 """)]
    public async Task<LoomToolEnvelope<LoomAutofixStepList>> GetFixRunStepsAsync(
        [Description("The error issue ID")] string issueId,
        [Description("The fix run ID")] string runId,
        CancellationToken ct = default)
    {
        using var resp = await http.GetAsync(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}/steps", ct)
            .ConfigureAwait(false);

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
        ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("""
                 Approve a fix run that is in 'review' status.
                 This transitions the fix run to 'applied' status.
                 """)]
    public async Task<LoomToolEnvelope<LoomFixRunTransitionResponse>> ApproveFixRunAsync(
        [Description("The error issue ID")] string issueId,
        [Description("The fix run ID to approve")]
        string runId,
        CancellationToken ct = default)
    {
        using var resp = await http.PostAsync(
            $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}/approve",
            null, ct).ConfigureAwait(false);

        switch (resp.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(
                    $"Fix run '{runId}' not found for issue '{issueId}'.");
            case HttpStatusCode.BadRequest:
                return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(await ReadCollectorErrorAsync(
                    resp,
                    "Cannot approve fix run.",
                    ct).ConfigureAwait(false));
        }

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
        ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("""
                 Reject a fix run that is in 'review' status.
                 Optionally provide a reason for rejection.
                 """)]
    public async Task<LoomToolEnvelope<LoomFixRunTransitionResponse>> RejectFixRunAsync(
        [Description("The error issue ID")] string issueId,
        [Description("The fix run ID to reject")]
        string runId,
        [Description("Optional reason for rejection")]
        string? reason = null,
        CancellationToken ct = default)
    {
        using var resp = await http.PostAsJsonAsync(
            $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}/reject",
            new { reason },
            ct).ConfigureAwait(false);

        switch (resp.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(
                    $"Fix run '{runId}' not found for issue '{issueId}'.");
            case HttpStatusCode.BadRequest:
                return LoomToolEnvelope.Fail<LoomFixRunTransitionResponse>(await ReadCollectorErrorAsync(
                    resp,
                    "Cannot reject fix run.",
                    ct).ConfigureAwait(false));
        }

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
        catch (JsonException)
        {
        }

        return errorBody.Trim();
    }
}
