using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Mcp.Tools;
using Qyl.Collector.Analysis;
using Qyl.Collector.Storage;
using ContractRanges = System.Collections.Generic.IReadOnlyList<(string Name, double? Minimum, double? Maximum, double? Default)>;

namespace Qyl.Collector.Hosting;

internal static partial class CollectorEndpointExtensions
{
    private static readonly FrozenDictionary<string, (JsonTypeInfo Input, JsonTypeInfo Output, ContractRanges Ranges)> s_readAnalysisContracts =
        new Dictionary<string, (JsonTypeInfo Input, JsonTypeInfo Output, ContractRanges Ranges)>(StringComparer.Ordinal)
        {
            ["triage"] = (AnalysisContractJsonContext.Default.TriageInput, AnalysisContractJsonContext.Default.TriageOutput, TriageInput.Ranges),
            ["get_failure_context"] = (AnalysisContractJsonContext.Default.GetFailureContextInput, AnalysisContractJsonContext.Default.GetFailureContextOutput, GetFailureContextInput.Ranges),
            ["compare_groups"] = (AnalysisContractJsonContext.Default.CompareGroupsInput, AnalysisContractJsonContext.Default.CompareGroupsOutput, CompareGroupsInput.Ranges),
            ["compare_traces"] = (AnalysisContractJsonContext.Default.CompareTracesInput, AnalysisContractJsonContext.Default.CompareTracesOutput, CompareTracesInput.Ranges),
            ["verify_fix"] = (AnalysisContractJsonContext.Default.VerifyFixInput, AnalysisContractJsonContext.Default.VerifyFixOutput, VerifyFixInput.Ranges),
            ["aggregate_telemetry"] = (AnalysisContractJsonContext.Default.AggregateTelemetryInput, AnalysisContractJsonContext.Default.AggregateTelemetryOutput, AggregateTelemetryInput.Ranges),
            ["discover_attributes"] = (AnalysisContractJsonContext.Default.DiscoverAttributesInput, AnalysisContractJsonContext.Default.DiscoverAttributesOutput, DiscoverAttributesInput.Ranges),
            ["get_agent_run"] = (AnalysisContractJsonContext.Default.GetAgentRunInput, AnalysisContractJsonContext.Default.GetAgentRunOutput, GetAgentRunInput.Ranges),
            ["list_releases"] = (AnalysisContractJsonContext.Default.ListReleasesInput, AnalysisContractJsonContext.Default.ListReleasesOutput, ListReleasesInput.Ranges),
            ["get_service_map"] = (AnalysisContractJsonContext.Default.GetServiceMapInput, AnalysisContractJsonContext.Default.GetServiceMapOutput, GetServiceMapInput.Ranges),
            ["check_telemetry_health"] = (AnalysisContractJsonContext.Default.CheckTelemetryHealthInput, AnalysisContractJsonContext.Default.CheckTelemetryHealthOutput, CheckTelemetryHealthInput.Ranges),
            ["get_mcp_stats"] = (AnalysisContractJsonContext.Default.GetMcpStatsInput, AnalysisContractJsonContext.Default.GetMcpStatsOutput, GetMcpStatsInput.Ranges),
            ["list_sessions"] = (AnalysisContractJsonContext.Default.ListSessionsInput, AnalysisContractJsonContext.Default.ListSessionsOutput, ListSessionsInput.Ranges),
            ["list_traces"] = (AnalysisContractJsonContext.Default.ListTracesInput, AnalysisContractJsonContext.Default.ListTracesOutput, ListTracesInput.Ranges),
            ["get_trace"] = (AnalysisContractJsonContext.Default.GetTraceInput, AnalysisContractJsonContext.Default.GetTraceOutput, GetTraceInput.Ranges),
            ["search_logs"] = (AnalysisContractJsonContext.Default.SearchLogsInput, AnalysisContractJsonContext.Default.SearchLogsOutput, SearchLogsInput.Ranges),
            ["list_metrics"] = (AnalysisContractJsonContext.Default.ListMetricsInput, AnalysisContractJsonContext.Default.ListMetricsOutput, ListMetricsInput.Ranges),
            ["query_metric"] = (AnalysisContractJsonContext.Default.QueryMetricInput, AnalysisContractJsonContext.Default.QueryMetricOutput, QueryMetricInput.Ranges),
            ["ci_log"] = (AnalysisContractJsonContext.Default.CiLogInput, AnalysisContractJsonContext.Default.CiLogOutput, CiLogInput.Ranges),
            ["run_saved_query"] = (AnalysisContractJsonContext.Default.RunSavedQueryInput, AnalysisContractJsonContext.Default.RunSavedQueryOutput, RunSavedQueryInput.Ranges),
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
                    return AnalysisError(AnalysisEngine.NoSavedQuery(savedId));
                readInput = AnalysisEngine.SavedQueryInput(saved, input);
            }
            if (operation == "get_failure_context" && J.String(input, "failure_id") is { } failureId)
            {
                // A failure is explained in the trace of its latest occurrence. Its error spans name that trace, which is
                // then read like a trace_id request, so the answer never waits on the project's whole history.
                var errorsQuery = AnalysisReadPlan.Resolve(operation, input, now, handles, project) with { ErrorsOnly = true, Tables = AnalysisTables.Spans };
                var errors = await store.ReadAnalysisSnapshotAsync(project, errorsQuery, deadline.Token).ConfigureAwait(false);
                if (new AnalysisEngine(errors, project, handles, now, deadline.Token).LatestOccurrenceTrace(failureId) is not { } trace)
                    return errors.ScanComplete ? AnalysisError(AnalysisEngine.NoFailure(failureId)) : ContractErrorResults.ServiceUnavailable("analysis.scan_budget_exceeded");
                readInput = input.DeepClone().AsObject();
                readInput["trace_id"] = trace;
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
        : ContractErrorResults.Validation(invalid.Field, invalid.Description, invalid.Code, invalid.RejectedValue, invalid.Allowed, invalid.Hint);

    private static (JsonTypeInfo Input, JsonTypeInfo Output, ContractRanges Ranges) AnalysisContract(string operation)
    {
        if (operation == "update_failure_status")
            return (AnalysisContractJsonContext.Default.UpdateFailureStatusInput, AnalysisContractJsonContext.Default.UpdateFailureStatusOutput, []);
        return s_readAnalysisContracts.TryGetValue(operation, out var contract)
            ? contract
            : throw new InvalidOperationException("Unknown analysis contract.");
    }

    private static void ValidateAnalysisInput(string operation, JsonObject input)
    {
        var contract = AnalysisContract(operation);
        _ = JsonSerializer.Deserialize(input.ToJsonString(), contract.Input);
        CheckRanges(input, contract.Ranges);
    }

    // A number the request sends outside the range the contract bounds its member to is refused naming that range; an
    // omitted member takes its default. The engine reads the members unchecked, so this is the one place a bound applies.
    // A nested member, such as baseline.offset_minutes, belongs to an object the request may omit as a whole.
    private static void CheckRanges(JsonObject input, ContractRanges ranges)
    {
        static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);
        foreach (var (path, minimum, maximum, fallback) in ranges)
        foreach (var (field, sent) in Sent(input, path))
        {
            if (sent is not JsonValue || !double.TryParse(J.Text(sent), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || value >= (minimum ?? double.NegativeInfinity) && value <= (maximum ?? double.PositiveInfinity)) continue;
            var range = (minimum, maximum) switch
            {
                ({ } low, { } high) => $"from {Text(low)} to {Text(high)}",
                ({ } low, null) => $"of at least {Text(low)}",
                _ => $"of at most {Text(maximum!.Value)}"
            };
            throw new AnalysisInputException(field, $"Use {field} {range}.", path + ".out_of_range")
            {
                Hint = path.Contains('.') ? $"Correct {field}." : fallback is { } omitted ? $"Omit {field} for {Text(omitted)}." : $"Omit {field} for the tool's default.",
                RejectedValue = J.Text(sent)
            };
        }
    }

    // The values a request sends at a contract path, each named as the MCP names it: baseline.offset_minutes, and an
    // array's elements, written [] in the path, by their index, as matchers[0].key.
    private static IEnumerable<(string Field, JsonNode Value)> Sent(JsonNode? node, string path, string field = "")
    {
        var dot = path.IndexOf('.');
        var segment = dot < 0 ? path : path[..dot];
        var elements = segment.EndsWith("[]", StringComparison.Ordinal);
        var name = elements ? segment[..^2] : segment;
        if ((node as JsonObject)?[name] is not { } member) yield break;
        var at = field.Length == 0 ? name : field + "." + name;
        var values = !elements ? [(at, member)] : member is JsonArray items ? items.Select((item, i) => ($"{at}[{i}]", item)) : [];
        foreach (var (named, value) in values)
        {
            if (value is null) continue;
            if (dot < 0) yield return (named, value);
            else foreach (var nested in Sent(value, path[(dot + 1)..], named)) yield return nested;
        }
    }

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
            var project = ResolveProjectScope(context); var snapshot = await store.ReadAnalysisSnapshotAsync(project, new AnalysisReadQuery { ErrorsOnly = true, Tables = AnalysisTables.Spans }, ct).ConfigureAwait(false);
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
