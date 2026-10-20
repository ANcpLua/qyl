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
    private const int SavedQueryMaxMatcherValueLength = 1024;

    private static async Task<IResult> CreateSavedQueryAsync(
        HttpContext httpContext,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (ResourceHttp.ReadIdempotencyKey(httpContext.Request, out var idempotencyKey) is { } keyError)
            return keyError;

        var (request, bodyError) = await ReadResourceBodyAsync(httpContext.Request, QylSerializerContext.Default.SavedQueryCreateRequest,
            SavedQueryCreateRequest.Ranges, SavedQueryCreateRequest.Lengths, ct).ConfigureAwait(false);
        if (bodyError is not null)
            return bodyError;

        if (ValidateSavedQueryFilter(request!.Filter) is { } filterError)
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

        var (request, bodyError) = await ReadResourceBodyAsync(httpContext.Request, QylSerializerContext.Default.SavedQueryUpdateRequest,
            SavedQueryUpdateRequest.Ranges, SavedQueryUpdateRequest.Lengths, ct).ConfigureAwait(false);
        if (bodyError is not null)
            return bodyError;

        if (request!.Filter is { } filter && ValidateSavedQueryFilter(filter) is { } filterError)
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
    /// where given, otherwise the saved <c>lookback_minutes</c> (default 60) ending now; a member
    /// <c>SavedQueryRunRequest</c> does not declare is refused like an analysis argument. A trace or
    /// log run's <c>next_cursor</c> carries the saved query's id and the window of its first page,
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
            CheckContract(body, QylSerializerContext.Default.SavedQueryRunRequest, SavedQueryRunRequest.Ranges, SavedQueryRunRequest.Lengths, SavedQueryRunRequest.Choices);
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

    private static string SavedQueryLocation(string savedQueryId) =>
        "/api/v1/saved-queries/" + Uri.EscapeDataString(savedQueryId);

    private static IResult SavedQueryNameConflict(string name) =>
        ContractErrorResults.Conflict(name, "A saved query with this name already exists in the project.");

    // The filter's lengths and ranges were checked with its body; what the contract states beside them is checked here.
    private static IResult? ValidateSavedQueryFilter(SavedQueryFilter filter)
    {
        try { Analysis.AnalysisMatchers.Validate(System.Text.Json.Nodes.JsonNode.Parse(SavedQueryMapper.WriteFilter(filter))?["matchers"]); }
        catch (Analysis.AnalysisInputException invalid) { return ContractErrorResults.Validation("filter.matchers", invalid.Description, invalid.Code); }

        switch (filter)
        {
            case SavedTraceQueryFilter:
                return null;

            case SavedLogQueryFilter logs:
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
                if (metrics.Aggregation is { } aggregation && !Enum.IsDefined(aggregation))
                    return ContractErrorResults.Validation("filter.aggregation", "Unknown aggregation.",
                        "aggregation.invalid");
                if (metrics.GroupBy is { } groupBy && groupBy.Any(static key => string.IsNullOrEmpty(key)))
                    return ContractErrorResults.Validation("filter.group_by", "Group by non-empty attribute keys.", "group_by.invalid");
                if (metrics.Matchers is { } matchers && matchers.Any(static matcher =>
                        (Convert.ToString(matcher.Value, CultureInfo.InvariantCulture) ?? "").EnumerateRunes().Count() > SavedQueryMaxMatcherValueLength ||
                        !Enum.IsDefined(matcher.Op)))
                    return ContractErrorResults.Validation("filter.matchers",
                        $"Each matcher takes one of the matcher ops and a value of at most {SavedQueryMaxMatcherValueLength} characters.",
                        "matchers.invalid");
                return null;

            default:
                return ResourceHttp.InvalidBody("filter.signal must be traces, logs or metrics.");
        }
    }
}
