using System.Net.Http.Json;
using System.Text.Json;
using Qyl.Api.Contracts.Workflow;

namespace Qyl.Cli.Codex;

internal sealed class WorkflowCollectorClient(
    HttpClient httpClient,
    Uri apiBaseUri,
    string? apiKey)
{
    private const string ApiKeyHeader = "x-otlp-api-key";

    public async Task CreateRunAsync(
        WorkflowSpoolMetadata metadata,
        CancellationToken cancellationToken)
    {
        var request = new WorkflowRunCreateRequest
        {
            RunId = metadata.RunId,
            ThreadId = metadata.ThreadId,
            Title = metadata.Title,
            StartedAt = metadata.StartedAt,
            Metadata = new Dictionary<string, object>
            {
                ["codex_version"] = metadata.CodexVersion,
                ["app_server_schema"] = metadata.SchemaDigest,
                ["working_directory"] = metadata.WorkingDirectory,
                ["capture"] = "full"
            }
        };
        using var message = CreateRequest(
            HttpMethod.Post,
            "workflow-runs/",
            JsonContent.Create(
                request,
                CodexWorkflowContractJsonContext.Default.WorkflowRunCreateRequest));
        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkflowEventBatchAppendResponse> AppendAsync(
        string runId,
        IReadOnlyList<WorkflowSpoolEntry> entries,
        CancellationToken cancellationToken)
    {
        var content = entries
            .SelectMany(static entry => entry.Content)
            .DistinctBy(static chunk => chunk.ContentRef, StringComparer.Ordinal)
            .ToArray();
        var request = new WorkflowEventBatchAppendRequest
        {
            ClientId = "qyl-codex-observer",
            Events = entries.Select(static entry => entry.Event).ToArray(),
            Content = content.Length is 0 ? null : content
        };
        using var message = CreateRequest(
            HttpMethod.Post,
            $"workflow-runs/{Uri.EscapeDataString(runId)}/events",
            JsonContent.Create(
                request,
                CodexWorkflowContractJsonContext.Default.WorkflowEventBatchAppendRequest));
        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync(
                   CodexWorkflowContractJsonContext.Default.WorkflowEventBatchAppendResponse,
                   cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidDataException("Collector returned an empty workflow append response.");
    }

    public async Task<WorkflowControlCommandPage> PollControlsAsync(
        string runId,
        ulong afterSequence,
        int waitMilliseconds,
        CancellationToken cancellationToken)
    {
        var path =
            $"workflow-runs/{Uri.EscapeDataString(runId)}/commands?after_sequence={afterSequence.ToString(CultureInfo.InvariantCulture)}&limit=100&wait_ms={waitMilliseconds.ToString(CultureInfo.InvariantCulture)}";
        using var message = CreateRequest(HttpMethod.Get, path);
        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync(
                   CodexWorkflowContractJsonContext.Default.WorkflowControlCommandPage,
                   cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidDataException("Collector returned an empty workflow control page.");
    }

    public async Task<WorkflowControlCommand> UpdateControlAsync(
        string runId,
        string commandId,
        WorkflowControlStatus status,
        string? error,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var request = new WorkflowControlStatusUpdateRequest
        {
            Status = status,
            Error = error,
            OccurredAt = occurredAt
        };
        using var message = CreateRequest(
            HttpMethod.Post,
            $"workflow-runs/{Uri.EscapeDataString(runId)}/commands/{Uri.EscapeDataString(commandId)}/status",
            JsonContent.Create(
                request,
                CodexWorkflowContractJsonContext.Default.WorkflowControlStatusUpdateRequest));
        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync(
                   CodexWorkflowContractJsonContext.Default.WorkflowControlCommand,
                   cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidDataException("Collector returned an empty workflow control response.");
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativePath,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, new Uri(apiBaseUri, relativePath))
        {
            Content = content
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.TryAddWithoutValidation(ApiKeyHeader, apiKey);
        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException(
            $"Collector workflow API returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
            null,
            response.StatusCode);
    }
}
