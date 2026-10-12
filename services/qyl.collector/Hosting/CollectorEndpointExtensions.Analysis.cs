using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Qyl.Collector.Analysis;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

internal static partial class CollectorEndpointExtensions
{
    private static AnalysisHandles CreateAnalysisHandles(HttpContext context, string project)
    {
        var options = context.RequestServices.GetRequiredService<OtlpApiKeyOptions>();
        var configured = context.RequestServices.GetRequiredService<IConfiguration>()["QYL_ANALYSIS_SIGNING_KEY"];
        // Authenticated projects already possess a persisted credential. Deriving a separate signing
        // key with a domain separator keeps tokens self-contained without a write from a read tool.
        var material = configured ?? options.Keys.Where(k => k.ProjectId == project).OrderBy(k => k.ApiKey, StringComparer.Ordinal).Select(k => k.ApiKey).FirstOrDefault() ?? "qyl-unsecured-local-analysis";
        return new AnalysisHandles(SHA256.HashData(Encoding.UTF8.GetBytes("qyl-analysis-v1\0" + project + "\0" + material)));
    }
    internal static async Task<IResult> AnalyzeAsync(string operation, HttpContext context, IQylStore store, TimeProvider time, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(55));
        try
        {
            var node = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: deadline.Token).ConfigureAwait(false);
            if (node is not JsonObject input) return ResourceHttp.InvalidBody("The request body must be a JSON object.");
            ValidateAnalysisInput(operation, input);
            var project = ResolveProjectScope(context);
            var snapshot = await store.ReadAnalysisSnapshotAsync(project, deadline.Token).ConfigureAwait(false);
            var result = new AnalysisEngine(snapshot, project, CreateAnalysisHandles(context, project), time.GetUtcNow()).Run(operation, input);
            // The generated contract validator is applied below before every result leaves this boundary.
            return AnalysisResult(operation, result);
        }
        catch (AnalysisInputException invalid) { return invalid.Code.EndsWith(".not_found", StringComparison.Ordinal) ? ContractErrorResults.NotFound(invalid.Field, invalid.Description) : ContractErrorResults.Validation(invalid.Field, invalid.Description, invalid.Code); }
        catch (JsonException invalid) { return ContractErrorResults.Validation(invalid.Path ?? "body", "The analysis input or output does not match the generated contract.", "analysis.contract_invalid"); }
    }
    private static void ValidateAnalysisInput(string operation, JsonObject input)
    {
        var typeInfo = operation switch
        {
            "triage" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.TriageInput,
            "list_failures" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListFailuresInput,
            "get_failure_context" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetFailureContextInput,
            "compare_groups" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.CompareGroupsInput,
            "compare_traces" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.CompareTracesInput,
            "verify_fix" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.VerifyFixInput,
            "aggregate_telemetry" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.AggregateTelemetryInput,
            "discover_attributes" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.DiscoverAttributesInput,
            "get_agent_run" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetAgentRunInput,
            "list_releases" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListReleasesInput,
            "get_service_map" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetServiceMapInput,
            "check_telemetry_health" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.CheckTelemetryHealthInput,
            "get_mcp_stats" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetMcpStatsInput,
            "list_sessions" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListSessionsInput,
            "list_traces" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListTracesInput,
            "get_trace" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetTraceInput,
            "search_logs" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.SearchLogsInput,
            "list_metrics" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListMetricsInput,
            "get_metric_series" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetMetricSeriesInput,
            "query_metric" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.QueryMetricInput,
            "ci_log" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.CiLogInput,
            "list_saved_queries" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListSavedQueriesInput,
            "run_saved_query" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.RunSavedQueryInput,
            "display_traces" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.DisplayTracesInput,
            "display_mcp_dashboard" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.DisplayMcpDashboardInput,
            "fetch_telemetry" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.FetchTelemetryInput,
            _ => throw new InvalidOperationException("Unknown analysis output contract.")
        };
        _ = JsonSerializer.Deserialize(input.ToJsonString(), typeInfo);
    }

    private static IResult AnalysisResult(string operation, JsonObject result)
    {
        var typeInfo = operation switch
        {
            "triage" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.TriageOutput,
            "list_failures" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListFailuresOutput,
            "get_failure_context" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetFailureContextOutput,
            "compare_groups" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.CompareGroupsOutput,
            "compare_traces" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.CompareTracesOutput,
            "verify_fix" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.VerifyFixOutput,
            "aggregate_telemetry" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.AggregateTelemetryOutput,
            "discover_attributes" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.DiscoverAttributesOutput,
            "get_agent_run" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetAgentRunOutput,
            "list_releases" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListReleasesOutput,
            "get_service_map" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetServiceMapOutput,
            "check_telemetry_health" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.CheckTelemetryHealthOutput,
            "get_mcp_stats" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetMcpStatsOutput,
            "list_sessions" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListSessionsOutput,
            "list_traces" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListTracesOutput,
            "get_trace" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetTraceOutput,
            "search_logs" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.SearchLogsOutput,
            "list_metrics" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListMetricsOutput,
            "get_metric_series" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.GetMetricSeriesOutput,
            "query_metric" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.QueryMetricOutput,
            "ci_log" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.CiLogOutput,
            "list_saved_queries" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.ListSavedQueriesOutput,
            "run_saved_query" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.RunSavedQueryOutput,
            "display_traces" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.DisplayTracesOutput,
            "display_mcp_dashboard" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.DisplayMcpDashboardOutput,
            "fetch_telemetry" => (System.Text.Json.Serialization.Metadata.JsonTypeInfo)AnalysisContractJsonContext.Default.FetchTelemetryOutput,
            "update_failure_status" => AnalysisContractJsonContext.Default.UpdateFailureStatusOutput,
            _ => throw new InvalidOperationException("Unknown analysis output contract.")
        };
        var typed = JsonSerializer.Deserialize(result.ToJsonString(), typeInfo)
            ?? throw new InvalidOperationException("Analysis result is empty.");
        return Results.Json(typed, typeInfo);
    }

    internal static async Task<IResult> UpdateFailureStatusAsync(HttpContext context, [FromRoute(Name = "failure_id")] string failureId, IQylStore store, TimeProvider time, CancellationToken ct)
    {
        try
        {
            if (ResourceHttp.ReadIfMatch(context.Request, out var condition) is { } error) return error;
            var body = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: ct).ConfigureAwait(false) as JsonObject;
            if (body is null) return ResourceHttp.InvalidBody("The request body must be a JSON object.");
            var status = J.String(body, "status");
            if (status is not ("open" or "resolved" or "ignored")) return ContractErrorResults.Validation("status", "Choose open, resolved or ignored.", "status.invalid");
            if (body["ignore_until"] is not null && status != "ignored") return ContractErrorResults.Validation("ignore_until", "ignore_until is valid only with status=ignored.", "ignore_until.invalid");
            if (J.String(body, "assignee")?.EnumerateRunes().Count() > 256) return ContractErrorResults.Validation("assignee", "Use at most 256 characters.", "assignee.length");
            _ = J.Date(body, "ignore_until");
            var project = ResolveProjectScope(context); var snapshot = await store.ReadAnalysisSnapshotAsync(project, ct).ConfigureAwait(false);
            var engine = new AnalysisEngine(snapshot, project, CreateAnalysisHandles(context, project), time.GetUtcNow());
            if (!engine.OwnsFailure(failureId)) return ContractErrorResults.NotFound("failure", failureId);
            var existing = snapshot.State.FirstOrDefault(s => s.StateKey == failureId); var current = existing?.Etag ?? '"' + failureId + '"';
            if (!condition.Matches(current)) return ContractErrorResults.PreconditionFailed(current);
            body["updated_at"] = time.GetUtcNow().ToString("O");
            var row = new AnalysisStateRow { ProjectId = project, StateKey = failureId, ValueJson = body.ToJsonString(), Etag = ResourceHttp.NewEtag(), UpdatedAtUnixNano = ResourceHttp.NowUnixNano(time) };
            var outcome = await store.WriteAnalysisStateAsync(row, existing?.Etag, ct).ConfigureAwait(false);
            if (outcome.Status == ResourceWriteStatus.PreconditionFailed) return ContractErrorResults.PreconditionFailed(outcome.CurrentEtag);
            var updated = await store.ReadAnalysisSnapshotAsync(project, ct).ConfigureAwait(false);
            var result = new AnalysisEngine(updated, project, CreateAnalysisHandles(context, project), time.GetUtcNow()).Run("list_failures", J.O(("failure_id", failureId), ("status", "all"), ("start_time", DateTimeOffset.UnixEpoch.ToString("O"))));
            context.Response.Headers.ETag = row.Etag;
            return AnalysisResult("update_failure_status", J.O(("failure", result["failures"]?[0])));
        }
        catch (AnalysisInputException invalid) { return invalid.Code.EndsWith(".not_found", StringComparison.Ordinal) ? ContractErrorResults.NotFound(invalid.Field, invalid.Description) : ContractErrorResults.Validation(invalid.Field, invalid.Description, invalid.Code); }
        catch (JsonException) { return ResourceHttp.InvalidBody("Provide a valid failure status request."); }
    }
}
