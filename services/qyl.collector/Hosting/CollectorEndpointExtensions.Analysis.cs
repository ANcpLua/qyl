using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Qyl.Collector.Analysis;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

internal static partial class CollectorEndpointExtensions
{
    private static readonly FrozenDictionary<string, (JsonTypeInfo Input, JsonTypeInfo Output)> s_readAnalysisContracts =
        new Dictionary<string, (JsonTypeInfo Input, JsonTypeInfo Output)>(StringComparer.Ordinal)
        {
            ["triage"] = (AnalysisContractJsonContext.Default.TriageInput, AnalysisContractJsonContext.Default.TriageOutput),
            ["get_failure_context"] = (AnalysisContractJsonContext.Default.GetFailureContextInput, AnalysisContractJsonContext.Default.GetFailureContextOutput),
            ["compare_groups"] = (AnalysisContractJsonContext.Default.CompareGroupsInput, AnalysisContractJsonContext.Default.CompareGroupsOutput),
            ["compare_traces"] = (AnalysisContractJsonContext.Default.CompareTracesInput, AnalysisContractJsonContext.Default.CompareTracesOutput),
            ["verify_fix"] = (AnalysisContractJsonContext.Default.VerifyFixInput, AnalysisContractJsonContext.Default.VerifyFixOutput),
            ["aggregate_telemetry"] = (AnalysisContractJsonContext.Default.AggregateTelemetryInput, AnalysisContractJsonContext.Default.AggregateTelemetryOutput),
            ["discover_attributes"] = (AnalysisContractJsonContext.Default.DiscoverAttributesInput, AnalysisContractJsonContext.Default.DiscoverAttributesOutput),
            ["get_agent_run"] = (AnalysisContractJsonContext.Default.GetAgentRunInput, AnalysisContractJsonContext.Default.GetAgentRunOutput),
            ["list_releases"] = (AnalysisContractJsonContext.Default.ListReleasesInput, AnalysisContractJsonContext.Default.ListReleasesOutput),
            ["get_service_map"] = (AnalysisContractJsonContext.Default.GetServiceMapInput, AnalysisContractJsonContext.Default.GetServiceMapOutput),
            ["check_telemetry_health"] = (AnalysisContractJsonContext.Default.CheckTelemetryHealthInput, AnalysisContractJsonContext.Default.CheckTelemetryHealthOutput),
            ["get_mcp_stats"] = (AnalysisContractJsonContext.Default.GetMcpStatsInput, AnalysisContractJsonContext.Default.GetMcpStatsOutput),
            ["list_sessions"] = (AnalysisContractJsonContext.Default.ListSessionsInput, AnalysisContractJsonContext.Default.ListSessionsOutput),
            ["list_traces"] = (AnalysisContractJsonContext.Default.ListTracesInput, AnalysisContractJsonContext.Default.ListTracesOutput),
            ["get_trace"] = (AnalysisContractJsonContext.Default.GetTraceInput, AnalysisContractJsonContext.Default.GetTraceOutput),
            ["search_logs"] = (AnalysisContractJsonContext.Default.SearchLogsInput, AnalysisContractJsonContext.Default.SearchLogsOutput),
            ["list_metrics"] = (AnalysisContractJsonContext.Default.ListMetricsInput, AnalysisContractJsonContext.Default.ListMetricsOutput),
            ["query_metric"] = (AnalysisContractJsonContext.Default.QueryMetricInput, AnalysisContractJsonContext.Default.QueryMetricOutput),
            ["ci_log"] = (AnalysisContractJsonContext.Default.CiLogInput, AnalysisContractJsonContext.Default.CiLogOutput),
            ["list_saved_queries"] = (AnalysisContractJsonContext.Default.ListSavedQueriesInput, AnalysisContractJsonContext.Default.ListSavedQueriesOutput),
            ["run_saved_query"] = (AnalysisContractJsonContext.Default.RunSavedQueryInput, AnalysisContractJsonContext.Default.RunSavedQueryOutput),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static Task<IResult> AnalyzeReadEndpointAsync(HttpContext context, IQylStore store, TimeProvider time, CancellationToken ct)
    {
        var endpoint = context.GetEndpoint() ?? throw new InvalidOperationException("An analysis route requires an endpoint.");
        var operation = endpoint.Metadata.GetRequiredMetadata<IEndpointNameMetadata>().EndpointName;
        return AnalyzeAsync(operation, context, store, time, ct);
    }

    private static AnalysisHandles CreateAnalysisHandles(HttpContext context, string project) =>
        context.RequestServices.GetRequiredService<AnalysisSigningKey>().ForProject(project);
    private static async Task<IResult> AnalyzeAsync(string operation, HttpContext context, IQylStore store, TimeProvider time, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(55));
        try
        {
            var node = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: deadline.Token).ConfigureAwait(false);
            if (node is not JsonObject input) return ResourceHttp.InvalidBody("The request body must be a JSON object.");
            ValidateAnalysisInput(operation, input);
            var project = ResolveProjectScope(context);
            var now = time.GetUtcNow(); var handles = CreateAnalysisHandles(context, project);
            if (operation == "query_metric" && J.Bool(input, "raw"))
                return AnalysisResult(operation, await RawMetricPointsAsync(input, store, project, handles, now, deadline.Token));
            var readInput = input;
            if (operation == "run_saved_query" && J.String(input, "id") is { } savedId)
            {
                if (await store.GetSavedQueryAsync(project, savedId, deadline.Token).ConfigureAwait(false) is not { } saved)
                return ContractErrorResults.NotFound("saved_query", savedId, $"Saved query {savedId} was not found in this project; list_saved_queries lists the saved queries.");
                readInput = JsonNode.Parse(saved.FilterJson)!.AsObject();
                foreach (var pair in input) readInput[pair.Key] = pair.Value?.DeepClone();
            }
            var query = AnalysisReadPlan.Resolve(operation, readInput, now, handles, project);
            var snapshot = await store.ReadAnalysisSnapshotAsync(project, query, deadline.Token).ConfigureAwait(false);
            var result = new AnalysisEngine(snapshot, project, handles, now, deadline.Token).Run(operation, input);
            // The generated contract validator is applied below before every result leaves this boundary.
            try { return AnalysisResult(operation, result); }
            catch (JsonException) { return ContractErrorResults.InternalServerError("analysis.response_invalid"); }
        }
        catch (MetricQueryConflictException conflict) { return ContractErrorResults.Conflict("metric", conflict.Message); }
        catch (AnalysisInputException invalid) { return AnalysisError(invalid); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return ContractErrorResults.AnalysisDeadlineExceeded(); }
        catch (JsonException invalid)
        {
            var allowed = AllowedAt(AnalysisContract(operation).Input, invalid.Path);
            return ContractErrorResults.Validation(invalid.Path is { Length: > 2 } path ? path[2..] : "body", "The analysis input does not match the generated contract.", "analysis.contract_invalid",
                allowed: allowed, hint: allowed is null ? "Compare the field with the tool's input schema." : "Send one of the allowed values.");
        }
    }

    // The wire names a contract enum accepts at the JSON path of a rejected input, when the property there is one.
    private static string[]? AllowedAt(JsonTypeInfo input, string? path)
    {
        if (path is null) return null;
        var type = input;
        foreach (var segment in path.TrimStart('$').Split(['.', '['], StringSplitOptions.RemoveEmptyEntries).Select(static s => s.Trim(']', '\'')).Where(static s => !s.All(char.IsAsciiDigit)))
        {
            if (type.Properties.FirstOrDefault(p => p.Name == segment) is not { } property) return null;
            var next = type.Options.GetTypeInfo(property.PropertyType);
            type = next.ElementType is { } element ? type.Options.GetTypeInfo(element) : next;
        }
        var enumType = Nullable.GetUnderlyingType(type.Type) ?? type.Type;
        if (!enumType.IsEnum) return null;
        var names = type.Options.GetTypeInfo(enumType);
        return [.. Enum.GetValuesAsUnderlyingType(enumType).Cast<object>().Select(value => JsonSerializer.Serialize(Enum.ToObject(enumType, value), names).Trim('"'))];
    }
    // A *.not_found code answers 404 naming the missing resource; any other rejected argument 400 with its
    // field, the values it accepts and a hint.
    internal static IResult AnalysisError(AnalysisInputException invalid) => invalid.Code.EndsWith(".not_found", StringComparison.Ordinal)
        ? ContractErrorResults.NotFound(invalid.Code[..^".not_found".Length], invalid.Missing, invalid.Description)
        : ContractErrorResults.Validation(invalid.Field, invalid.Description, invalid.Code, allowed: invalid.Allowed, hint: invalid.Hint);

    private static (JsonTypeInfo Input, JsonTypeInfo Output) AnalysisContract(string operation)
    {
        if (operation == "update_failure_status")
            return (AnalysisContractJsonContext.Default.UpdateFailureStatusInput, AnalysisContractJsonContext.Default.UpdateFailureStatusOutput);
        return s_readAnalysisContracts.TryGetValue(operation, out var contract)
            ? contract
            : throw new InvalidOperationException("Unknown analysis contract.");
    }

    private static void ValidateAnalysisInput(string operation, JsonObject input) =>
        _ = JsonSerializer.Deserialize(input.ToJsonString(), AnalysisContract(operation).Input);

    private static IResult AnalysisResult(string operation, JsonObject result)
    {
        var typeInfo = AnalysisContract(operation).Output;
        var typed = JsonSerializer.Deserialize(result.ToJsonString(), typeInfo)
            ?? throw new JsonException("Analysis result is empty.");
        return Results.Json(typed, typeInfo);
    }

    private static async Task<IResult> UpdateFailureStatusAsync(HttpContext context, [FromRoute(Name = "failure_id")] string failureId, IQylStore store, TimeProvider time, CancellationToken ct)
    {
        try
        {
            if (ResourceHttp.ReadIfMatch(context.Request, out var condition) is { } error) return error;
            if (await JsonNode.ParseAsync(context.Request.Body, cancellationToken: ct).ConfigureAwait(false) as JsonObject is not { } body) return ResourceHttp.InvalidBody("The request body must be a JSON object.");
            var status = J.String(body, "status");
            if (status is not ("open" or "resolved" or "ignored")) return ContractErrorResults.Validation("status", "Choose open, resolved or ignored.", "status.invalid");
            if (body["ignore_until"] is not null && status != "ignored") return ContractErrorResults.Validation("ignore_until", "ignore_until is valid only with status=ignored.", "ignore_until.invalid");
            if (body["assignee"] is not null && body["assignee"]!.GetValueKind() != JsonValueKind.String) return ContractErrorResults.Validation("assignee", "assignee must be a string.", "assignee.type");
            if (J.String(body, "assignee")?.EnumerateRunes().Count() > 256) return ContractErrorResults.Validation("assignee", "Use at most 256 characters.", "assignee.length");
            var ignoreUntil = J.Date(body, "ignore_until");
            body = J.O(("status", status), ("assignee", J.String(body, "assignee")), ("ignore_until", ignoreUntil?.ToString("O")));
            var project = ResolveProjectScope(context); var snapshot = await store.ReadAnalysisSnapshotAsync(project, new AnalysisReadQuery { ErrorsOnly = true }, ct).ConfigureAwait(false);
            var engine = new AnalysisEngine(snapshot, project, CreateAnalysisHandles(context, project), time.GetUtcNow());
            if (!engine.OwnsFailure(failureId)) return snapshot.ScanComplete ? ContractErrorResults.NotFound("failure", failureId) : ContractErrorResults.ServiceUnavailable("analysis.scan_budget_exceeded");
            var existing = snapshot.State.FirstOrDefault(s => s.StateKey == failureId); var current = existing?.Etag ?? '"' + failureId + '"';
            if (!condition.Matches(current)) return ContractErrorResults.PreconditionFailed(current);
            body["updated_at"] = time.GetUtcNow().ToString("O");
            var row = new AnalysisStateRow { ProjectId = project, StateKey = failureId, ValueJson = body.ToJsonString(), Etag = ResourceHttp.NewEtag(), UpdatedAtUnixNano = ResourceHttp.NowUnixNano(time) };
            var outcome = await store.WriteAnalysisStateAsync(row, existing?.Etag, ct).ConfigureAwait(false);
            if (outcome.Status == ResourceWriteStatus.PreconditionFailed) return ContractErrorResults.PreconditionFailed(outcome.CurrentEtag);
            var updated = snapshot with { State = snapshot.State.Where(s => s.StateKey != failureId).Append(row).ToArray() };
            var failure = new AnalysisEngine(updated, project, CreateAnalysisHandles(context, project), time.GetUtcNow()).FailureSummary(failureId);
            context.Response.Headers.ETag = row.Etag;
            try { return AnalysisResult("update_failure_status", J.O(("failure", failure))); }
            catch (JsonException) { return ContractErrorResults.InternalServerError("analysis.response_invalid"); }
        }
        catch (MetricQueryConflictException conflict) { return ContractErrorResults.Conflict("metric", conflict.Message); }
        catch (AnalysisInputException invalid) { return AnalysisError(invalid); }
        catch (JsonException) { return ResourceHttp.InvalidBody("Provide a valid failure status request."); }
    }
}
