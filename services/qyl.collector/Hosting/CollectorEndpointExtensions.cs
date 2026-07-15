using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qyl.Collector;
using Qyl.Collector.Cost;
using Qyl.Collector.Dashboard;
using Qyl.Collector.Grpc;
using Qyl.Api.Contracts;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.OTel.Metrics;
using Qyl.Api.Contracts.Streaming;
using Qyl.Api.Contracts.Cost;
using ExportLogsServiceRequest = global::OpenTelemetry.Proto.Collector.Logs.V1.ExportLogsServiceRequest;
using ExportLogsServiceResponse = global::OpenTelemetry.Proto.Collector.Logs.V1.ExportLogsServiceResponse;
using ExportMetricsServiceRequest = global::OpenTelemetry.Proto.Collector.Metrics.V1.ExportMetricsServiceRequest;
using ExportMetricsServiceResponse = global::OpenTelemetry.Proto.Collector.Metrics.V1.ExportMetricsServiceResponse;
using ExportProfilesServiceRequest = global::OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest;
using ExportProfilesServiceResponse = global::OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceResponse;
using ExportTraceServiceRequest = global::OpenTelemetry.Proto.Collector.Trace.V1.ExportTraceServiceRequest;
using ExportTraceServiceResponse = global::OpenTelemetry.Proto.Collector.Trace.V1.ExportTraceServiceResponse;

namespace Qyl.Collector.Hosting;

internal static class CollectorEndpointExtensions
{
    public static WebApplication MapQylCollectorEndpoints(this WebApplication app)
    {
        app.MapGrpcService<TraceServiceImpl>();
        app.MapGrpcService<LogsServiceImpl>();
        app.MapGrpcService<MetricsServiceImpl>();
        app.MapGrpcService<ProfilesServiceImpl>();

        var otlp = app.MapGroup("/v1");
        otlp.MapPost("/traces", IngestOtlpTracesAsync);
        otlp.MapPost("/logs", IngestOtlpLogsAsync);
        otlp.MapPost("/metrics", IngestOtlpMetricsAsync);
        app.MapPost("/v1development/profiles", IngestOtlpProfilesAsync);

        var api = app.MapGroup("/api/v1");

        api.MapGet("/sessions", GetSessionsAsync);
        api.MapGet("/sessions/stats", GetSessionStatsAsync);
        api.MapGet("/sessions/{sessionId}", GetSessionByIdAsync);
        api.MapGet("/sessions/{sessionId}/traces", GetSessionTracesAsync);

        api.MapGet("/traces", GetTracesAsync);
        api.MapGet("/traces/{traceId}", GetTraceAsync);
        api.MapGet("/traces/{traceId}/spans", GetTraceSpansAsync);

        api.MapGet("/logs", GetLogsAsync);
        api.MapGet("/stream/logs", StreamLogsAsync);

        api.MapGet("/metrics", GetMetricsAsync);

        api.MapGet("/cost/etl-audit", GetGenAiEtlAuditAsync);
        api.MapPost("/cost/etl-audit/evaluate", EvaluateGenAiEtlAuditAsync);

        api.MapGet("/profiles", GetProfilesAsync);
        api.MapGet("/profiles/{profileId}", GetProfileByIdAsync);
        api.MapGet("/profiles/by-trace/{traceId}", GetTraceProfilesAsync);
        api.MapGet("/profiles/by-span/{spanId}", GetSpanProfilesAsync);

        app.MapFallback(FallbackHandler);

        return app;
    }


    internal static async Task<IResult> IngestOtlpTracesAsync(
        HttpContext context,
        IQylStore store,
        CancellationToken ct)
    {
        var encoding = OtlpPayloadEncoding.Json;
        ExportTraceServiceRequest otlpData;
        try
        {
            encoding = OtlpPayloadParser.GetEncoding(context.Request.ContentType);
            otlpData = await OtlpPayloadParser.ParseTraceRequestAsync(context.Request, encoding, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is OtlpUnsupportedMediaTypeException or OtlpUnsupportedContentEncodingException)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpTracesEndpoint");
            OtlpTracesLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status415UnsupportedMediaType,
                encoding,
                "Content-Type must be application/x-protobuf or application/json; Content-Encoding must be gzip, identity, or absent.");
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpTracesEndpoint");
            OtlpTracesLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                encoding,
                "The OTLP traces payload could not be decoded.");
        }

        try
        {
            if (otlpData.ResourceSpans.Count is 0)
                return OtlpHttpResult.Success(
                    encoding,
                    new ExportTraceServiceResponse());

            var traceBatch = OtlpConverter.ConvertTraceRequest(otlpData);
            var spans = IngestionStorageMapper.ToSpanStorageRows(traceBatch);

            if (spans.Count is 0)
                return OtlpHttpResult.Success(
                    encoding,
                    new ExportTraceServiceResponse());

            await store.EnqueueAsync(new SpanBatch(spans), ct);

            return OtlpHttpResult.Success(
                encoding,
                new ExportTraceServiceResponse());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpTracesEndpoint");
            OtlpTracesLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                encoding,
                "The OTLP traces payload is invalid.");
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpTracesEndpoint");
            OtlpTracesLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                encoding,
                "The collector could not persist the OTLP traces payload.");
        }
    }

    internal static async Task<IResult> IngestOtlpLogsAsync(
        HttpContext context,
        IQylStore store,
        CancellationToken ct)
    {
        var encoding = OtlpPayloadEncoding.Json;
        ExportLogsServiceRequest otlpData;
        try
        {
            encoding = OtlpPayloadParser.GetEncoding(context.Request.ContentType);
            otlpData = await OtlpPayloadParser.ParseLogsRequestAsync(context.Request, encoding, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is OtlpUnsupportedMediaTypeException or OtlpUnsupportedContentEncodingException)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpLogsEndpoint");
            OtlpLogsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status415UnsupportedMediaType,
                encoding,
                "Content-Type must be application/x-protobuf or application/json; Content-Encoding must be gzip, identity, or absent.");
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpLogsEndpoint");
            OtlpLogsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                encoding,
                "The OTLP logs payload could not be decoded.");
        }

        try
        {
            if (otlpData.ResourceLogs.Count is 0)
                return OtlpHttpResult.Success(
                    encoding,
                    new ExportLogsServiceResponse());

            var logBatch = OtlpConverter.ConvertLogs(otlpData);
            var logs = IngestionStorageMapper.ToLogStorageRows(logBatch);

            if (logs.Count is 0)
                return OtlpHttpResult.Success(
                    encoding,
                    new ExportLogsServiceResponse());

            await store.InsertLogsAsync(logs, ct);
            return OtlpHttpResult.Success(
                encoding,
                new ExportLogsServiceResponse());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpLogsEndpoint");
            OtlpLogsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                encoding,
                "The OTLP logs payload is invalid.");
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpLogsEndpoint");
            OtlpLogsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                encoding,
                "The collector could not persist the OTLP logs payload.");
        }
    }


    internal static async Task<IResult> IngestOtlpMetricsAsync(
        HttpContext context,
        IQylStore store,
        CancellationToken ct)
    {
        var encoding = OtlpPayloadEncoding.Json;
        ExportMetricsServiceRequest otlpData;
        try
        {
            encoding = OtlpPayloadParser.GetEncoding(context.Request.ContentType);
            otlpData = await OtlpPayloadParser.ParseMetricsRequestAsync(context.Request, encoding, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is OtlpUnsupportedMediaTypeException or OtlpUnsupportedContentEncodingException)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpMetricsEndpoint");
            OtlpMetricsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status415UnsupportedMediaType,
                encoding,
                "Content-Type must be application/x-protobuf or application/json; Content-Encoding must be gzip, identity, or absent.");
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpMetricsEndpoint");
            OtlpMetricsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                encoding,
                "The OTLP metrics payload could not be decoded.");
        }

        try
        {
            if (otlpData.ResourceMetrics.Count is 0)
                return OtlpHttpResult.Success(
                    encoding,
                    new ExportMetricsServiceResponse());

            var metricBatch = OtlpConverter.ConvertMetrics(otlpData);
            var metrics = IngestionStorageMapper.ToMetricStorageRows(metricBatch);

            if (metrics.Count is 0)
                return OtlpHttpResult.Success(
                    encoding,
                    new ExportMetricsServiceResponse());

            await store.InsertMetricsAsync(metrics, ct);
            return OtlpHttpResult.Success(
                encoding,
                new ExportMetricsServiceResponse());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpMetricsEndpoint");
            OtlpMetricsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                encoding,
                "The OTLP metrics payload is invalid.");
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpMetricsEndpoint");
            OtlpMetricsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                encoding,
                "The collector could not persist the OTLP metrics payload.");
        }
    }

    internal static async Task<IResult> GetSessionsAsync(
        HttpContext httpContext,
        IQylStore store,
        string? cursor,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseSessions(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!TryReadOffsetCursor(cursor, out var offset))
        {
            return ContractErrorResults.Validation(
                "cursor",
                "Cursor must be a non-negative integer offset.",
                "cursor.invalid",
                cursor);
        }

        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit, ContractLimits.SessionMaxLimit,
                out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.SessionMaxLimit);
        }
        var sessions = await store.GetSessionsAsync(
            ResolveProjectScope(httpContext),
            boundedLimit + 1,
            offset,
            query.IsActive,
            query.StartTime,
            query.EndTime,
            ct: ct).ConfigureAwait(false);

        var hasMore = sessions.Count > boundedLimit;
        var pageItems = hasMore ? sessions.Take(boundedLimit).ToArray() : sessions;
        var response = SessionMapper.ToPage(
            pageItems,
            hasMore,
            offset > 0 ? Math.Max(0, offset - boundedLimit).ToString(CultureInfo.InvariantCulture) : null,
            hasMore ? (offset + boundedLimit).ToString(CultureInfo.InvariantCulture) : null);
        return Results.Ok(response);
    }

    private static bool TryReadOffsetCursor(string? cursor, out int offset)
    {
        offset = 0;
        return string.IsNullOrWhiteSpace(cursor) ||
               int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out offset) && offset >= 0;
    }

    private static async Task<IResult> GetSessionByIdAsync(
        HttpContext httpContext,
        string sessionId,
        IQylStore store,
        CancellationToken ct) =>
        await store.GetSessionAsync(sessionId, ResolveProjectScope(httpContext), ct: ct).ConfigureAwait(false) is not { } session
            ? ContractErrorResults.NotFound("session", sessionId)
            : Results.Ok(SessionMapper.ToContract(session));

    internal static async Task<IResult> GetSessionStatsAsync(
        HttpContext httpContext,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseSessionStats(httpContext.Request, out var query) is { } queryError)
            return queryError;

        var stats = await store.GetSessionStatsAsync(
                ResolveProjectScope(httpContext), query.StartTime, query.EndTime, ct: ct)
            .ConfigureAwait(false);
        return Results.Ok(new SessionStats
        {
            ActiveSessions = stats.ActiveSessions,
            TotalSessions = stats.TotalSessions,
            AvgDurationMs = stats.AvgDurationMs,
            SessionsWithErrors = stats.SessionsWithErrors,
            SessionsWithGenAi = stats.SessionsWithGenAi,
            BounceRate = stats.BounceRate
        });
    }

    private static async Task<IResult> GetSessionTracesAsync(
        HttpContext httpContext,
        string sessionId,
        IQylStore store,
        CancellationToken ct)
    {
        if (await store.GetSessionAsync(sessionId, ResolveProjectScope(httpContext), ct: ct).ConfigureAwait(false) is null)
            return ContractErrorResults.NotFound("session", sessionId);

        var spans = await store.GetSpansBySessionAsync(sessionId, ResolveProjectScope(httpContext), ct: ct)
            .ConfigureAwait(false);
        var traces = spans
            .GroupBy(static span => span.TraceId, StringComparer.Ordinal)
            .Select(static group =>
            {
                var spanContracts = SpanMapper.ToContracts(group);
                return SpanMapper.ToTrace(group.Key, spanContracts);
            })
            .ToList();

        return Results.Ok(new CursorPageTrace { Items = traces, HasMore = false });
    }

    internal static async Task<IResult> GetTracesAsync(
        HttpContext httpContext,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseTraces(httpContext.Request, out var parameters) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(parameters.Limit, ContractLimits.DefaultPageLimit, ContractLimits.TraceMaxLimit,
                out var boundedLimit))
        {
            return InvalidLimit(parameters.Limit, ContractLimits.TraceMaxLimit);
        }

        TracePageCursor? cursor = null;
        if (parameters.Cursor is { } encodedCursor)
        {
            if (!TracePageCursorCodec.TryDecode(encodedCursor, out var decodedCursor))
            {
                return ContractErrorResults.Validation(
                    "cursor",
                    "Cursor is not a valid qyl trace-page cursor.",
                    "cursor.invalid",
                    encodedCursor);
            }

            cursor = decodedCursor;
        }

        var page = await store.GetTracePageAsync(
                ResolveProjectScope(httpContext),
                cursor,
                boundedLimit,
                ct)
            .ConfigureAwait(false);
        var traces = page.Items.Select(static item =>
        {
            var spanContracts = SpanMapper.ToContracts(item.Spans);
            return SpanMapper.ToTrace(item.TraceId, spanContracts);
        }).ToList();
        var nextCursor = page.HasMore && page.Items.Count > 0
            ? TracePageCursorCodec.Encode(new TracePageCursor(
                page.Items[^1].ActivityUnixNano,
                page.Items[^1].TraceId))
            : null;

        return Results.Ok(new CursorPageTrace
        {
            Items = traces,
            HasMore = page.HasMore,
            NextCursor = nextCursor
        });
    }

    private static async Task<IResult> GetTraceSpansAsync(
        HttpContext httpContext,
        string traceId,
        IQylStore store,
        CancellationToken ct)
    {
        var spans = await store.GetTraceAsync(traceId, ResolveProjectScope(httpContext), ct: ct).ConfigureAwait(false);
        if (spans.Count is 0) return ContractErrorResults.NotFound("trace", traceId);

        var spanContracts = SpanMapper.ToContracts(spans);
        return Results.Ok(new CursorPageSpan { Items = spanContracts, HasMore = false });
    }

    private static async Task<IResult> GetTraceAsync(
        HttpContext httpContext,
        string traceId,
        IQylStore store,
        CancellationToken ct)
    {
        var spans = await store.GetTraceAsync(traceId, ResolveProjectScope(httpContext), ct: ct).ConfigureAwait(false);
        if (spans.Count is 0) return ContractErrorResults.NotFound("trace", traceId);

        var spanContracts = SpanMapper.ToContracts(spans);

        return Results.Ok(SpanMapper.ToTrace(traceId, spanContracts));
    }

    internal static async Task<IResult> GetLogsAsync(
        HttpContext httpContext,
        IQylStore store,
        string? sessionId,
        string? traceId,
        string? serviceName,
        string? level,
        string? query,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseLogs(httpContext.Request, out var typedQuery) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(typedQuery.Limit, ContractLimits.DefaultPageLimit, ContractLimits.LogMaxLimit,
                out var boundedLimit))
        {
            return InvalidLimit(typedQuery.Limit, ContractLimits.LogMaxLimit);
        }

        if (typedQuery.SeverityMin is < ContractLimits.MinimumLogSeverity or > ContractLimits.MaximumLogSeverity)
        {
            return ContractErrorResults.Validation(
                "severityMin",
                $"Severity must be between {ContractLimits.MinimumLogSeverity} and {ContractLimits.MaximumLogSeverity}.",
                "severity.out_of_range",
                typedQuery.SeverityMin.Value.ToString(CultureInfo.InvariantCulture));
        }
        var logs = await store.GetLogsAsync(
            ResolveProjectScope(httpContext),
            sessionId: sessionId,
            traceId: traceId,
            severityText: level,
            minSeverity: typedQuery.SeverityMin,
            search: query,
            start: typedQuery.StartTime.HasValue
                ? QylTimeConversions.ToUnixNanoUnsigned(typedQuery.StartTime.Value.ToUniversalTime())
                : null,
            before: typedQuery.EndTime.HasValue
                ? QylTimeConversions.ToUnixNanoUnsigned(typedQuery.EndTime.Value.ToUniversalTime())
                : null,
            serviceName: serviceName,
            limit: boundedLimit,
            ct: ct);

        return Results.Ok(new CursorPageLogRecord { Items = LogMapper.ToContracts(logs), HasMore = logs.Count >= boundedLimit });
    }

    internal static async Task<IResult> GetMetricsAsync(
        HttpContext httpContext,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseMetrics(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(
                query.Limit,
                ContractLimits.DefaultPageLimit,
                ContractLimits.MetricMaxLimit,
                out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.MetricMaxLimit);
        }

        if (query.StartTime.HasValue && query.EndTime.HasValue && query.StartTime > query.EndTime)
        {
            return ContractErrorResults.Validation(
                "endTime",
                "End time must be greater than or equal to start time.",
                "range.invalid",
                query.EndTime.Value.ToString("O", CultureInfo.InvariantCulture));
        }

        MetricPageCursor? cursor = null;
        if (query.Cursor is { } encodedCursor)
        {
            if (!MetricPageCursorCodec.TryDecode(encodedCursor, out var decodedCursor))
            {
                return ContractErrorResults.Validation(
                    "cursor",
                    "Cursor is not a valid qyl metric-page cursor.",
                    "cursor.invalid",
                    encodedCursor);
            }

            cursor = decodedCursor;
        }

        var page = await store.GetMetricPageAsync(
                ResolveProjectScope(httpContext),
                cursor,
                query.MetricType,
                query.Name,
                query.ServiceName,
                query.StartTime.HasValue
                    ? QylTimeConversions.ToUnixNanoUnsigned(query.StartTime.Value.ToUniversalTime())
                    : null,
                query.EndTime.HasValue
                    ? QylTimeConversions.ToUnixNanoUnsigned(query.EndTime.Value.ToUniversalTime())
                    : null,
                boundedLimit,
                ct)
            .ConfigureAwait(false);
        var items = page.Items.Select(MetricMapper.ToContract).ToArray();
        var nextCursor = page.HasMore && page.Items.Count > 0
            ? MetricPageCursorCodec.Encode(new MetricPageCursor(
                page.Items[^1].TimeUnixNano,
                page.Items[^1].MetricId))
            : null;

        return Results.Ok(new CursorPageMetricPoint
        {
            Items = items,
            HasMore = page.HasMore,
            NextCursor = nextCursor
        });
    }

    internal static async Task StreamLogsAsync(
        HttpContext context,
        IQylStore store,
        CollectorStreamCapacity streamCapacity,
        string? serviceName,
        string? query,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseLogStream(context.Request, out var minSeverity) is { } queryError)
        {
            await queryError.ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        if (minSeverity is < ContractLimits.MinimumStreamSeverity or > ContractLimits.MaximumLogSeverity)
        {
            await ContractErrorResults.WriteValidationAsync(
                context.Response,
                "minSeverity",
                $"Severity must be between {ContractLimits.MinimumStreamSeverity} and {ContractLimits.MaximumLogSeverity}.",
                "severity.out_of_range",
                minSeverity.Value.ToString(CultureInfo.InvariantCulture),
                ct).ConfigureAwait(false);
            return;
        }

        using var streamLease = streamCapacity.TryAcquire();
        if (streamLease is null)
        {
            await ContractErrorResults.WriteServiceUnavailableAsync(
                context.Response,
                "collector.log_stream_capacity",
                ct).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers.Pragma = "no-cache";

        var afterIngestSequence = ParseLastEventId(context.Request.Headers["Last-Event-ID"].ToString());
        await foreach (var streamEvent in StreamLogEventsAsync(
                           store,
                           ResolveProjectScope(context),
                           serviceName,
                           minSeverity,
                           query,
                           afterIngestSequence,
                           ct)
                           .ConfigureAwait(false))
        {
            if (streamEvent.Log is { } log)
            {
                await WriteSseEventAsync(
                    context.Response,
                    streamEvent.EventType,
                    streamEvent.EventId,
                    log,
                    QylSerializerContext.Default.LogStreamEvent,
                    ct).ConfigureAwait(false);
            }
            else if (streamEvent.Heartbeat is { } heartbeat)
            {
                await WriteSseEventAsync(
                    context.Response,
                    streamEvent.EventType,
                    null,
                    heartbeat,
                    QylSerializerContext.Default.HeartbeatEvent,
                    ct).ConfigureAwait(false);
            }
        }
    }

    internal static async IAsyncEnumerable<(
            string EventType,
            long? EventId,
            LogStreamEvent? Log,
            HeartbeatEvent? Heartbeat)>
        StreamLogEventsAsync(
        IQylStore store,
        string projectId,
        string? serviceName,
        int? minSeverity,
        string? query,
        long? afterIngestSequence,
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var rows = await store.GetLogStreamPageAsync(
                projectId,
                serviceName: serviceName,
                minSeverity: minSeverity,
                search: query,
                afterIngestSequence: afterIngestSequence,
                limit: 250,
                ct: ct).ConfigureAwait(false);

            if (rows.Count > 0)
            {
                foreach (var log in rows)
                {
                    yield return ("log", log.IngestSequence, new LogStreamEvent
                        {
                            Type = "log",
                            Data = LogMapper.ToContract(log),
                            Timestamp = QylTimeConversions.NanosToDateTimeOffset(log.TimeUnixNano)
                        },
                        null);
                    afterIngestSequence = log.IngestSequence;
                }
            }
            else
            {
                yield return ("heartbeat", null, null, new HeartbeatEvent
                {
                    Type = "heartbeat",
                    Timestamp = TimeProvider.System.GetUtcNow()
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
    }

    private static long? ParseLastEventId(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) && sequence >= 0
            ? sequence
            : null;

    internal static async Task<IResult> GetGenAiEtlAuditAsync(
        HttpContext httpContext,
        GenAiEtlAuditService auditService,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseGenAiEtlAudit(httpContext.Request, out var query) is { } queryError)
            return queryError;
        if (!ContractLimits.TryResolve(
                query.Limit,
                ContractLimits.GenAiEtlAuditDefaultLimit,
                ContractLimits.GenAiEtlAuditMaxLimit,
                out var limit))
        {
            return InvalidLimit(query.Limit, ContractLimits.GenAiEtlAuditMaxLimit);
        }
        if (!TryResolveAuditPeriod(
                query.StartTime,
                query.EndTime,
                timeProvider,
                out var periodStart,
                out var periodEnd,
                out var periodError))
        {
            return periodError!;
        }

        var report = await auditService.GetReportAsync(
                ResolveProjectScope(httpContext),
                periodStart,
                periodEnd,
                limit,
                ct)
            .ConfigureAwait(false);
        return Results.Ok(report);
    }

    internal static async Task<IResult> EvaluateGenAiEtlAuditAsync(
        HttpContext httpContext,
        GenAiEtlAuditService auditService,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseGenAiEtlAuditPeriod(httpContext.Request, out var query) is { } queryError)
            return queryError;
        if (!TryResolveAuditPeriod(
                query.StartTime,
                query.EndTime,
                timeProvider,
                out var periodStart,
                out var periodEnd,
                out var periodError))
        {
            return periodError!;
        }

        if (!httpContext.Request.HasJsonContentType())
        {
            return ContractErrorResults.Validation(
                "body",
                "Content-Type must be application/json.",
                "body.unsupported_content_type",
                httpContext.Request.ContentType);
        }

        GenAiEtlAuditEvaluationRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync(
                    httpContext.Request.Body,
                    QylSerializerContext.Default.GenAiEtlAuditEvaluationRequest,
                    ct)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return ContractErrorResults.Validation(
                "body",
                "Request body must be a valid ETL audit evaluation document.",
                "body.invalid_json");
        }

        if (request is null)
        {
            return ContractErrorResults.Validation(
                "body",
                "Request body is required.",
                "body.required");
        }

        var outcome = await auditService.EvaluateAsync(
                ResolveProjectScope(httpContext),
                periodStart,
                periodEnd,
                request,
                ct)
            .ConfigureAwait(false);
        if (outcome.Failure is { } failure)
        {
            return ContractErrorResults.Validation(
                failure.Field,
                failure.Message,
                failure.Code,
                failure.RejectedValue);
        }

        return Results.Ok(outcome.Response);
    }

    private static bool TryResolveAuditPeriod(
        DateTimeOffset? requestedStart,
        DateTimeOffset? requestedEnd,
        TimeProvider timeProvider,
        out DateTimeOffset periodStart,
        out DateTimeOffset periodEnd,
        out IResult? error)
    {
        var now = timeProvider.GetUtcNow();
        var defaultEnd = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        periodEnd = (requestedEnd ?? defaultEnd).ToUniversalTime();
        periodStart = (requestedStart ?? periodEnd.AddDays(-30)).ToUniversalTime();
        if (periodStart < periodEnd &&
            periodEnd - periodStart <= TimeSpan.FromDays(ContractLimits.GenAiEtlAuditMaxPeriodDays))
        {
            error = null;
            return true;
        }

        if (periodStart < periodEnd)
        {
            error = ContractErrorResults.Validation(
                "startTime",
                $"The ETL audit period must not exceed {ContractLimits.GenAiEtlAuditMaxPeriodDays} days.",
                "period.too_large",
                periodStart.ToString("O", CultureInfo.InvariantCulture));
            return false;
        }

        error = ContractErrorResults.Validation(
            "startTime",
            "startTime must be earlier than endTime.",
            "period.invalid",
            requestedStart?.ToString("O", CultureInfo.InvariantCulture));
        return false;
    }

    private static async Task WriteSseEventAsync<T>(
        HttpResponse response,
        string eventType,
        long? eventId,
        T streamEvent,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken ct)
    {
        if (eventId.HasValue)
        {
            await response.WriteAsync("id: ", ct).ConfigureAwait(false);
            await response.WriteAsync(eventId.Value.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
            await response.WriteAsync("\n", ct).ConfigureAwait(false);
        }

        await response.WriteAsync("event: ", ct).ConfigureAwait(false);
        await response.WriteAsync(eventType, ct).ConfigureAwait(false);
        await response.WriteAsync("\ndata: ", ct).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(
            response.Body,
            streamEvent,
            jsonTypeInfo,
            ct).ConfigureAwait(false);
        await response.WriteAsync("\n\n", ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }


    private static async Task<IResult> IngestOtlpProfilesAsync(
        HttpContext context,
        IQylStore store,
        CancellationToken ct)
    {
        var encoding = OtlpPayloadEncoding.Json;
        ExportProfilesServiceRequest otlpData;
        try
        {
            encoding = OtlpPayloadParser.GetEncoding(context.Request.ContentType);
            otlpData = await OtlpPayloadParser.ParseProfilesRequestAsync(context.Request, encoding, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is OtlpUnsupportedMediaTypeException or OtlpUnsupportedContentEncodingException)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpProfilesEndpoint");
            OtlpProfilesLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status415UnsupportedMediaType,
                encoding,
                "Content-Type must be application/x-protobuf or application/json; Content-Encoding must be gzip, identity, or absent.");
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpProfilesEndpoint");
            OtlpProfilesLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                encoding,
                "The OTLP profiles payload could not be decoded.");
        }

        try
        {
            if (otlpData.ResourceProfiles.Count is 0)
                return OtlpHttpResult.Success(
                    encoding,
                    new ExportProfilesServiceResponse());

            var profileBatch = OtlpConverter.ConvertProfiles(otlpData);
            var results = IngestionStorageMapper.ToProfileStorageRows(profileBatch);

            if (results.Count is 0)
                return OtlpHttpResult.Success(
                    encoding,
                    new ExportProfilesServiceResponse());

            await store.InsertProfilesAsync(results, ct);
            return OtlpHttpResult.Success(
                encoding,
                new ExportProfilesServiceResponse());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpProfilesEndpoint");
            OtlpProfilesLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                encoding,
                "The OTLP profiles payload is invalid.");
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpProfilesEndpoint");
            OtlpProfilesLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                encoding,
                "The collector could not persist the OTLP profiles payload.");
        }
    }

    internal static async Task<IResult> GetProfilesAsync(
        HttpContext httpContext,
        IQylStore store,
        string? sessionId,
        string? traceId,
        string? serviceName,
        string? sampleType,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseProfiles(httpContext.Request, out var limit) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(limit, ContractLimits.DefaultPageLimit, ContractLimits.ProfileMaxLimit,
                out var boundedLimit))
        {
            return InvalidLimit(limit, ContractLimits.ProfileMaxLimit);
        }
        var profiles = await store.GetProfilesAsync(
            ResolveProjectScope(httpContext),
            sessionId,
            traceId,
            serviceName: serviceName,
            sampleType: sampleType,
            limit: boundedLimit,
            ct: ct);

        return Results.Ok(ProfileMapper.ToContracts(profiles));
    }

    private static async Task<IResult> GetProfileByIdAsync(
        HttpContext httpContext,
        string profileId,
        IQylStore store,
        CancellationToken ct)
    {
        var detail = await store.GetProfileDetailAsync(profileId, ResolveProjectScope(httpContext), ct: ct);
        return detail is not null
            ? Results.Ok(ProfileMapper.ToContract(detail))
            : ContractErrorResults.NotFound("profile", profileId);
    }

    internal static async Task<IResult> GetTraceProfilesAsync(
        HttpContext httpContext,
        string traceId,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseProfiles(httpContext.Request, out var limit) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(limit, ContractLimits.DefaultPageLimit, ContractLimits.ProfileMaxLimit,
                out var boundedLimit))
        {
            return InvalidLimit(limit, ContractLimits.ProfileMaxLimit);
        }

        var profiles = await store.GetProfilesAsync(
            ResolveProjectScope(httpContext), traceId: traceId, limit: boundedLimit, ct: ct);
        return Results.Ok(ProfileMapper.ToContracts(profiles));
    }

    internal static async Task<IResult> GetSpanProfilesAsync(
        HttpContext httpContext,
        string spanId,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseProfiles(httpContext.Request, out var limit) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(limit, ContractLimits.DefaultPageLimit, ContractLimits.ProfileMaxLimit,
                out var boundedLimit))
        {
            return InvalidLimit(limit, ContractLimits.ProfileMaxLimit);
        }

        var profiles = await store.GetProfilesAsync(ResolveProjectScope(httpContext), spanId: spanId, limit: boundedLimit, ct: ct);
        return Results.Ok(ProfileMapper.ToContracts(profiles));
    }

    private static IResult InvalidLimit(int? limit, int maximum) =>
        ContractErrorResults.Validation(
            "limit",
            $"Limit must be between 1 and {maximum}.",
            "limit.out_of_range",
            limit?.ToString(CultureInfo.InvariantCulture));

    private static string ResolveProjectScope(HttpContext httpContext) =>
        ProjectScope.Normalize(httpContext.Request.Headers["X-Qyl-Project"].FirstOrDefault());

    private static Task FallbackHandler(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        if (IsPathOrDescendant(path, "/api") ||
            OtlpConstants.IsOtlpNamespacePath(path) ||
            IsPathOrDescendant(path, "/assets"))
        {
            context.Response.StatusCode = 404;
            return Task.CompletedTask;
        }

        if (EmbeddedDashboardExtensions.HasEmbeddedDashboard())
        {
            context.Response.StatusCode = 404;
            return Task.CompletedTask;
        }

        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var webRootPath = env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            context.Response.StatusCode = 404;
            return context.Response.WriteAsync(
                "Dashboard not found. Build with: dotnet run --project eng/build/build.csproj -- FrontendBuild");
        }

        var indexPath = Path.Combine(webRootPath, "index.html");
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html";
            return context.Response.SendFileAsync(indexPath);
        }

        context.Response.StatusCode = 404;
        return context.Response.WriteAsync(
            "Dashboard not found. Build with: dotnet run --project eng/build/build.csproj -- FrontendBuild");
    }

    private static bool IsPathOrDescendant(string path, string root) =>
        path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);

    private static class ContractLimits
    {
        public const int DefaultPageLimit = 100;
        public const int SessionMaxLimit = 1_000;
        public const int TraceMaxLimit = 1_000;
        public const int LogMaxLimit = 10_000;
        public const int MetricMaxLimit = 1_000;
        public const int ProfileMaxLimit = 1_000;
        public const int GenAiEtlAuditDefaultLimit = 25;
        public const int GenAiEtlAuditMaxLimit = 100;
        public const int GenAiEtlAuditMaxPeriodDays = 180;
        public const int MinimumLogSeverity = 0;
        public const int MinimumStreamSeverity = 1;
        public const int MaximumLogSeverity = 24;

        public static bool TryResolve(int? requested, int defaultValue, int maxValue, out int resolved)
        {
            resolved = requested ?? defaultValue;
            return resolved is >= 1 && resolved <= maxValue;
        }
    }
}

internal static partial class OtlpLogsLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP logs payload")]
    public static partial void FailedToProcessPayload(ILogger logger, Exception exception);
}

internal static partial class OtlpMetricsLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP metrics payload")]
    public static partial void FailedToProcessPayload(ILogger logger, Exception exception);
}

internal static partial class OtlpProfilesLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP profiles payload")]
    public static partial void FailedToProcessPayload(ILogger logger, Exception exception);
}

internal static partial class OtlpTracesLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP traces payload")]
    public static partial void FailedToProcessPayload(ILogger logger, Exception exception);
}
