using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Mcp.Tools;
using Qyl.Collector.Analysis;
using Qyl.Collector.Storage;
using ContractChoices = System.Collections.Generic.IReadOnlyList<(string Name, System.Collections.Generic.IReadOnlyList<string> Values, System.Collections.Generic.IReadOnlyList<System.Type> Models)>;
using ContractLengths = System.Collections.Generic.IReadOnlyList<(string Name, int? Minimum, int? Maximum)>;
using ContractRanges = System.Collections.Generic.IReadOnlyList<(string Name, double? Minimum, double? Maximum, double? Default)>;

namespace Qyl.Collector.Hosting;

internal static partial class CollectorEndpointExtensions
{
    private static readonly FrozenDictionary<string, (JsonTypeInfo Input, JsonTypeInfo Output, ContractRanges Ranges, ContractLengths Lengths, ContractChoices Choices)> s_readAnalysisContracts =
        new Dictionary<string, (JsonTypeInfo Input, JsonTypeInfo Output, ContractRanges Ranges, ContractLengths Lengths, ContractChoices Choices)>(StringComparer.Ordinal)
        {
            ["triage"] = (AnalysisContractJsonContext.Default.TriageInput, AnalysisContractJsonContext.Default.TriageOutput, TriageInput.Ranges, TriageInput.Lengths, TriageInput.Choices),
            ["get_failure_context"] = (AnalysisContractJsonContext.Default.GetFailureContextInput, AnalysisContractJsonContext.Default.GetFailureContextOutput, GetFailureContextInput.Ranges, GetFailureContextInput.Lengths, GetFailureContextInput.Choices),
            ["compare_groups"] = (AnalysisContractJsonContext.Default.CompareGroupsInput, AnalysisContractJsonContext.Default.CompareGroupsOutput, CompareGroupsInput.Ranges, CompareGroupsInput.Lengths, CompareGroupsInput.Choices),
            ["compare_traces"] = (AnalysisContractJsonContext.Default.CompareTracesInput, AnalysisContractJsonContext.Default.CompareTracesOutput, CompareTracesInput.Ranges, CompareTracesInput.Lengths, CompareTracesInput.Choices),
            ["verify_fix"] = (AnalysisContractJsonContext.Default.VerifyFixInput, AnalysisContractJsonContext.Default.VerifyFixOutput, VerifyFixInput.Ranges, VerifyFixInput.Lengths, VerifyFixInput.Choices),
            ["aggregate_telemetry"] = (AnalysisContractJsonContext.Default.AggregateTelemetryInput, AnalysisContractJsonContext.Default.AggregateTelemetryOutput, AggregateTelemetryInput.Ranges, AggregateTelemetryInput.Lengths, AggregateTelemetryInput.Choices),
            ["discover_attributes"] = (AnalysisContractJsonContext.Default.DiscoverAttributesInput, AnalysisContractJsonContext.Default.DiscoverAttributesOutput, DiscoverAttributesInput.Ranges, DiscoverAttributesInput.Lengths, DiscoverAttributesInput.Choices),
            ["get_agent_run"] = (AnalysisContractJsonContext.Default.GetAgentRunInput, AnalysisContractJsonContext.Default.GetAgentRunOutput, GetAgentRunInput.Ranges, GetAgentRunInput.Lengths, GetAgentRunInput.Choices),
            ["list_releases"] = (AnalysisContractJsonContext.Default.ListReleasesInput, AnalysisContractJsonContext.Default.ListReleasesOutput, ListReleasesInput.Ranges, ListReleasesInput.Lengths, ListReleasesInput.Choices),
            ["get_service_map"] = (AnalysisContractJsonContext.Default.GetServiceMapInput, AnalysisContractJsonContext.Default.GetServiceMapOutput, GetServiceMapInput.Ranges, GetServiceMapInput.Lengths, GetServiceMapInput.Choices),
            ["check_telemetry_health"] = (AnalysisContractJsonContext.Default.CheckTelemetryHealthInput, AnalysisContractJsonContext.Default.CheckTelemetryHealthOutput, CheckTelemetryHealthInput.Ranges, CheckTelemetryHealthInput.Lengths, CheckTelemetryHealthInput.Choices),
            ["get_mcp_stats"] = (AnalysisContractJsonContext.Default.GetMcpStatsInput, AnalysisContractJsonContext.Default.GetMcpStatsOutput, GetMcpStatsInput.Ranges, GetMcpStatsInput.Lengths, GetMcpStatsInput.Choices),
            ["list_sessions"] = (AnalysisContractJsonContext.Default.ListSessionsInput, AnalysisContractJsonContext.Default.ListSessionsOutput, ListSessionsInput.Ranges, ListSessionsInput.Lengths, ListSessionsInput.Choices),
            ["list_traces"] = (AnalysisContractJsonContext.Default.ListTracesInput, AnalysisContractJsonContext.Default.ListTracesOutput, ListTracesInput.Ranges, ListTracesInput.Lengths, ListTracesInput.Choices),
            ["get_trace"] = (AnalysisContractJsonContext.Default.GetTraceInput, AnalysisContractJsonContext.Default.GetTraceOutput, GetTraceInput.Ranges, GetTraceInput.Lengths, GetTraceInput.Choices),
            ["search_logs"] = (AnalysisContractJsonContext.Default.SearchLogsInput, AnalysisContractJsonContext.Default.SearchLogsOutput, SearchLogsInput.Ranges, SearchLogsInput.Lengths, SearchLogsInput.Choices),
            ["list_metrics"] = (AnalysisContractJsonContext.Default.ListMetricsInput, AnalysisContractJsonContext.Default.ListMetricsOutput, ListMetricsInput.Ranges, ListMetricsInput.Lengths, ListMetricsInput.Choices),
            ["query_metric"] = (AnalysisContractJsonContext.Default.QueryMetricInput, AnalysisContractJsonContext.Default.QueryMetricOutput, QueryMetricInput.Ranges, QueryMetricInput.Lengths, QueryMetricInput.Choices),
            ["ci_log"] = (AnalysisContractJsonContext.Default.CiLogInput, AnalysisContractJsonContext.Default.CiLogOutput, CiLogInput.Ranges, CiLogInput.Lengths, CiLogInput.Choices),
            ["run_saved_query"] = (AnalysisContractJsonContext.Default.RunSavedQueryInput, AnalysisContractJsonContext.Default.RunSavedQueryOutput, RunSavedQueryInput.Ranges, RunSavedQueryInput.Lengths, RunSavedQueryInput.Choices),
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
        catch (JsonException) { return ResourceHttp.InvalidBody("The request body is not valid JSON."); }
    }

    // A *.not_found code answers 404 naming the missing resource; any other rejected argument 400 with its
    // field, the values it accepts and a hint.
    internal static IResult AnalysisError(AnalysisInputException invalid) => invalid.Code.EndsWith(".not_found", StringComparison.Ordinal)
        ? ContractErrorResults.NotFound(invalid.Code[..^".not_found".Length], invalid.Missing, invalid.Description)
        : ContractErrorResults.Validation(invalid.Field, invalid.Description, invalid.Code, invalid.RejectedValue, invalid.Allowed, invalid.Hint);

    private static (JsonTypeInfo Input, JsonTypeInfo Output, ContractRanges Ranges, ContractLengths Lengths, ContractChoices Choices) AnalysisContract(string operation)
    {
        if (operation == "update_failure_status")
            return (AnalysisContractJsonContext.Default.UpdateFailureStatusInput, AnalysisContractJsonContext.Default.UpdateFailureStatusOutput, [], [], []);
        return s_readAnalysisContracts.TryGetValue(operation, out var contract)
            ? contract
            : throw new InvalidOperationException("Unknown analysis contract.");
    }

    private static void ValidateAnalysisInput(string operation, JsonObject input)
    {
        var contract = AnalysisContract(operation);
        CheckContract(input, contract.Input, contract.Ranges, contract.Lengths, contract.Choices);
    }

    // A request body held to its contract: a member with a closed set answers with its values first, as the MCP's input
    // schema does, then the body's type, its ranges and its lengths.
    private static void CheckContract(JsonObject input, JsonTypeInfo type, ContractRanges ranges, ContractLengths lengths, ContractChoices choices)
    {
        CheckChoices(input, choices, type.Options);
        ReadAsContract(input, type);
        CheckRanges(input, ranges);
        CheckLengths(input, lengths);
    }

    // A saved query's or an annotation's body read as its contract model once the ranges and lengths its contract declares
    // hold, checked as an analysis request's are and refused naming the field by its path.
    private static async Task<(T? Value, IResult? Error)> ReadResourceBodyAsync<T>(HttpRequest request, JsonTypeInfo<T> type,
        ContractRanges ranges, ContractLengths lengths, CancellationToken ct) where T : class
    {
        try
        {
            if (await JsonNode.ParseAsync(request.Body, cancellationToken: ct).ConfigureAwait(false) is not JsonObject body)
                return (null, ResourceHttp.InvalidBody("The request body must be a JSON object."));
            CheckRanges(body, ranges);
            CheckLengths(body, lengths);
            return (body.Deserialize(type), null);
        }
        catch (AnalysisInputException invalid) { return (null, AnalysisError(invalid)); }
        // A missing discriminator or an unknown kind or signal surfaces as NotSupportedException. The serializer's own
        // message names internal types, so the response carries a fixed one.
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return (null, ResourceHttp.InvalidBody(
                "The request body is not valid JSON for this operation's contract model, or a required member or discriminator is missing or unknown."));
        }
    }

    // A request read as its contract type, the way the MCP's input schema reads the same arguments: a member the type
    // does not declare, a required one it leaves out and a value of another JSON type, null included, are refused naming
    // the field by its path, with the members the object accepts, the values an enumeration takes or the type the field
    // expects. A union C# reads as a bare object is left to CheckChoices, which reads an object as its form's model.
    private static void ReadAsContract(JsonNode? node, JsonTypeInfo type, string field = "")
    {
        var clr = Nullable.GetUnderlyingType(type.Type) ?? type.Type;
        if (clr == typeof(object) || type.PolymorphismOptions is not null) return;
        switch (type.Kind)
        {
            case JsonTypeInfoKind.Object when node is JsonObject members:
                foreach (var (name, value) in members)
                {
                    var at = field.Length == 0 ? name : $"{field}.{name}";
                    var property = type.Properties.FirstOrDefault(p => p.Name == name);
                    if (property is null)
                    {
                        var accepted = type.Properties.Select(static p => p.Name).ToArray();
                        throw new AnalysisInputException(at, $"is not accepted here; accepted: {string.Join(", ", accepted)}", Rule(at) + ".unknown")
                            { Allowed = accepted, Hint = $"Replace {at} with one of the accepted arguments or drop it." };
                    }
                    ReadAsContract(value, type.Options.GetTypeInfo(property.PropertyType), at);
                }
                if (type.Properties.FirstOrDefault(p => p.IsRequired && !members.ContainsKey(p.Name)) is { } missing)
                {
                    var at = field.Length == 0 ? missing.Name : $"{field}.{missing.Name}";
                    throw new AnalysisInputException(at, "is required", Rule(at) + ".required") { Hint = $"Send {at}." };
                }
                return;
            case JsonTypeInfoKind.Enumerable when node is JsonArray items:
                var element = type.Options.GetTypeInfo(type.ElementType!);
                for (var i = 0; i < items.Count; i++) ReadAsContract(items[i], element, $"{field}[{i}]");
                return;
            case JsonTypeInfoKind.None when node is JsonValue value:
                ReadValue(value, type, clr, field);
                return;
        }
        // An object where the contract takes another type, a scalar where it takes an object or an array, or null.
        throw new AnalysisInputException(field, $"must be {JsonType(type, clr)}", Rule(field) + ".type") { Hint = $"Send {field} as {JsonTypeArticle(type, clr)}." };
    }

    // A JSON value of a member C# reads as a string, a number, a boolean, a time or an enumeration.
    private static void ReadValue(JsonValue value, JsonTypeInfo type, Type clr, string field)
    {
        var kind = value.GetValueKind();
        if (clr.IsEnum)
        {
            var names = type.Options.GetTypeInfo(clr);
            string[] allowed = [.. Enum.GetValuesAsUnderlyingType(clr).Cast<object>().Select(member => JsonSerializer.Serialize(Enum.ToObject(clr, member), names).Trim('"'))];
            if (kind == JsonValueKind.String && allowed.Contains(value.GetValue<string>(), StringComparer.Ordinal)) return;
            throw new AnalysisInputException(field, $"must be one of: {string.Join(", ", allowed)}", Rule(field) + ".invalid") { Allowed = allowed, Hint = "Send one of the allowed values." };
        }
        if (clr == typeof(DateTimeOffset) && kind == JsonValueKind.String)
        {
            if (ContractDateTime.IsValid(value.GetValue<string>())) return;
            throw new AnalysisInputException(field, "must match format \"date-time\"", Rule(field) + ".format") { Hint = "Send a time such as 2026-10-19T12:00:00Z." };
        }
        if (s_integers.TryGetValue(clr, out var range) && kind == JsonValueKind.Number)
        {
            var number = double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture);
            if (number != Math.Floor(number)) throw new AnalysisInputException(field, "must be integer", Rule(field) + ".type") { Hint = $"Send {field} as an integer." };
            if (number < range.Minimum || number > range.Maximum)
                throw new AnalysisInputException(field, number > range.Maximum ? $"must be <= {range.Maximum.ToString(CultureInfo.InvariantCulture)}" : $"must be >= {range.Minimum.ToString(CultureInfo.InvariantCulture)}", Rule(field) + ".out_of_range")
                    { Hint = $"Correct {field}." };
            return;
        }
        var fits = JsonType(type, clr) switch
        {
            "string" => kind == JsonValueKind.String,
            "number" => kind == JsonValueKind.Number,
            "boolean" => kind is JsonValueKind.True or JsonValueKind.False,
            "integer" => false,
            _ => true
        };
        if (!fits) throw new AnalysisInputException(field, $"must be {JsonType(type, clr)}", Rule(field) + ".type") { Hint = $"Send {field} as {JsonTypeArticle(type, clr)}." };
    }

    // The integer types a contract member maps to, with the range each holds.
    private static readonly FrozenDictionary<Type, (double Minimum, double Maximum)> s_integers = new Dictionary<Type, (double Minimum, double Maximum)>
    {
        [typeof(int)] = (int.MinValue, int.MaxValue), [typeof(long)] = (long.MinValue, long.MaxValue), [typeof(short)] = (short.MinValue, short.MaxValue),
        [typeof(uint)] = (uint.MinValue, uint.MaxValue), [typeof(ulong)] = (ulong.MinValue, ulong.MaxValue), [typeof(ushort)] = (ushort.MinValue, ushort.MaxValue),
        [typeof(byte)] = (byte.MinValue, byte.MaxValue), [typeof(sbyte)] = (sbyte.MinValue, sbyte.MaxValue)
    }.ToFrozenDictionary();

    // The JSON Schema type a member's C# type stands for, as the MCP's input schema names it.
    private static string JsonType(JsonTypeInfo type, Type clr) => type.Kind switch
    {
        JsonTypeInfoKind.Object or JsonTypeInfoKind.Dictionary => "object",
        JsonTypeInfoKind.Enumerable => "array",
        _ when clr == typeof(bool) => "boolean",
        _ when s_integers.ContainsKey(clr) => "integer",
        _ when clr == typeof(double) || clr == typeof(float) || clr == typeof(decimal) => "number",
        _ when clr == typeof(string) || clr == typeof(DateTimeOffset) || clr.IsEnum => "string",
        _ => "value"
    };

    private static string JsonTypeArticle(JsonTypeInfo type, Type clr) => JsonType(type, clr) switch
    {
        "integer" or "object" or "array" => "an " + JsonType(type, clr),
        var name => "a " + name
    };

    // The rule a refusal names: the field's path with an array's indexes written [], as Ranges and Choices write it.
    private static string Rule(string field) => ArrayIndex().Replace(field, "[]");

    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex ArrayIndex();

    // A value of a member C# reads as a bare string or object, such as a baseline or a matcher's value, is refused when it
    // takes none of the forms the contract gives the member, with the values it accepts when they form a closed set: its
    // strings, its object forms, written {a, b}, and its JSON types, written <string>. An object is read as the model of
    // the form it is meant for, as the MCP's input schema resolves the union: the one form that declares every member it
    // sends, else the only object form there is, else the one of several such forms it satisfies.
    private static void CheckChoices(JsonObject input, ContractChoices choices, JsonSerializerOptions options)
    {
        foreach (var (path, forms, models) in choices)
        foreach (var (field, sent) in Sent(input, path))
        {
            if (sent is JsonObject members && models.Count > 0)
            {
                JsonTypeInfo[] objects = [.. models.Select(options.GetTypeInfo)];
                JsonTypeInfo[] meant = [.. objects.Where(model => members.All(member => model.Properties.Any(p => p.Name == member.Key)))];
                if (meant.Length == 0 && objects.Length == 1) meant = objects;
                if (meant.Length == 1) { ReadAsContract(members, meant[0], field); continue; }
                if (meant.Any(model => Reads(members, model, field))) continue;
            }
            else if (Takes(forms, sent)) continue;
            string[] types = [.. forms.Where(static form => form.StartsWith('<')).Select(static form => form.Trim('<', '>'))];
            throw new AnalysisInputException(field, $"must be one of: {string.Join(", ", forms.Select(static form => form.Trim('<', '>')))}", path + ".invalid")
            {
                Allowed = types.Length == 0 ? forms : null,
                Hint = types.Length == 0 ? "Send one of the allowed values." : $"Send {field} as {string.Join(" or ", types.Select(static type => (type is "integer" ? "an " : "a ") + type))}."
            };
        }
    }

    private static bool Reads(JsonObject members, JsonTypeInfo model, string field)
    {
        try { ReadAsContract(members, model, field); return true; }
        catch (AnalysisInputException) { return false; }
    }

    private static bool Takes(IReadOnlyList<string> forms, JsonNode? sent) => sent switch
    {
        JsonObject => forms.Any(static form => form.StartsWith('{')),
        JsonValue value => value.GetValueKind() switch
        {
            JsonValueKind.String => forms.Contains("<string>") || forms.Any(form => form[0] is not ('{' or '<') && form == value.GetValue<string>()),
            JsonValueKind.Number => forms.Contains("<number>") || forms.Contains("<integer>") && double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture) % 1 == 0,
            JsonValueKind.True or JsonValueKind.False => forms.Contains("<boolean>"),
            _ => false
        },
        _ => false
    };

    // A number the request sends outside the range the contract bounds its member to is refused naming that range; an
    // omitted member takes its default. The engine reads the members unchecked, so this is the one place a bound applies.
    // A nested member, such as baseline.offset_minutes, belongs to an object the request may omit as a whole.
    private static void CheckRanges(JsonObject input, ContractRanges ranges)
    {
        static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);
        foreach (var (path, minimum, maximum, fallback) in ranges)
        foreach (var (field, sent) in Sent(input, path))
        {
            if (sent is not JsonValue number || number.GetValueKind() != JsonValueKind.Number)
                throw new AnalysisInputException(field, "must be number", path + ".type") { Hint = $"Send {field} as a number." };
            var value = double.Parse(sent.ToJsonString(), CultureInfo.InvariantCulture);
            if (value >= (minimum ?? double.NegativeInfinity) && value <= (maximum ?? double.PositiveInfinity)) continue;
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

    // A string or an array the request sends outside the length the contract bounds its member to is refused naming that
    // length: a string counts its code points, as JSON Schema does, and an array its items. This is the one place a length
    // or an item count of a request body applies.
    private static void CheckLengths(JsonObject input, ContractLengths lengths)
    {
        static string Count(int count, string unit) => $"{count} {unit}{(count == 1 ? "" : "s")}";
        foreach (var (path, minimum, maximum) in lengths)
        foreach (var (field, sent) in Sent(input, path))
        {
            var (length, unit) = sent switch
            {
                JsonArray items => (items.Count, "item"),
                JsonValue value when value.GetValueKind() == JsonValueKind.String => (value.GetValue<string>().EnumerateRunes().Count(), "character"),
                _ => (-1, "")
            };
            if (length < 0 || length >= (minimum ?? 0) && length <= (maximum ?? int.MaxValue)) continue;
            var range = (minimum, maximum) switch
            {
                ({ } low, { } high) when low == high => $"exactly {Count(high, unit)}",
                ({ } low, { } high) => $"{low} to {Count(high, unit)}",
                ({ } low, null) => $"at least {Count(low, unit)}",
                _ => $"at most {Count(maximum!.Value, unit)}"
            };
            throw new AnalysisInputException(field, unit == "item" ? $"Use {field} with {range}." : $"Use {field} of {range}.", path + ".length") { Hint = $"Correct {field}." };
        }
    }

    // The values a request sends at a contract path, null included, each named as the MCP names it: baseline.offset_minutes,
    // and an array's elements, written [] in the path, by their index, as matchers[0].key.
    private static IEnumerable<(string Field, JsonNode? Value)> Sent(JsonNode? node, string path, string field = "")
    {
        var dot = path.IndexOf('.');
        var segment = dot < 0 ? path : path[..dot];
        var elements = segment.EndsWith("[]", StringComparison.Ordinal);
        var name = elements ? segment[..^2] : segment;
        if (node is not JsonObject members || !members.TryGetPropertyValue(name, out var member)) yield break;
        var at = field.Length == 0 ? name : field + "." + name;
        IEnumerable<(string, JsonNode?)> values = !elements ? [(at, member)] : member is JsonArray items ? items.Select((item, i) => ($"{at}[{i}]", item)) : [];
        foreach (var (named, value) in values)
        {
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
            CheckLengths(body, Api.Contracts.UpdateFailureStatusRequest.Lengths);
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
