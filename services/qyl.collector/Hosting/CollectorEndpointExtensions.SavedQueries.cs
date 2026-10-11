using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.OTel.Enums;
using Qyl.Api.Contracts.SavedQueries;
using Qyl.Collector.Storage;
using ContractMatcher = Qyl.Api.Contracts.OTel.Metrics.MetricAttributeMatcher;
using ContractMatcherOp = Qyl.Api.Contracts.OTel.Metrics.MetricMatcherOp;
using StorageMatcher = Qyl.Collector.Storage.MetricAttributeMatcher;

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
    private const int SavedQueryDefaultLookbackMinutes = 60;
    private const int SavedQueryMaxServiceNameLength = 256;
    private const int SavedQueryMaxSearchLength = 1024;
    private const int SavedQueryMaxMetricNameLength = 256;
    private const int SavedQueryMaxGroupBy = 16;
    private const int SavedQueryMaxMatchers = 32;
    private const int SavedQueryMaxMatcherKeyLength = 256;
    private const int SavedQueryMaxMatcherValueLength = 1024;

    internal static async Task<IResult> CreateSavedQueryAsync(
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
            ResourceWriteStatus.NameConflict => SavedQueryNameConflict(request.Name),
            _ => ReplaySavedQuery(httpContext, outcome.Replay!, fingerprint, idempotencyKey!)
        };
    }

    internal static async Task<IResult> ListSavedQueriesAsync(
        HttpContext httpContext,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseSavedQueries(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit,
                ContractLimits.SavedQueryMaxLimit, out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.SavedQueryMaxLimit);
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

    internal static async Task<IResult> GetSavedQueryAsync(
        HttpContext httpContext,
        [FromRoute(Name = "saved_query_id")] string savedQueryId,
        IQylStore store,
        CancellationToken ct) =>
        await store.GetSavedQueryAsync(ResolveProjectScope(httpContext), savedQueryId, ct).ConfigureAwait(false)
            is not { } row
            ? ContractErrorResults.NotFound("saved_query", savedQueryId)
            : ResourceHttp.WithEtag(httpContext, row.Etag, SavedQueryMapper.ToContract(row),
                QylSerializerContext.Default.SavedQuery);

    internal static async Task<IResult> UpdateSavedQueryAsync(
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

    internal static async Task<IResult> DeleteSavedQueryAsync(
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
    internal static async Task<IResult> RunSavedQueryAsync(
        HttpContext httpContext,
        [FromRoute(Name = "saved_query_id")] string savedQueryId,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var (request, bodyError) = await ResourceHttp.ReadOptionalJsonBodyAsync(
            httpContext.Request,
            QylSerializerContext.Default.SavedQueryRunRequest,
            static () => new SavedQueryRunRequest(),
            ct).ConfigureAwait(false);
        if (bodyError is not null)
            return bodyError;

        if (!ContractLimits.TryResolve(request!.Limit, ContractLimits.DefaultPageLimit,
                ContractLimits.SavedQueryRunMaxLimit, out var boundedLimit))
        {
            return InvalidLimit(request.Limit, ContractLimits.SavedQueryRunMaxLimit);
        }

        var projectId = ResolveProjectScope(httpContext);
        if (await store.GetSavedQueryAsync(projectId, savedQueryId, ct).ConfigureAwait(false) is not { } row)
            return ContractErrorResults.NotFound("saved_query", savedQueryId);

        var filter = SavedQueryMapper.ReadFilter(row.FilterJson);
        var lookback = TimeSpan.FromMinutes(LookbackMinutes(filter) ?? SavedQueryDefaultLookbackMinutes);
        var end = request.EndTime ?? timeProvider.GetUtcNow();
        var start = request.StartTime ?? end - lookback;
        if (end < start)
        {
            return ContractErrorResults.Validation(
                "end_time",
                "The range end must not precede its start.",
                "range.inverted",
                end.ToString("O", CultureInfo.InvariantCulture));
        }

        var startUnixNano = QylTimeConversions.ToUnixNanoUnsigned(start.ToUniversalTime());
        var endUnixNano = QylTimeConversions.ToUnixNanoUnsigned(end.ToUniversalTime());

        switch (filter)
        {
            case SavedTraceQueryFilter traces:
            {
                TracePageCursor? cursor = null;
                if (request.Cursor is { } encoded)
                {
                    if (!TryDecodeRunCursor(encoded, PageCursorKind.SavedTraceRun, savedQueryId, out var window,
                            out var fields) ||
                        !PageCursorCodec.TryParseHex(fields[0], out var activity) ||
                        !ContractIdentifiers.IsTraceId(fields[1]))
                    {
                        return InvalidCursor(encoded);
                    }

                    (startUnixNano, endUnixNano) = window;
                    cursor = new TracePageCursor(activity, fields[1]);
                }

                var page = await store.GetTracePageAsync(new TracePageQuery
                {
                    ProjectId = projectId,
                    Limit = boundedLimit,
                    Cursor = cursor,
                    StartUnixNano = startUnixNano,
                    EndUnixNano = endUnixNano,
                    ServiceName = traces.ServiceName,
                    ErrorsOnly = traces.ErrorsOnly is true
                }, ct).ConfigureAwait(false);
                var traceItems = page.Items;
                return Results.Json<SavedQueryRunResult>(new SavedTraceQueryRun
                {
                    Items = [.. ToTracePage(page, PageCursorKind.SavedTraceRun).Items.Select(SpanMapper.ToSummary)],
                    HasMore = page.HasMore,
                    NextCursor = page.HasMore && traceItems.Count > 0
                        ? PageCursorCodec.Encode(
                            PageCursorKind.SavedTraceRun,
                            savedQueryId,
                            PageCursorCodec.Hex(startUnixNano),
                            PageCursorCodec.Hex(endUnixNano),
                            PageCursorCodec.Hex(traceItems[^1].ActivityUnixNano),
                            traceItems[^1].TraceId)
                        : null
                }, QylSerializerContext.Default.SavedQueryRunResult);
            }

            case SavedLogQueryFilter logs:
            {
                LogPageCursor? cursor = null;
                if (request.Cursor is { } encoded)
                {
                    if (!TryDecodeRunCursor(encoded, PageCursorKind.SavedLogRun, savedQueryId, out var window,
                            out var fields) ||
                        !PageCursorCodec.TryParseHex(fields[0], out var time))
                    {
                        return InvalidCursor(encoded);
                    }

                    (startUnixNano, endUnixNano) = window;
                    cursor = new LogPageCursor(time, fields[1]);
                }

                var rows = await store.GetLogsAsync(
                    projectId,
                    sessionId: logs.SessionId,
                    traceId: logs.TraceId,
                    minSeverity: logs.SeverityMin is { } severity ? (int)severity : null,
                    search: logs.Query,
                    start: startUnixNano,
                    before: endUnixNano,
                    serviceName: logs.ServiceName,
                    limit: boundedLimit + 1,
                    cursor: cursor,
                    ct: ct).ConfigureAwait(false);
                var page = ToLogPage(
                    rows,
                    boundedLimit,
                    PageCursorKind.SavedLogRun,
                    savedQueryId,
                    PageCursorCodec.Hex(startUnixNano),
                    PageCursorCodec.Hex(endUnixNano));
                return Results.Json<SavedQueryRunResult>(new SavedLogQueryRun
                {
                    Items = page.Items,
                    HasMore = page.HasMore,
                    NextCursor = page.NextCursor
                }, QylSerializerContext.Default.SavedQueryRunResult);
            }

            case SavedMetricQueryFilter metrics:
            {
                if (request.Cursor is { } encoded)
                    return InvalidCursor(encoded);

                var descriptor = (await store.ListMetricsAsync(projectId, metrics.MetricName, limit: 1, ct: ct)
                        .ConfigureAwait(false))
                    .FirstOrDefault(entry => string.Equals(entry.MetricName, metrics.MetricName, StringComparison.Ordinal));
                if (descriptor is null)
                    return ContractErrorResults.NotFound("metric", metrics.MetricName);

                var aggregation = MetricMapper.ToStorageAggregation(
                    metrics.Aggregation ?? Qyl.Api.Contracts.OTel.Metrics.MetricAggregation.Avg);
                var stepMs = metrics.StepMs ?? ContractLimits.MetricDefaultStepMs;
                var series = await store.QueryMetricAsync(new MetricRangeQuery
                {
                    ProjectId = projectId,
                    MetricName = metrics.MetricName,
                    StartUnixNano = startUnixNano,
                    EndUnixNano = endUnixNano,
                    StepUnixNano = (ulong)stepMs * 1_000_000UL,
                    Aggregation = aggregation,
                    GroupBy = metrics.GroupBy ?? [],
                    Matchers = ToStorageMatchers(metrics.Matchers),
                    SeriesLimit = ContractLimits.MetricSeriesDefaultLimit + 1
                }, ct).ConfigureAwait(false);

                var truncated = series.Count > ContractLimits.MetricSeriesDefaultLimit;
                return Results.Json<SavedQueryRunResult>(new SavedMetricQueryRun
                {
                    Result = MetricMapper.ToQueryResult(
                        descriptor,
                        aggregation,
                        stepMs,
                        start,
                        end,
                        truncated ? [.. series.Take(ContractLimits.MetricSeriesDefaultLimit)] : series,
                        truncated)
                }, QylSerializerContext.Default.SavedQueryRunResult);
            }

            default:
                throw new InvalidDataException($"Saved query {savedQueryId} has an unknown filter.");
        }
    }

    private static bool TryDecodeRunCursor(
        string encoded,
        PageCursorKind kind,
        string savedQueryId,
        out (ulong Start, ulong End) window,
        out string[] keyset)
    {
        window = default;
        keyset = [];
        if (!PageCursorCodec.TryDecode(encoded, kind, 5, out var fields) ||
            !string.Equals(fields[0], savedQueryId, StringComparison.Ordinal) ||
            !PageCursorCodec.TryParseHex(fields[1], out var start) ||
            !PageCursorCodec.TryParseHex(fields[2], out var end))
        {
            return false;
        }

        window = (start, end);
        keyset = fields[3..];
        return true;
    }

    private static int? LookbackMinutes(SavedQueryFilter filter) => filter switch
    {
        SavedTraceQueryFilter traces => traces.LookbackMinutes,
        SavedLogQueryFilter logs => logs.LookbackMinutes,
        SavedMetricQueryFilter metrics => metrics.LookbackMinutes,
        _ => null
    };

    private static IReadOnlyList<StorageMatcher> ToStorageMatchers(IReadOnlyList<ContractMatcher>? matchers) =>
        matchers is null
            ? []
            : [.. matchers.Select(static matcher =>
                new StorageMatcher(matcher.Key, matcher.Value, matcher.Op is ContractMatcherOp.Prefix))];

    private static string SavedQueryLocation(string savedQueryId) =>
        "/api/v1/saved-queries/" + Uri.EscapeDataString(savedQueryId);

    private static IResult SavedQueryNameConflict(string name) =>
        ContractErrorResults.Conflict(name, "A saved query with this name already exists in the project.");

    private static IResult ReplaySavedQuery(
        HttpContext httpContext,
        IdempotencyKeyRow previous,
        string fingerprint,
        string idempotencyKey)
    {
        if (!string.Equals(previous.RequestHash, fingerprint, StringComparison.Ordinal))
            return ResourceHttp.IdempotencyConflict(idempotencyKey);

        var savedQuery = JsonSerializer.Deserialize(previous.ResponseJson, QylSerializerContext.Default.SavedQuery)
                         ?? throw new InvalidDataException("A stored idempotent response is empty.");
        return ResourceHttp.Created(httpContext, SavedQueryLocation(previous.ResourceId), savedQuery.Etag, savedQuery,
            QylSerializerContext.Default.SavedQuery);
    }

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
                        !LengthBetween(matcher.Value, 0, SavedQueryMaxMatcherValueLength) ||
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
