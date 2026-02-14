using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying workflow executions.
/// </summary>
[McpServerToolType]
public sealed class WorkflowTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_workflow_runs")]
    [Description("""
                 List workflow executions with optional filters.

                 Shows workflow runs with:
                 - Workflow name and run ID
                 - Status (queued, running, completed, failed)
                 - Duration and trigger info
                 - Start and end time

                 Example queries:
                 - All recent: list_workflow_runs()
                 - Failures: list_workflow_runs(status="failed")
                 - By name: list_workflow_runs(workflowName="deploy")

                 Returns: Table of workflow runs with key metrics
                 """)]
    public Task<string> ListWorkflowRunsAsync(
        [Description("Maximum runs to return (default: 20)")]
        int limit = 20,
        [Description("Filter by status: 'queued', 'running', 'completed', 'failed'")]
        string? status = null,
        [Description("Filter by workflow name (partial match)")]
        string? workflowName = null)
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/workflows/runs?limit={limit}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrEmpty(workflowName))
                url += $"&workflowName={Uri.EscapeDataString(workflowName)}";

            var response = await client.GetFromJsonAsync<WorkflowRunsResponse>(
                url, WorkflowJsonContext.Default.WorkflowRunsResponse).ConfigureAwait(false);

            if (response?.Runs is null || response.Runs.Count is 0)
                return "No workflow runs found matching the criteria.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Workflow Runs ({response.Runs.Count} results)");
            sb.AppendLine();
            sb.AppendLine("| Run ID | Workflow | Status | Trigger | Duration | Started |");
            sb.AppendLine("|--------|----------|--------|---------|----------|---------|");

            foreach (var run in response.Runs)
            {
                var statusIcon = run.Status switch
                {
                    "failed" => "❌",
                    "running" => "🔄",
                    "queued" => "⏳",
                    _ => "✅"
                };
                var runId = run.RunId.Length > 8 ? run.RunId[..8] : run.RunId;
                var durationStr = run.DurationMs > 0 ? $"{run.DurationMs:F0}ms" : "-";
                var trigger = run.Trigger ?? "-";
                var started = run.StartTime ?? "-";

                sb.AppendLine($"| {runId} | {run.WorkflowName ?? "unknown"} | {statusIcon} {run.Status} | {trigger} | {durationStr} | {started} |");
            }

            return sb.ToString();
        });
    }

    [McpServerTool(Name = "qyl.get_workflow_run")]
    [Description("""
                 Get detailed info for a specific workflow run.

                 Returns full details including:
                 - Workflow name and run ID
                 - Status and trigger
                 - Start/end timing and duration
                 - Step-level events and outputs
                 - Error details if failed

                 Returns: Full workflow run details with events
                 """)]
    public Task<string> GetWorkflowRunAsync(
        [Description("The workflow run ID to look up")]
        string runId)
    {
        return CollectorHelper.ExecuteAsync(async () =>
        {
            var run = await client.GetFromJsonAsync<WorkflowRunDetailDto>(
                $"/api/v1/workflows/runs/{Uri.EscapeDataString(runId)}",
                WorkflowJsonContext.Default.WorkflowRunDetailDto).ConfigureAwait(false);

            if (run is null)
                return $"Workflow run '{runId}' not found.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Workflow Run: {run.WorkflowName ?? "unknown"}");
            sb.AppendLine();
            sb.AppendLine($"- **Run ID:** {run.RunId}");
            sb.AppendLine($"- **Status:** {run.Status}");
            sb.AppendLine($"- **Trigger:** {run.Trigger ?? "unknown"}");

            if (run.DurationMs > 0)
                sb.AppendLine($"- **Duration:** {run.DurationMs:F0}ms");

            if (!string.IsNullOrEmpty(run.StartTime))
                sb.AppendLine($"- **Started:** {run.StartTime}");

            if (!string.IsNullOrEmpty(run.EndTime))
                sb.AppendLine($"- **Ended:** {run.EndTime}");

            if (!string.IsNullOrEmpty(run.ErrorMessage))
            {
                sb.AppendLine();
                sb.AppendLine("### Error");
                sb.AppendLine("```");
                sb.AppendLine(run.ErrorMessage);
                sb.AppendLine("```");
            }

            if (run.Events is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine($"## Steps ({run.Events.Count})");
                sb.AppendLine();
                sb.AppendLine("| # | Step | Status | Duration |");
                sb.AppendLine("|---|------|--------|----------|");

                foreach (var evt in run.Events)
                {
                    var stepIcon = evt.Status switch
                    {
                        "failed" => "❌",
                        "running" => "🔄",
                        _ => "✅"
                    };
                    var durationStr = evt.DurationMs > 0 ? $"{evt.DurationMs:F0}ms" : "-";
                    sb.AppendLine($"| {evt.Sequence} | {evt.StepName ?? "unknown"} | {stepIcon} {evt.Status} | {durationStr} |");
                }
            }

            return sb.ToString();
        });
    }
}

#region DTOs

internal sealed record WorkflowRunsResponse(
    [property: JsonPropertyName("runs")] List<WorkflowRunDto>? Runs,
    [property: JsonPropertyName("total")] int Total);

internal sealed record WorkflowRunDto(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("workflow_name")] string? WorkflowName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("trigger")] string? Trigger,
    [property: JsonPropertyName("duration_ms")] double DurationMs,
    [property: JsonPropertyName("start_time")] string? StartTime);

internal sealed record WorkflowRunDetailDto(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("workflow_name")] string? WorkflowName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("trigger")] string? Trigger,
    [property: JsonPropertyName("duration_ms")] double DurationMs,
    [property: JsonPropertyName("start_time")] string? StartTime,
    [property: JsonPropertyName("end_time")] string? EndTime,
    [property: JsonPropertyName("error_message")] string? ErrorMessage,
    [property: JsonPropertyName("events")] List<WorkflowStepDto>? Events);

internal sealed record WorkflowStepDto(
    [property: JsonPropertyName("step_name")] string? StepName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("duration_ms")] double DurationMs,
    [property: JsonPropertyName("sequence")] int Sequence);

#endregion

[JsonSerializable(typeof(WorkflowRunsResponse))]
[JsonSerializable(typeof(WorkflowRunDetailDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class WorkflowJsonContext : JsonSerializerContext;
