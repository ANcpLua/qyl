using System.Net;
using System.Net.Http.Json;

namespace Qyl.Loom;

/// <summary>
///     Typed HTTP client for the qyl collector REST API.
///     Replaces direct DuckDbStore usage so Loom runs as a standalone process.
/// </summary>
public sealed class CollectorClient(HttpClient http)
{
    // ── Issues ────────────────────────────────────────────────────────────────

    public async Task<IssueSummary?> GetIssueByIdAsync(string issueId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}", ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync(CollectorClientJsonContext.Default.IssueSummary, ct)
            .ConfigureAwait(false);
    }

    public async Task<List<string>> GetUntriagedIssueIdsAsync(int limit = 20, CancellationToken ct = default)
    {
        var response = await http
            .GetAsync($"/api/v1/issues/untriaged?limit={limit}", ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content
            .ReadFromJsonAsync(CollectorClientJsonContext.Default.UntriagedIssuesResponse, ct)
            .ConfigureAwait(false);

        return envelope?.Ids ?? [];
    }

    public async Task<List<IssueEventDto>> GetIssueEventsAsync(
        string issueId, int limit = 100, CancellationToken ct = default)
    {
        var response = await http
            .GetAsync($"/api/v1/issues/{Uri.EscapeDataString(issueId)}/events?limit={limit}", ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content
            .ReadFromJsonAsync(CollectorClientJsonContext.Default.IssueEventsResponse, ct)
            .ConfigureAwait(false);

        return envelope?.Items ?? [];
    }

    // ── Fix Runs ──────────────────────────────────────────────────────────────

    public async Task<List<FixRunRecord>> GetPendingFixRunsAsync(int limit = 10, CancellationToken ct = default)
    {
        var response = await http
            .GetAsync($"/api/v1/fix-runs?status=pending&limit={limit}", ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content
            .ReadFromJsonAsync(CollectorClientJsonContext.Default.FixRunListResponse, ct)
            .ConfigureAwait(false);

        return envelope?.Items ?? [];
    }

    public async Task<FixRunRecord?> GetFixRunAsync(string runId, CancellationToken ct = default)
    {
        var response = await http
            .GetAsync($"/api/v1/fix-runs/{Uri.EscapeDataString(runId)}", ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync(CollectorClientJsonContext.Default.FixRunRecord, ct)
            .ConfigureAwait(false);
    }

    public async Task<FixRunRecord> CreateFixRunAsync(
        string issueId, FixPolicy policy,
        string? instruction = null, string? stoppingPoint = null,
        CancellationToken ct = default)
    {
        var request = new FixRunCreateRequest(
            policy.ToString().ToLowerInvariant(), instruction, stoppingPoint);
        var response = await http
            .PostAsJsonAsync(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs",
                request,
                CollectorClientJsonContext.Default.FixRunCreateRequest,
                ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content
                   .ReadFromJsonAsync(CollectorClientJsonContext.Default.FixRunRecord, ct)
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Collector returned null fix run.");
    }

    public async Task UpdateFixRunAsync(
        string issueId, string runId, string status,
        string? description = null, double? confidence = null, string? changesJson = null,
        CancellationToken ct = default)
    {
        var request = new FixRunPatchRequest(status, description, confidence, changesJson);
        var response = await http
            .PatchAsJsonAsync(
                $"/api/v1/issues/{Uri.EscapeDataString(issueId)}/fix-runs/{Uri.EscapeDataString(runId)}",
                request,
                CollectorClientJsonContext.Default.FixRunPatchRequest,
                ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    // ── Autofix Steps ─────────────────────────────────────────────────────────

    public async Task InsertAutofixStepAsync(AutofixStepRecord step, CancellationToken ct = default)
    {
        var response = await http
            .PostAsJsonAsync(
                $"/api/v1/fix-runs/{Uri.EscapeDataString(step.RunId)}/steps",
                step,
                CollectorClientJsonContext.Default.AutofixStepRecord,
                ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAutofixStepAsync(
        string runId, string stepId, string status,
        string? outputJson = null, string? errorMessage = null, CancellationToken ct = default)
    {
        var request = new AutofixStepPatchRequest(status, outputJson, errorMessage);
        var response = await http
            .PatchAsJsonAsync(
                $"/api/v1/fix-runs/{Uri.EscapeDataString(runId)}/steps/{Uri.EscapeDataString(stepId)}",
                request,
                CollectorClientJsonContext.Default.AutofixStepPatchRequest,
                ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    // ── Triage ────────────────────────────────────────────────────────────────

    public async Task<TriageResult> InsertTriageResultAsync(
        TriageResult result, CancellationToken ct = default)
    {
        var response = await http
            .PostAsJsonAsync(
                $"/api/v1/issues/{Uri.EscapeDataString(result.IssueId)}/triage",
                result,
                CollectorClientJsonContext.Default.TriageResult,
                ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync(CollectorClientJsonContext.Default.TriageResult, ct)
            .ConfigureAwait(false) ?? result;
    }

    public async Task UpdateTriageFixRunAsync(
        string triageId, string fixRunId, CancellationToken ct = default)
    {
        var request = new TriagePatchRequest(fixRunId);
        var response = await http
            .PatchAsJsonAsync(
                $"/api/v1/triage/{Uri.EscapeDataString(triageId)}",
                request,
                CollectorClientJsonContext.Default.TriagePatchRequest,
                ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    // ── Deployments ───────────────────────────────────────────────────────────

    public async Task<List<DeploymentDto>> GetDeploymentsAfterAsync(
        DateTime since, CancellationToken ct = default)
    {
        var response = await http
            .GetAsync($"/api/v1/deployments?since={since:O}", ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content
            .ReadFromJsonAsync(CollectorClientJsonContext.Default.DeploymentListResponse, ct)
            .ConfigureAwait(false);

        return envelope?.Items ?? [];
    }

    // ── Regressions ───────────────────────────────────────────────────────────

    public async Task<List<string>> DetectRegressionsAsync(
        string serviceName, string? version = null, CancellationToken ct = default)
    {
        var url = ANcpLua.Roslyn.Utilities.Web.QueryString.AppendPairs(
            $"/api/v1/regressions/check/{Uri.EscapeDataString(serviceName)}",
            ("version", version));

        var response = await http.PostAsync(url, null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content
            .ReadFromJsonAsync(CollectorClientJsonContext.Default.RegressionCheckResponse, ct)
            .ConfigureAwait(false);

        return envelope?.RegressedIssueIds ?? [];
    }

    // ── Issues (list) ────────────────────────────────────────────────────────

    public async Task<List<IssueSummary>> GetRecentIssuesAsync(int limit = 10, CancellationToken ct = default)
    {
        var response = await http
            .GetAsync($"/api/v1/issues?limit={limit}", ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content
            .ReadFromJsonAsync(CollectorClientJsonContext.Default.IssueListResponse, ct)
            .ConfigureAwait(false);

        return envelope?.Items ?? [];
    }
}

// ── Response DTOs ────────────────────────────────────────────────────────────
// Minimal shapes matching collector JSON responses.

public sealed record UntriagedIssuesResponse(List<string> Ids);

public sealed record IssueEventsResponse(List<IssueEventDto> Items, int Total);

/// <summary>
///     Subset of ErrorIssueEventRow returned by the collector issue events endpoint.
///     Only the fields Loom needs for autofix context gathering.
/// </summary>
public sealed record IssueEventDto
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("issueId")] public required string IssueId { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("stackTrace")] public string? StackTrace { get; init; }
    [JsonPropertyName("environment")] public string? Environment { get; init; }
    [JsonPropertyName("timestamp")] public required DateTime Timestamp { get; init; }
}

public sealed record FixRunListResponse(List<FixRunRecord> Items, int Total);

public sealed record FixRunCreateRequest(
    string? Policy,
    string? Instruction = null,
    string? StoppingPoint = null);

public sealed record FixRunPatchRequest(
    string? Status = null,
    string? Description = null,
    double? Confidence = null,
    string? ChangesJson = null);

public sealed record AutofixStepPatchRequest(
    string? Status = null,
    string? OutputJson = null,
    string? ErrorMessage = null);

public sealed record TriagePatchRequest(string? FixRunId = null);

public sealed record DeploymentListResponse(List<DeploymentDto> Items);

public sealed record DeploymentDto
{
    [JsonPropertyName("deploymentId")] public required string DeploymentId { get; init; }
    [JsonPropertyName("serviceName")] public required string ServiceName { get; init; }
    [JsonPropertyName("serviceVersion")] public required string ServiceVersion { get; init; }
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("status")] public required string Status { get; init; }
    [JsonPropertyName("startTime")] public required DateTime StartTime { get; init; }
}

public sealed record RegressionCheckResponse(List<string> RegressedIssueIds, int Count);

public sealed record IssueListResponse(List<IssueSummary> Items, int Total);

// ── Source-generated JSON context ────────────────────────────────────────────

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(IssueSummary))]
[JsonSerializable(typeof(UntriagedIssuesResponse))]
[JsonSerializable(typeof(IssueEventsResponse))]
[JsonSerializable(typeof(IssueEventDto))]
[JsonSerializable(typeof(FixRunRecord))]
[JsonSerializable(typeof(FixRunListResponse))]
[JsonSerializable(typeof(FixRunCreateRequest))]
[JsonSerializable(typeof(FixRunPatchRequest))]
[JsonSerializable(typeof(AutofixStepRecord))]
[JsonSerializable(typeof(AutofixStepPatchRequest))]
[JsonSerializable(typeof(TriageResult))]
[JsonSerializable(typeof(TriagePatchRequest))]
[JsonSerializable(typeof(DeploymentListResponse))]
[JsonSerializable(typeof(DeploymentDto))]
[JsonSerializable(typeof(RegressionCheckResponse))]
[JsonSerializable(typeof(IssueListResponse))]
[JsonSerializable(typeof(ConfidenceResult))]
internal sealed partial class CollectorClientJsonContext : JsonSerializerContext;
