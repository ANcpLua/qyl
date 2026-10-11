using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Annotations;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

// Annotations (Contract 11.6.0): user notes on a trace, span or session inside the caller's
// project. A note never changes the telemetry it points at.
internal static partial class CollectorEndpointExtensions
{
    internal const int AnnotationMaxTextLength = 2000;
    private const string AnnotationOperation = "annotations";

    internal static async Task<IResult> CreateAnnotationAsync(
        HttpContext httpContext,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (ResourceHttp.ReadIdempotencyKey(httpContext.Request, out var idempotencyKey) is { } keyError)
            return keyError;

        var (request, bodyError) = await ResourceHttp.ReadJsonBodyAsync(
            httpContext.Request, QylSerializerContext.Default.AnnotationCreateRequest, ct).ConfigureAwait(false);
        if (bodyError is not null)
            return bodyError;

        if (ValidateAnnotationText(request!.Text) is { } textError)
            return textError;
        if (ValidateAnnotationTarget(request.Target) is { } targetError)
            return targetError;

        var projectId = ResolveProjectScope(httpContext);
        var fingerprint = ResourceHttp.Fingerprint(request, QylSerializerContext.Default.AnnotationCreateRequest);
        var notBefore = ResourceHttp.IdempotencyNotBefore(timeProvider);

        // A retry replays its first result even if the target has since expired, so the key is
        // consulted before the target is.
        if (idempotencyKey is not null &&
            await store.GetIdempotencyKeyAsync(projectId, AnnotationOperation, idempotencyKey, notBefore, ct)
                .ConfigureAwait(false) is { } previous)
        {
            return ReplayAnnotation(httpContext, previous, fingerprint, idempotencyKey);
        }

        if (await FindMissingTargetAsync(store, projectId, request.Target, ct).ConfigureAwait(false) is { } missing)
            return missing;

        var now = ResourceHttp.NowUnixNano(timeProvider);
        var row = new AnnotationRow
        {
            ProjectId = projectId,
            AnnotationId = ResourceHttp.NewId("ann_"),
            TargetKind = request.Target switch
            {
                TraceAnnotationTarget => AnnotationMapper.TraceKind,
                SpanAnnotationTarget => AnnotationMapper.SpanKind,
                _ => AnnotationMapper.SessionKind
            },
            TraceId = request.Target switch
            {
                TraceAnnotationTarget trace => trace.TraceId,
                SpanAnnotationTarget span => span.TraceId,
                _ => null
            },
            SpanId = (request.Target as SpanAnnotationTarget)?.SpanId,
            SessionId = (request.Target as SessionAnnotationTarget)?.SessionId,
            Text = request.Text,
            CreatedAtUnixNano = now,
            UpdatedAtUnixNano = now,
            Etag = ResourceHttp.NewEtag()
        };
        var annotation = AnnotationMapper.ToContract(row);
        var idempotency = idempotencyKey is null
            ? null
            : new IdempotencyKeyRow
            {
                ProjectId = projectId,
                Operation = AnnotationOperation,
                IdempotencyKey = idempotencyKey,
                RequestHash = fingerprint,
                ResourceId = row.AnnotationId,
                ResponseJson = JsonSerializer.Serialize(annotation, QylSerializerContext.Default.Annotation),
                CreatedAtUnixNano = now
            };

        var outcome = await store.CreateAnnotationAsync(row, idempotency, notBefore, ct).ConfigureAwait(false);
        return outcome.Status switch
        {
            ResourceWriteStatus.Created => ResourceHttp.Created(
                httpContext, AnnotationLocation(row.AnnotationId), row.Etag, annotation,
                QylSerializerContext.Default.Annotation),
            _ => ReplayAnnotation(httpContext, outcome.Replay!, fingerprint, idempotencyKey!)
        };
    }

    internal static async Task<IResult> ListAnnotationsAsync(
        HttpContext httpContext,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseAnnotations(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit,
                ContractLimits.MaxPageLimit, out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.MaxPageLimit);
        }

        AnnotationPageCursor? cursor = null;
        if (query.Cursor is { } encoded)
        {
            if (!PageCursorCodec.TryDecode(encoded, PageCursorKind.Annotations, 2, out var fields) ||
                !PageCursorCodec.TryParseHex(fields[0], out var createdAt))
            {
                return InvalidCursor(encoded);
            }

            cursor = new AnnotationPageCursor(createdAt, fields[1]);
        }

        var rows = await store.ListAnnotationsAsync(new AnnotationListQuery
        {
            ProjectId = ResolveProjectScope(httpContext),
            Limit = boundedLimit + 1,
            TraceId = query.TraceId,
            SpanId = query.SpanId,
            SessionId = query.SessionId,
            Cursor = cursor
        }, ct).ConfigureAwait(false);

        var hasMore = rows.Count > boundedLimit;
        IReadOnlyList<AnnotationRow> page = hasMore ? [.. rows.Take(boundedLimit)] : rows;
        return Results.Json(new CursorPageAnnotation
        {
            Items = AnnotationMapper.ToContracts(page),
            HasMore = hasMore,
            NextCursor = hasMore
                ? PageCursorCodec.Encode(
                    PageCursorKind.Annotations,
                    PageCursorCodec.Hex(page[^1].CreatedAtUnixNano),
                    page[^1].AnnotationId)
                : null
        }, QylSerializerContext.Default.CursorPageAnnotation);
    }

    internal static async Task<IResult> GetAnnotationAsync(
        HttpContext httpContext,
        [FromRoute(Name = "annotation_id")] string annotationId,
        IQylStore store,
        CancellationToken ct) =>
        await store.GetAnnotationAsync(ResolveProjectScope(httpContext), annotationId, ct).ConfigureAwait(false)
            is not { } row
            ? ContractErrorResults.NotFound("annotation", annotationId)
            : ResourceHttp.WithEtag(httpContext, row.Etag, AnnotationMapper.ToContract(row),
                QylSerializerContext.Default.Annotation);

    internal static async Task<IResult> UpdateAnnotationAsync(
        HttpContext httpContext,
        [FromRoute(Name = "annotation_id")] string annotationId,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (ResourceHttp.ReadIfMatch(httpContext.Request, out var ifMatch) is { } preconditionError)
            return preconditionError;

        var (request, bodyError) = await ResourceHttp.ReadJsonBodyAsync(
            httpContext.Request, QylSerializerContext.Default.AnnotationUpdateRequest, ct).ConfigureAwait(false);
        if (bodyError is not null)
            return bodyError;
        if (ValidateAnnotationText(request!.Text) is { } textError)
            return textError;

        var now = ResourceHttp.NowUnixNano(timeProvider);
        var etag = ResourceHttp.NewEtag();
        var outcome = await store.UpdateAnnotationAsync(
            ResolveProjectScope(httpContext),
            annotationId,
            ifMatch,
            current => current with { Text = request.Text, UpdatedAtUnixNano = now, Etag = etag },
            ct).ConfigureAwait(false);

        return outcome.Status switch
        {
            ResourceWriteStatus.Updated => ResourceHttp.WithEtag(httpContext, outcome.Row!.Etag,
                AnnotationMapper.ToContract(outcome.Row), QylSerializerContext.Default.Annotation),
            ResourceWriteStatus.PreconditionFailed => ContractErrorResults.PreconditionFailed(outcome.CurrentEtag),
            _ => ContractErrorResults.NotFound("annotation", annotationId)
        };
    }

    internal static async Task<IResult> DeleteAnnotationAsync(
        HttpContext httpContext,
        [FromRoute(Name = "annotation_id")] string annotationId,
        IQylStore store,
        CancellationToken ct)
    {
        if (ResourceHttp.ReadIfMatch(httpContext.Request, out var ifMatch) is { } preconditionError)
            return preconditionError;

        var outcome = await store.DeleteAnnotationAsync(ResolveProjectScope(httpContext), annotationId, ifMatch, ct)
            .ConfigureAwait(false);
        return outcome.Status switch
        {
            ResourceWriteStatus.Deleted => Results.NoContent(),
            ResourceWriteStatus.PreconditionFailed => ContractErrorResults.PreconditionFailed(outcome.CurrentEtag),
            _ => ContractErrorResults.NotFound("annotation", annotationId)
        };
    }

    private static string AnnotationLocation(string annotationId) =>
        "/api/v1/annotations/" + Uri.EscapeDataString(annotationId);

    private static IResult ReplayAnnotation(
        HttpContext httpContext,
        IdempotencyKeyRow previous,
        string fingerprint,
        string idempotencyKey)
    {
        if (!string.Equals(previous.RequestHash, fingerprint, StringComparison.Ordinal))
            return ResourceHttp.IdempotencyConflict(idempotencyKey);

        var annotation = JsonSerializer.Deserialize(previous.ResponseJson, QylSerializerContext.Default.Annotation)
                         ?? throw new InvalidDataException("A stored idempotent response is empty.");
        return ResourceHttp.Created(httpContext, AnnotationLocation(previous.ResourceId), annotation.Etag, annotation,
            QylSerializerContext.Default.Annotation);
    }

    // The contract counts characters as JSON Schema does: Unicode code points, not UTF-16 units.
    private static IResult? ValidateAnnotationText(string text)
    {
        var length = text.EnumerateRunes().Count();
        return length is >= 1 and <= AnnotationMaxTextLength
            ? null
            : ContractErrorResults.Validation(
                "text",
                $"Text must be 1 to {AnnotationMaxTextLength} characters.",
                "text.length",
                length.ToString(CultureInfo.InvariantCulture));
    }

    private static IResult? ValidateAnnotationTarget(AnnotationTarget target) => target switch
    {
        TraceAnnotationTarget trace when !ContractIdentifiers.IsTraceId(trace.TraceId) =>
            ContractErrorResults.Validation("target.trace_id", ContractIdentifiers.TraceIdMessage, "trace_id.invalid",
                trace.TraceId),
        SpanAnnotationTarget span when !ContractIdentifiers.IsTraceId(span.TraceId) =>
            ContractErrorResults.Validation("target.trace_id", ContractIdentifiers.TraceIdMessage, "trace_id.invalid",
                span.TraceId),
        SpanAnnotationTarget span when !ContractIdentifiers.IsSpanId(span.SpanId) =>
            ContractErrorResults.Validation("target.span_id", ContractIdentifiers.SpanIdMessage, "span_id.invalid",
                span.SpanId),
        SessionAnnotationTarget session when !ContractIdentifiers.IsSessionId(session.SessionId) =>
            ContractErrorResults.Validation("target.session_id", ContractIdentifiers.SessionIdMessage,
                "session_id.invalid", session.SessionId),
        TraceAnnotationTarget or SpanAnnotationTarget or SessionAnnotationTarget => null,
        _ => ResourceHttp.InvalidBody("target.kind must be trace, span or session.")
    };

    private static async Task<IResult?> FindMissingTargetAsync(
        IQylStore store,
        string projectId,
        AnnotationTarget target,
        CancellationToken ct) => target switch
    {
        TraceAnnotationTarget trace =>
            await store.TraceExistsAsync(projectId, trace.TraceId, ct).ConfigureAwait(false)
                ? null
                : ContractErrorResults.NotFound("trace", trace.TraceId),
        SpanAnnotationTarget span =>
            await store.SpanExistsAsync(projectId, span.TraceId, span.SpanId, ct).ConfigureAwait(false)
                ? null
                : ContractErrorResults.NotFound("span", span.SpanId),
        SessionAnnotationTarget session =>
            await store.GetSessionAsync(session.SessionId, projectId, ct).ConfigureAwait(false) is not null
                ? null
                : ContractErrorResults.NotFound("session", session.SessionId),
        _ => ResourceHttp.InvalidBody("target.kind must be trace, span or session.")
    };
}
