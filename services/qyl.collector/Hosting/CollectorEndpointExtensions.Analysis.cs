using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Mcp.Tools;
using Qyl.Collector.Analysis;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

internal static partial class CollectorEndpointExtensions
{
    private static readonly FrozenDictionary<string, (JsonTypeInfo Input, JsonTypeInfo Output, ContractRules Rules)> s_readAnalysisContracts =
        new Dictionary<string, (JsonTypeInfo Input, JsonTypeInfo Output, ContractRules Rules)>(StringComparer.Ordinal)
        {
            ["triage"] = (AnalysisContractJsonContext.Default.TriageInput, AnalysisContractJsonContext.Default.TriageOutput, new(TriageInput.Ranges, TriageInput.Lengths, TriageInput.Patterns, TriageInput.Choices)),
            ["get_failure_context"] = (AnalysisContractJsonContext.Default.GetFailureContextInput, AnalysisContractJsonContext.Default.GetFailureContextOutput, new(GetFailureContextInput.Ranges, GetFailureContextInput.Lengths, GetFailureContextInput.Patterns, GetFailureContextInput.Choices)),
            ["compare_groups"] = (AnalysisContractJsonContext.Default.CompareGroupsInput, AnalysisContractJsonContext.Default.CompareGroupsOutput, new(CompareGroupsInput.Ranges, CompareGroupsInput.Lengths, CompareGroupsInput.Patterns, CompareGroupsInput.Choices)),
            ["compare_traces"] = (AnalysisContractJsonContext.Default.CompareTracesInput, AnalysisContractJsonContext.Default.CompareTracesOutput, new(CompareTracesInput.Ranges, CompareTracesInput.Lengths, CompareTracesInput.Patterns, CompareTracesInput.Choices)),
            ["verify_fix"] = (AnalysisContractJsonContext.Default.VerifyFixInput, AnalysisContractJsonContext.Default.VerifyFixOutput, new(VerifyFixInput.Ranges, VerifyFixInput.Lengths, VerifyFixInput.Patterns, VerifyFixInput.Choices)),
            ["aggregate_telemetry"] = (AnalysisContractJsonContext.Default.AggregateTelemetryInput, AnalysisContractJsonContext.Default.AggregateTelemetryOutput, new(AggregateTelemetryInput.Ranges, AggregateTelemetryInput.Lengths, AggregateTelemetryInput.Patterns, AggregateTelemetryInput.Choices)),
            ["discover_attributes"] = (AnalysisContractJsonContext.Default.DiscoverAttributesInput, AnalysisContractJsonContext.Default.DiscoverAttributesOutput, new(DiscoverAttributesInput.Ranges, DiscoverAttributesInput.Lengths, DiscoverAttributesInput.Patterns, DiscoverAttributesInput.Choices)),
            ["get_agent_run"] = (AnalysisContractJsonContext.Default.GetAgentRunInput, AnalysisContractJsonContext.Default.GetAgentRunOutput, new(GetAgentRunInput.Ranges, GetAgentRunInput.Lengths, GetAgentRunInput.Patterns, GetAgentRunInput.Choices)),
            ["list_releases"] = (AnalysisContractJsonContext.Default.ListReleasesInput, AnalysisContractJsonContext.Default.ListReleasesOutput, new(ListReleasesInput.Ranges, ListReleasesInput.Lengths, ListReleasesInput.Patterns, ListReleasesInput.Choices)),
            ["get_service_map"] = (AnalysisContractJsonContext.Default.GetServiceMapInput, AnalysisContractJsonContext.Default.GetServiceMapOutput, new(GetServiceMapInput.Ranges, GetServiceMapInput.Lengths, GetServiceMapInput.Patterns, GetServiceMapInput.Choices)),
            ["check_telemetry_health"] = (AnalysisContractJsonContext.Default.CheckTelemetryHealthInput, AnalysisContractJsonContext.Default.CheckTelemetryHealthOutput, new(CheckTelemetryHealthInput.Ranges, CheckTelemetryHealthInput.Lengths, CheckTelemetryHealthInput.Patterns, CheckTelemetryHealthInput.Choices)),
            ["get_mcp_stats"] = (AnalysisContractJsonContext.Default.GetMcpStatsInput, AnalysisContractJsonContext.Default.GetMcpStatsOutput, new(GetMcpStatsInput.Ranges, GetMcpStatsInput.Lengths, GetMcpStatsInput.Patterns, GetMcpStatsInput.Choices)),
            ["list_sessions"] = (AnalysisContractJsonContext.Default.ListSessionsInput, AnalysisContractJsonContext.Default.ListSessionsOutput, new(ListSessionsInput.Ranges, ListSessionsInput.Lengths, ListSessionsInput.Patterns, ListSessionsInput.Choices)),
            ["list_traces"] = (AnalysisContractJsonContext.Default.ListTracesInput, AnalysisContractJsonContext.Default.ListTracesOutput, new(ListTracesInput.Ranges, ListTracesInput.Lengths, ListTracesInput.Patterns, ListTracesInput.Choices)),
            ["get_trace"] = (AnalysisContractJsonContext.Default.GetTraceInput, AnalysisContractJsonContext.Default.GetTraceOutput, new(GetTraceInput.Ranges, GetTraceInput.Lengths, GetTraceInput.Patterns, GetTraceInput.Choices)),
            ["search_logs"] = (AnalysisContractJsonContext.Default.SearchLogsInput, AnalysisContractJsonContext.Default.SearchLogsOutput, new(SearchLogsInput.Ranges, SearchLogsInput.Lengths, SearchLogsInput.Patterns, SearchLogsInput.Choices)),
            ["list_metrics"] = (AnalysisContractJsonContext.Default.ListMetricsInput, AnalysisContractJsonContext.Default.ListMetricsOutput, new(ListMetricsInput.Ranges, ListMetricsInput.Lengths, ListMetricsInput.Patterns, ListMetricsInput.Choices)),
            ["query_metric"] = (AnalysisContractJsonContext.Default.QueryMetricInput, AnalysisContractJsonContext.Default.QueryMetricOutput, new(QueryMetricInput.Ranges, QueryMetricInput.Lengths, QueryMetricInput.Patterns, QueryMetricInput.Choices)),
            ["ci_log"] = (AnalysisContractJsonContext.Default.CiLogInput, AnalysisContractJsonContext.Default.CiLogOutput, new(CiLogInput.Ranges, CiLogInput.Lengths, CiLogInput.Patterns, CiLogInput.Choices)),
            ["run_saved_query"] = (AnalysisContractJsonContext.Default.RunSavedQueryInput, AnalysisContractJsonContext.Default.RunSavedQueryOutput, new(RunSavedQueryInput.Ranges, RunSavedQueryInput.Lengths, RunSavedQueryInput.Patterns, RunSavedQueryInput.Choices)),
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

    private static (JsonTypeInfo Input, JsonTypeInfo Output, ContractRules Rules) AnalysisContract(string operation)
    {
        if (operation == "update_failure_status")
            return (AnalysisContractJsonContext.Default.UpdateFailureStatusInput, AnalysisContractJsonContext.Default.UpdateFailureStatusOutput, ContractRules.None);
        return s_readAnalysisContracts.TryGetValue(operation, out var contract)
            ? contract
            : throw new InvalidOperationException("Unknown analysis contract.");
    }

    private static void ValidateAnalysisInput(string operation, JsonObject input)
    {
        var contract = AnalysisContract(operation);
        CheckContract(input, contract.Input, contract.Rules);
    }

    // A request body read against its contract the way the MCP's input schema reads the same arguments, one member at a
    // time in the contract's order, so that both name the same first field: at each object a member it does not declare
    // first, in the order sent, then each declared member in the contract's order, a required one left out where it is
    // declared; at each member its closed set of forms or its type, its range and its length, then the members or items
    // it holds. An object a union takes is read as the model of its form.
    private static void CheckContract(JsonObject input, JsonTypeInfo type, ContractRules rules) => CheckObject(input, type, "", "", rules);

    private static void CheckObject(JsonObject members, JsonTypeInfo type, string field, string path, ContractRules rules)
    {
        if (type.PolymorphismOptions is { } polymorphism) CheckVariant(members, type, polymorphism, field, path, rules);
        else CheckMembers(members, type, field, path, rules, discriminator: null);
    }

    // An object's members: one it does not declare first, in the order sent, then each declared one in the contract's
    // order, a required one left out where it is declared. A form of a polymorphic union declares its discriminator first.
    private static void CheckMembers(JsonObject members, JsonTypeInfo type, string field, string path, ContractRules rules, string? discriminator)
    {
        string[] accepted = [.. discriminator is null ? [] : new[] { discriminator }, .. type.Properties.Select(static p => p.Name)];
        foreach (var (name, _) in members)
            if (!accepted.Contains(name, StringComparer.Ordinal)) throw Unknown(Join(field, name), Join(path, name), accepted);
        foreach (var property in type.Properties)
        {
            var at = Join(field, property.Name);
            if (members.TryGetPropertyValue(property.Name, out var value))
                CheckValue(value, type.Options.GetTypeInfo(property.PropertyType), at, Join(path, property.Name), rules);
            else if (property.IsRequired)
                throw new AnalysisInputException(at, "is required", Join(path, property.Name) + ".required") { Hint = $"Send {at}." };
        }
    }

    // An object a polymorphic union takes, an annotation's target or a saved query's filter, read as the form its
    // discriminator names, as the MCP's input schema resolves the union: that form's members. Without a form it names,
    // a member no form declares comes first, then the discriminator, missing or outside the forms there are.
    private static void CheckVariant(JsonObject members, JsonTypeInfo type, JsonPolymorphismOptions polymorphism, string field, string path, ContractRules rules)
    {
        var discriminator = polymorphism.TypeDiscriminatorPropertyName;
        (string Tag, JsonTypeInfo Type)[] forms = [.. polymorphism.DerivedTypes.Select(derived =>
            (Convert.ToString(derived.TypeDiscriminator, CultureInfo.InvariantCulture)!, type.Options.GetTypeInfo(derived.DerivedType)))];
        var tag = members.TryGetPropertyValue(discriminator, out var sent) && sent is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>() : null;
        if (forms.FirstOrDefault(form => form.Tag == tag) is { Type: not null } named)
        {
            CheckMembers(members, named.Type, field, path, rules, discriminator);
            return;
        }
        string[] firstForm = [discriminator, .. forms[0].Type.Properties.Select(static p => p.Name)];
        foreach (var (name, _) in members)
            if (name != discriminator && forms.All(form => form.Type.Properties.All(p => p.Name != name)))
                throw Unknown(Join(field, name), Join(path, name), firstForm);
        var at = Join(field, discriminator);
        if (sent is null && !members.ContainsKey(discriminator))
            throw new AnalysisInputException(at, "is required", Join(path, discriminator) + ".required") { Hint = $"Send {at}." };
        string[] tags = [.. forms.Select(static form => form.Tag)];
        throw new AnalysisInputException(at, $"must be one of: {string.Join(", ", tags)}", Join(path, discriminator) + ".invalid") { Allowed = tags, Hint = "Send one of the allowed values." };
    }

    private static AnalysisInputException Unknown(string at, string rule, string[] accepted) =>
        new(at, accepted.Length is 0 ? "is not accepted here" : $"is not accepted here; accepted: {string.Join(", ", accepted)}", rule + ".unknown")
        {
            Allowed = accepted.Length is 0 ? null : accepted,
            Hint = accepted.Length is 0 ? $"Drop {at}." : $"Replace {at} with one of the accepted arguments or drop it."
        };

    // One member's value: the forms its closed set takes or its type, the range, the length and the pattern the contract
    // bounds it to, then what it holds, an object's members or an array's items, whose paths write the item's index as [].
    private static void CheckValue(JsonNode? value, JsonTypeInfo type, string field, string path, ContractRules rules)
    {
        if (rules.Choices.TryGetValue(path, out var choice))
        {
            if (value is JsonObject form && choice.Models.Count > 0) { CheckForm(form, choice, field, path, rules, type.Options); return; }
            if (!Takes(choice.Values, value)) throw NotAForm(field, path, choice.Values);
        }
        else CheckType(value, type, field, path);
        if (rules.Ranges.TryGetValue(path, out var range)) CheckRange(value, field, path, range);
        if (rules.Lengths.TryGetValue(path, out var length)) CheckLength(value, field, path, length);
        if (rules.Patterns.TryGetValue(path, out var pattern)) CheckPattern(value, field, path, pattern);
        if (value is JsonObject members && type.Kind == JsonTypeInfoKind.Object && !Opaque(type))
            CheckObject(members, type, field, path, rules);
        else if (value is JsonArray items && type.Kind == JsonTypeInfoKind.Enumerable)
        {
            var element = type.Options.GetTypeInfo(type.ElementType!);
            for (var i = 0; i < items.Count; i++) CheckValue(items[i], element, $"{field}[{i}]", path + "[]", rules);
        }
    }

    private static string Join(string parent, string name) => parent.Length == 0 ? name : $"{parent}.{name}";

    // C# reads a union of models as a bare object, which CheckForm reads as its form's model.
    private static bool Opaque(JsonTypeInfo type) => (Nullable.GetUnderlyingType(type.Type) ?? type.Type) == typeof(object);

    // A saved query's or an annotation's body read against its contract as an analysis request is, then as its model.
    private static async Task<(T? Value, IResult? Error)> ReadResourceBodyAsync<T>(HttpRequest request, JsonTypeInfo<T> type,
        ContractRules rules, CancellationToken ct) where T : class
    {
        try
        {
            if (await JsonNode.ParseAsync(request.Body, cancellationToken: ct).ConfigureAwait(false) is not JsonObject body)
                return (null, ResourceHttp.InvalidBody("The request body must be a JSON object."));
            CheckContract(body, type, rules);
            return (body.Deserialize(type), null);
        }
        catch (AnalysisInputException invalid) { return (null, AnalysisError(invalid)); }
        catch (JsonException) { return (null, ResourceHttp.InvalidBody("The request body is not valid JSON.")); }
    }

    // A request whose contract takes no body, a delete, refuses a member it sends as a member no body declares.
    private static async Task<IResult?> RefuseBodyAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength is null or 0 && !request.Headers.TransferEncoding.Any(static encoding => encoding is { Length: > 0 })) return null;
        try
        {
            var body = await JsonNode.ParseAsync(request.Body, cancellationToken: ct).ConfigureAwait(false);
            if (body is JsonObject members)
                return members.FirstOrDefault() is { Key: { } name } ? AnalysisError(Unknown(name, name, [])) : null;
            return ResourceHttp.InvalidBody("This request takes no body.");
        }
        catch (JsonException) { return ResourceHttp.InvalidBody("This request takes no body."); }
    }

    // A value of the JSON type the member's C# type stands for, as the MCP's input schema names it: an object, an array,
    // a string, a number, a boolean, a time or one of an enumeration's values; null is none of them.
    private static void CheckType(JsonNode? value, JsonTypeInfo type, string field, string path)
    {
        var clr = Nullable.GetUnderlyingType(type.Type) ?? type.Type;
        if (Opaque(type)) return;
        switch (type.Kind)
        {
            case JsonTypeInfoKind.Object when value is JsonObject:
            case JsonTypeInfoKind.Enumerable when value is JsonArray:
                return;
            case JsonTypeInfoKind.None when value is JsonValue scalar:
                ReadValue(scalar, type, clr, field, path);
                return;
        }
        // An object where the contract takes another type, a scalar where it takes an object or an array, or null.
        throw new AnalysisInputException(field, $"must be {JsonType(type, clr)}", path + ".type") { Hint = $"Send {field} as {JsonTypeArticle(type, clr)}." };
    }

    // A JSON value of a member C# reads as a string, a number, a boolean, a time or an enumeration.
    private static void ReadValue(JsonValue value, JsonTypeInfo type, Type clr, string field, string path)
    {
        var kind = value.GetValueKind();
        if (clr.IsEnum)
        {
            // An enumeration travels as its names, or, without a string converter, as its numbers.
            var names = type.Options.GetTypeInfo(clr);
            string[] wire = [.. Enum.GetValuesAsUnderlyingType(clr).Cast<object>().Select(member => JsonSerializer.Serialize(Enum.ToObject(clr, member), names))];
            string[] allowed = [.. wire.Select(static text => text.Trim('"'))];
            var numeric = wire.All(static text => !text.StartsWith('"'));
            if (numeric ? kind == JsonValueKind.Number && allowed.Any(member => double.Parse(member, CultureInfo.InvariantCulture) == double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture))
                    : kind == JsonValueKind.String && allowed.Contains(value.GetValue<string>(), StringComparer.Ordinal))
                return;
            throw new AnalysisInputException(field, $"must be one of: {string.Join(", ", allowed)}", path + ".invalid") { Allowed = allowed, Hint = "Send one of the allowed values." };
        }
        if (clr == typeof(DateTimeOffset) && kind == JsonValueKind.String)
        {
            if (ContractDateTime.IsValid(value.GetValue<string>())) return;
            throw new AnalysisInputException(field, "must match format \"date-time\"", path + ".format") { Hint = "Send a time such as 2026-10-19T12:00:00Z." };
        }
        if (s_integers.TryGetValue(clr, out var range) && kind == JsonValueKind.Number)
        {
            var number = double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture);
            if (number != Math.Floor(number)) throw new AnalysisInputException(field, "must be integer", path + ".type") { Hint = $"Send {field} as an integer." };
            if (number < range.Minimum || number > range.Maximum)
                throw new AnalysisInputException(field, number > range.Maximum ? $"must be <= {range.Maximum.ToString(CultureInfo.InvariantCulture)}" : $"must be >= {range.Minimum.ToString(CultureInfo.InvariantCulture)}", path + ".out_of_range")
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
        if (!fits) throw new AnalysisInputException(field, $"must be {JsonType(type, clr)}", path + ".type") { Hint = $"Send {field} as {JsonTypeArticle(type, clr)}." };
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

    // A value of a member C# reads as a bare string or object, such as a baseline or a matcher's value, is refused when it
    // takes none of the forms the contract gives the member, with the values it accepts when they form a closed set: its
    // strings, its object forms, written {a, b}, and its JSON types, written <string>. An object is read as the model of
    // the form it is meant for, as the MCP's input schema resolves the union: the one form that declares every member it
    // sends, else the only object form there is, else the one of several such forms it satisfies.
    private static void CheckForm(JsonObject members, ContractChoice choice, string field, string path, ContractRules rules, JsonSerializerOptions options)
    {
        JsonTypeInfo[] objects = [.. choice.Models.Select(options.GetTypeInfo)];
        JsonTypeInfo[] meant = [.. objects.Where(model => members.All(member => model.Properties.Any(p => p.Name == member.Key)))];
        if (meant.Length == 0 && objects.Length == 1) meant = objects;
        if (meant.Length == 1) { CheckObject(members, meant[0], field, path, rules); return; }
        if (!meant.Any(model => Reads(members, model, field, path, rules))) throw NotAForm(field, path, choice.Values);
    }

    private static AnalysisInputException NotAForm(string field, string path, IReadOnlyList<string> forms)
    {
        string[] types = [.. forms.Where(static form => form.StartsWith('<')).Select(static form => form.Trim('<', '>'))];
        return new AnalysisInputException(field, $"must be one of: {string.Join(", ", forms.Select(static form => form.Trim('<', '>')))}", path + ".invalid")
        {
            Allowed = types.Length == 0 ? forms : null,
            Hint = types.Length == 0 ? "Send one of the allowed values." : $"Send {field} as {string.Join(" or ", types.Select(static type => (type is "integer" ? "an " : "a ") + type))}."
        };
    }

    private static bool Reads(JsonObject members, JsonTypeInfo model, string field, string path, ContractRules rules)
    {
        try { CheckObject(members, model, field, path, rules); return true; }
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

    // A number outside the range the contract bounds its member to is refused naming that range; an omitted member takes
    // its default. The engine reads the members unchecked, so this is the one place a bound applies. A nested member,
    // such as baseline.offset_minutes, belongs to an object the request may omit as a whole.
    private static void CheckRange(JsonNode? sent, string field, string path, (double? Minimum, double? Maximum, double? Default) range)
    {
        static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);
        if (sent is not JsonValue number || number.GetValueKind() != JsonValueKind.Number)
            throw new AnalysisInputException(field, "must be number", path + ".type") { Hint = $"Send {field} as a number." };
        var value = double.Parse(sent.ToJsonString(), CultureInfo.InvariantCulture);
        if (value >= (range.Minimum ?? double.NegativeInfinity) && value <= (range.Maximum ?? double.PositiveInfinity)) return;
        var bounds = (range.Minimum, range.Maximum) switch
        {
            ({ } low, { } high) => $"from {Text(low)} to {Text(high)}",
            ({ } low, null) => $"of at least {Text(low)}",
            _ => $"of at most {Text(range.Maximum!.Value)}"
        };
        throw new AnalysisInputException(field, $"Use {field} {bounds}.", path + ".out_of_range")
        {
            Hint = path.Contains('.') ? $"Correct {field}." : range.Default is { } omitted ? $"Omit {field} for {Text(omitted)}." : $"Omit {field} for the tool's default.",
            RejectedValue = J.Text(sent)
        };
    }

    // A string or an array outside the length the contract bounds its member to is refused naming that length: a string
    // counts its code points, as JSON Schema does, and an array its items. This is the one place a length or an item
    // count of a request body applies.
    private static void CheckLength(JsonNode? sent, string field, string path, (int? Minimum, int? Maximum) bounds)
    {
        static string Count(int count, string unit) => $"{count} {unit}{(count == 1 ? "" : "s")}";
        var (length, unit) = sent switch
        {
            JsonArray items => (items.Count, "item"),
            JsonValue value when value.GetValueKind() == JsonValueKind.String => (value.GetValue<string>().EnumerateRunes().Count(), "character"),
            _ => (-1, "")
        };
        if (length < 0 || length >= (bounds.Minimum ?? 0) && length <= (bounds.Maximum ?? int.MaxValue)) return;
        var range = (bounds.Minimum, bounds.Maximum) switch
        {
            ({ } low, { } high) when low == high => $"exactly {Count(high, unit)}",
            ({ } low, { } high) => $"{low} to {Count(high, unit)}",
            ({ } low, null) => $"at least {Count(low, unit)}",
            _ => $"at most {Count(bounds.Maximum!.Value, unit)}"
        };
        throw new AnalysisInputException(field, unit == "item" ? $"Use {field} with {range}." : $"Use {field} of {range}.", path + ".length") { Hint = $"Correct {field}." };
    }

    // A string that does not match the pattern the contract gives its member is refused naming the pattern, in the words of
    // the MCP's input schema; a malformed id is refused here rather than answered as an id not found.
    private static void CheckPattern(JsonNode? sent, string field, string path, string pattern)
    {
        if (sent is JsonValue value && value.GetValueKind() == JsonValueKind.String && !ContractPattern.IsMatch(pattern, value.GetValue<string>()))
            throw new AnalysisInputException(field, $"must match pattern \"{pattern}\"", path + ".pattern") { Hint = $"Correct {field}." };
    }

    private static IResult AnalysisResult(string operation, JsonObject result)
    {
        var typeInfo = AnalysisContract(operation).Output;
        var typed = JsonSerializer.Deserialize(result.ToJsonString(), typeInfo)
            ?? throw new JsonException("Analysis result is empty.");
        return Results.Json(typed, typeInfo);
    }

    private static readonly ContractRules s_failureStatusRules = new(Api.Contracts.UpdateFailureStatusRequest.Ranges, Api.Contracts.UpdateFailureStatusRequest.Lengths,
        Api.Contracts.UpdateFailureStatusRequest.Patterns, Api.Contracts.UpdateFailureStatusRequest.Choices);

    private static async Task<IResult> UpdateFailureStatusAsync(HttpContext context, [FromRoute(Name = "failure_id")] string failureId, IQylStore store, TimeProvider time, CancellationToken ct)
    {
        try
        {
            if (ResourceHttp.ReadIfMatch(context.Request, out var condition) is { } error) return error;
            if (await JsonNode.ParseAsync(context.Request.Body, cancellationToken: ct).ConfigureAwait(false) as JsonObject is not { } body) return ResourceHttp.InvalidBody("The request body must be a JSON object.");
            CheckContract(body, AnalysisContractJsonContext.Default.UpdateFailureStatusRequest, s_failureStatusRules);
            var status = J.String(body, "status");
            if (body["ignore_until"] is not null && status != "ignored") return ContractErrorResults.Validation("ignore_until", "ignore_until is valid only with status=ignored.", "ignore_until.invalid");
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
