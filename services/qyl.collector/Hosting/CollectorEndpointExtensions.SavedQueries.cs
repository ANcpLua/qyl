using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.SavedQueries;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

// Saved queries (Contract 11.6.0): named, structured filters over traces, logs or metrics,
// unique by name within the caller's project. A saved query stores the filter, never results;
// `:run` evaluates it against current telemetry with a window relative to the run.
internal static partial class CollectorEndpointExtensions
{
    private const string SavedQueryOperation = "saved-queries";
    private const int SavedQueryMaxNameLength = 200;
    private const int SavedQueryMaxDescriptionLength = 2000;
    private const int SavedQueryMaxLookbackMinutes = 43_200;
    private const int SavedQueryMaxServiceNameLength = 256;
    private const int SavedQueryMaxSearchLength = 1024;
    private const int SavedQueryMaxMetricNameLength = 256;
    private const int SavedQueryMaxGroupBy = 16;
    private const int SavedQueryMaxMatchers = 32;
    private const int SavedQueryMaxMatcherKeyLength = 256;
    private const int SavedQueryMaxMatcherValueLength = 1024;

    private static async Task<IResult> CreateSavedQueryAsync(
        HttpContext httpContext,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (ResourceHttp.ReadIdempotencyKey(httpContext.Request, out var idempotencyKey) is { } keyError)
            return keyError;

        var (request, bodyError) = await ResourceHttp.ReadJsonBodyAsync(
            httpContext.Request, QylSerializerContext.Default.SavedQueryCreateRequest, ct).ConfigureAwait(false);
        if (bodyError is not null)
            return bodyError;

        if (ValidateSavedQueryName(request!.Name) is { } nameError)
            return nameError;
        if (ValidateSavedQueryDescription(request.Description) is { } descriptionError)
            return descriptionError;
        if (ValidateSavedQueryFilter(request.Filter) is { } filterError)
            return filterError;

        var projectId = ResolveProjectScope(httpContext);
        var fingerprint = ResourceHttp.Fingerprint(request, QylSerializerContext.Default.SavedQueryCreateRequest);
        var notBefore = ResourceHttp.IdempotencyNotBefore(timeProvider);
        var now = ResourceHttp.NowUnixNano(timeProvider);
        var row = new SavedQueryRow
        {
            ProjectId = projectId,
            SavedQueryId = ResourceHttp.NewId("sq_"),
            Name = request.Name,
            Description = request.Description,
            Signal = SavedQueryMapper.SignalName(request.Filter),
            FilterJson = SavedQueryMapper.WriteFilter(request.Filter),
            CreatedAtUnixNano = now,
            UpdatedAtUnixNano = now,
            Etag = ResourceHttp.NewEtag()
        };
        var savedQuery = SavedQueryMapper.ToContract(row);
        var idempotency = idempotencyKey is null
            ? null
            : new IdempotencyKeyRow
            {
                ProjectId = projectId,
                Operation = SavedQueryOperation,
                IdempotencyKey = idempotencyKey,
                RequestHash = fingerprint,
                ResourceId = row.SavedQueryId,
                ResponseJson = JsonSerializer.Serialize(savedQuery, QylSerializerContext.Default.SavedQuery),
                CreatedAtUnixNano = now
            };

        var outcome = await store.CreateSavedQueryAsync(row, idempotency, notBefore, ct).ConfigureAwait(false);
        return outcome.Status switch
        {
            ResourceWriteStatus.Created => ResourceHttp.Created(
                httpContext, SavedQueryLocation(row.SavedQueryId), row.Etag, savedQuery,
                QylSerializerContext.Default.SavedQuery),
            // A repeat answers the saved query its key created as it is now, with its current etag.
            ResourceWriteStatus.Replayed => ResourceHttp.Created(
                httpContext, SavedQueryLocation(outcome.Row!.SavedQueryId), outcome.Row.Etag, SavedQueryMapper.ToContract(outcome.Row),
                QylSerializerContext.Default.SavedQuery),
            ResourceWriteStatus.NameConflict => SavedQueryNameConflict(request.Name),
            _ => ResourceHttp.IdempotencyConflict(idempotencyKey!)
        };
    }

    private static async Task<IResult> ListSavedQueriesAsync(
        HttpContext httpContext,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseSavedQueries(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit,
                ContractLimits.MaxPageLimit, out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.MaxPageLimit);
        }

        string? afterName = null;
        if (query.Cursor is { } encoded)
        {
            if (!PageCursorCodec.TryDecode(encoded, PageCursorKind.SavedQueries, 1, out var fields))
                return InvalidCursor(encoded);
            afterName = fields[0];
        }

        var rows = await store.ListSavedQueriesAsync(
                ResolveProjectScope(httpContext), query.Signal, afterName, boundedLimit + 1, ct)
            .ConfigureAwait(false);
        var hasMore = rows.Count > boundedLimit;
        IReadOnlyList<SavedQueryRow> page = hasMore ? [.. rows.Take(boundedLimit)] : rows;
        return Results.Json(new CursorPageSavedQuery
        {
            Items = SavedQueryMapper.ToContracts(page),
            HasMore = hasMore,
            NextCursor = hasMore ? PageCursorCodec.Encode(PageCursorKind.SavedQueries, page[^1].Name) : null
        }, QylSerializerContext.Default.CursorPageSavedQuery);
    }

    private static async Task<IResult> GetSavedQueryAsync(
        HttpContext httpContext,
        [FromRoute(Name = "saved_query_id")] string savedQueryId,
        IQylStore store,
        CancellationToken ct) =>
        await store.GetSavedQueryAsync(ResolveProjectScope(httpContext), savedQueryId, ct).ConfigureAwait(false)
            is not { } row
            ? ContractErrorResults.NotFound("saved_query", savedQueryId)
            : ResourceHttp.WithEtag(httpContext, row.Etag, SavedQueryMapper.ToContract(row),
                QylSerializerContext.Default.SavedQuery);

    private static async Task<IResult> UpdateSavedQueryAsync(
        HttpContext httpContext,
        [FromRoute(Name = "saved_query_id")] string savedQueryId,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (ResourceHttp.ReadIfMatch(httpContext.Request, out var ifMatch) is { } preconditionError)
            return preconditionError;

        var (request, bodyError) = await ResourceHttp.ReadJsonBodyAsync(
            httpContext.Request, QylSerializerContext.Default.SavedQueryUpdateRequest, ct).ConfigureAwait(false);
        if (bodyError is not null)
            return bodyError;

        if (request!.Name is { } name && ValidateSavedQueryName(name) is { } nameError)
            return nameError;
        if (ValidateSavedQueryDescription(request.Description) is { } descriptionError)
            return descriptionError;
        if (request.Filter is { } filter && ValidateSavedQueryFilter(filter) is { } filterError)
            return filterError;

        var now = ResourceHttp.NowUnixNano(timeProvider);
        var etag = ResourceHttp.NewEtag();
        var outcome = await store.UpdateSavedQueryAsync(
            ResolveProjectScope(httpContext),
            savedQueryId,
            ifMatch,
            current => current with
            {
                // Omitted members keep their value; the contract has no way to send a null.
                Name = request.Name ?? current.Name,
                Description = request.Description ?? current.Description,
                Signal = request.Filter is { } next ? SavedQueryMapper.SignalName(next) : current.Signal,
                FilterJson = request.Filter is { } replacement
                    ? SavedQueryMapper.WriteFilter(replacement)
                    : current.FilterJson,
                UpdatedAtUnixNano = now,
                Etag = etag
            },
            ct).ConfigureAwait(false);

        return outcome.Status switch
        {
            ResourceWriteStatus.Updated => ResourceHttp.WithEtag(httpContext, outcome.Row!.Etag,
                SavedQueryMapper.ToContract(outcome.Row), QylSerializerContext.Default.SavedQuery),
            ResourceWriteStatus.PreconditionFailed => ContractErrorResults.PreconditionFailed(outcome.CurrentEtag),
            ResourceWriteStatus.NameConflict => SavedQueryNameConflict(request.Name!),
            _ => ContractErrorResults.NotFound("saved_query", savedQueryId)
        };
    }

    private static async Task<IResult> DeleteSavedQueryAsync(
        HttpContext httpContext,
        [FromRoute(Name = "saved_query_id")] string savedQueryId,
        IQylStore store,
        CancellationToken ct)
    {
        if (ResourceHttp.ReadIfMatch(httpContext.Request, out var ifMatch) is { } preconditionError)
            return preconditionError;

        var outcome = await store.DeleteSavedQueryAsync(ResolveProjectScope(httpContext), savedQueryId, ifMatch, ct)
            .ConfigureAwait(false);
        return outcome.Status switch
        {
            ResourceWriteStatus.Deleted => Results.NoContent(),
            ResourceWriteStatus.PreconditionFailed => ContractErrorResults.PreconditionFailed(outcome.CurrentEtag),
            _ => ContractErrorResults.NotFound("saved_query", savedQueryId)
        };
    }

    /// <summary>
    /// Evaluates a saved query. The window is the request's <c>start_time</c>/<c>end_time</c>
    /// where given, otherwise <c>lookback_minutes</c> (default 60) ending now. A trace or log
    /// run's <c>next_cursor</c> carries the saved query's id and the window of its first page,
    /// so every later page reads the same window and no other saved query accepts it.
    /// </summary>
    private static async Task<IResult> RunSavedQueryAsync(
        HttpContext httpContext,
        [FromRoute(Name = "saved_query_id")] string savedQueryId,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        try
        {
            if ((httpContext.Request.ContentLength is > 0
                ? await System.Text.Json.Nodes.JsonNode.ParseAsync(httpContext.Request.Body, cancellationToken: ct).ConfigureAwait(false) as System.Text.Json.Nodes.JsonObject
                : new System.Text.Json.Nodes.JsonObject()) is not { } body) return ResourceHttp.InvalidBody("Provide an object with the run options.");
            CheckRanges(body, SavedQueryRunRequest.Ranges);
            body["id"] = savedQueryId;
            var project = ResolveProjectScope(httpContext);
            if (await store.GetSavedQueryAsync(project, savedQueryId, ct).ConfigureAwait(false) is not { } saved)
                return AnalysisError(Analysis.AnalysisEngine.NoSavedQuery(savedQueryId));
            // The run reads its saved filter's signal over its own window, as the analysis route does.
            var now = timeProvider.GetUtcNow(); var handles = CreateAnalysisHandles(httpContext, project);
            var query = Analysis.AnalysisReadPlan.Resolve("run_saved_query", Analysis.AnalysisEngine.SavedQueryInput(saved, body), now, handles, project);
            var snapshot = await store.ReadAnalysisSnapshotAsync(project, query, ct).ConfigureAwait(false);
            var result = new Analysis.AnalysisEngine(snapshot, project, handles, now).Run("run_saved_query", body);
            // The analysis answer names its rows by signal and reports the run's window beside them; the HTTP route answers
            // with the run result of the contract, whose metric result states its window itself.
            var signal = Analysis.J.String(result["saved_query"]?["filter"], "signal")!;
            var run = signal == "metrics"
                ? Analysis.J.O(("signal", signal), ("result", MetricRunResult(result)))
                : Analysis.J.O(("signal", signal), ("items", result[signal]), ("has_more", result["has_more"]), ("next_cursor", result["next_cursor"]));
            var typed = JsonSerializer.Deserialize(run.ToJsonString(), QylSerializerContext.Default.SavedQueryRunResult)!;
            return Results.Json(typed, QylSerializerContext.Default.SavedQueryRunResult);
        }
        catch (Analysis.AnalysisInputException invalid) { return AnalysisError(invalid); }
        catch (JsonException) { return ResourceHttp.InvalidBody("Provide valid run options."); }
    }

    private static System.Text.Json.Nodes.JsonObject MetricRunResult(System.Text.Json.Nodes.JsonObject result)
    {
        var metric = result["metric"]!.DeepClone().AsObject();
        metric["start_time"] = result["window"]!["start_time"]!.DeepClone();
        metric["end_time"] = result["window"]!["end_time"]!.DeepClone();
        return metric;
    }

    private static int? LookbackMinutes(SavedQueryFilter filter) => filter switch
    {
        SavedTraceQueryFilter traces => traces.LookbackMinutes,
        SavedLogQueryFilter logs => logs.LookbackMinutes,
        SavedMetricQueryFilter metrics => metrics.LookbackMinutes,
        _ => null
    };

    private static string SavedQueryLocation(string savedQueryId) =>
        "/api/v1/saved-queries/" + Uri.EscapeDataString(savedQueryId);

    private static IResult SavedQueryNameConflict(string name) =>
        ContractErrorResults.Conflict(name, "A saved query with this name already exists in the project.");

    private static IResult? ValidateSavedQueryName(string name) =>
        LengthBetween(name, 1, SavedQueryMaxNameLength)
            ? null
            : ContractErrorResults.Validation("name", $"Name must be 1 to {SavedQueryMaxNameLength} characters.",
                "name.length");

    private static IResult? ValidateSavedQueryDescription(string? description) =>
        description is null || LengthBetween(description, 0, SavedQueryMaxDescriptionLength)
            ? null
            : ContractErrorResults.Validation("description",
                $"Description must be at most {SavedQueryMaxDescriptionLength} characters.", "description.length");

    private static IResult? ValidateSavedQueryFilter(SavedQueryFilter filter)
    {
        try { Analysis.AnalysisMatchers.Validate(System.Text.Json.Nodes.JsonNode.Parse(SavedQueryMapper.WriteFilter(filter))?["matchers"]); }
        catch (Analysis.AnalysisInputException invalid) { return ContractErrorResults.Validation("filter.matchers", invalid.Description, invalid.Code); }

        if (LookbackMinutes(filter) is { } lookback and (< 1 or > SavedQueryMaxLookbackMinutes))
        {
            return ContractErrorResults.Validation("filter.lookback_minutes",
                $"Look-back must be between 1 and {SavedQueryMaxLookbackMinutes} minutes.", "lookback.out_of_range",
                lookback.ToString(CultureInfo.InvariantCulture));
        }

        switch (filter)
        {
            case SavedTraceQueryFilter traces:
                return OptionalLength("filter.service_name", traces.ServiceName, SavedQueryMaxServiceNameLength);

            case SavedLogQueryFilter logs:
                if (OptionalLength("filter.service_name", logs.ServiceName, SavedQueryMaxServiceNameLength) is { } service)
                    return service;
                if (OptionalLength("filter.query", logs.Query, SavedQueryMaxSearchLength) is { } search)
                    return search;
                if (logs.SeverityMin is { } severity && !Enum.IsDefined(severity))
                    return ContractErrorResults.Validation("filter.severity_min",
                        "Severity must be an OpenTelemetry severity number from 0 to 24.", "severity.out_of_range",
                        ((int)severity).ToString(CultureInfo.InvariantCulture));
                if (logs.TraceId is { } traceId && !ContractIdentifiers.IsTraceId(traceId))
                    return ContractErrorResults.Validation("filter.trace_id", ContractIdentifiers.TraceIdMessage,
                        "trace_id.invalid", traceId);
                if (logs.SessionId is { } sessionId && !ContractIdentifiers.IsSessionId(sessionId))
                    return ContractErrorResults.Validation("filter.session_id", ContractIdentifiers.SessionIdMessage,
                        "session_id.invalid", sessionId);
                return null;

            case SavedMetricQueryFilter metrics:
                if (!LengthBetween(metrics.MetricName, 1, SavedQueryMaxMetricNameLength))
                    return ContractErrorResults.Validation("filter.metric_name",
                        $"Metric name must be 1 to {SavedQueryMaxMetricNameLength} characters.", "metric_name.length");
                if (metrics.Aggregation is { } aggregation && !Enum.IsDefined(aggregation))
                    return ContractErrorResults.Validation("filter.aggregation", "Unknown aggregation.",
                        "aggregation.invalid");
                if (metrics.StepMs is { } step and (< 1 or > ContractLimits.MetricMaxStepMs))
                    return ContractErrorResults.Validation("filter.step_ms",
                        $"Step must be between 1 and {ContractLimits.MetricMaxStepMs} milliseconds.",
                        "step.out_of_range", step.ToString(CultureInfo.InvariantCulture));
                if (metrics.GroupBy is { } groupBy &&
                    (groupBy.Count > SavedQueryMaxGroupBy || groupBy.Any(static key => string.IsNullOrEmpty(key))))
                    return ContractErrorResults.Validation("filter.group_by",
                        $"Group-by takes at most {SavedQueryMaxGroupBy} non-empty attribute keys.", "group_by.invalid");
                if (metrics.Matchers is { } matchers &&
                    (matchers.Count > SavedQueryMaxMatchers || matchers.Any(static matcher =>
                        !LengthBetween(matcher.Key, 1, SavedQueryMaxMatcherKeyLength) ||
                        !LengthBetween(Convert.ToString(matcher.Value, CultureInfo.InvariantCulture) ?? "", 0, SavedQueryMaxMatcherValueLength) ||
                        !Enum.IsDefined(matcher.Op))))
                    return ContractErrorResults.Validation("filter.matchers",
                        $"At most {SavedQueryMaxMatchers} matchers, each a key of 1 to {SavedQueryMaxMatcherKeyLength} " +
                        $"characters, an op of eq or prefix, and a value of at most {SavedQueryMaxMatcherValueLength}.",
                        "matchers.invalid");
                return null;

            default:
                return ResourceHttp.InvalidBody("filter.signal must be traces, logs or metrics.");
        }
    }

    private static IResult? OptionalLength(string field, string? value, int maximum) =>
        value is null || LengthBetween(value, 1, maximum)
            ? null
            : ContractErrorResults.Validation(field, $"Value must be 1 to {maximum} characters.", "value.length");

    // JSON Schema lengths count Unicode code points.
    private static bool LengthBetween(string value, int minimum, int maximum)
    {
        var length = value.EnumerateRunes().Count();
        return length >= minimum && length <= maximum;
    }
}
