using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.Streaming;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Grpc;
using Qyl.Collector.Storage;
using ExportLogsServiceRequest = OpenTelemetry.Proto.Collector.Logs.V1.ExportLogsServiceRequest;
using ExportLogsServiceResponse = OpenTelemetry.Proto.Collector.Logs.V1.ExportLogsServiceResponse;
using ExportMetricsServiceRequest = OpenTelemetry.Proto.Collector.Metrics.V1.ExportMetricsServiceRequest;
using ExportMetricsServiceResponse = OpenTelemetry.Proto.Collector.Metrics.V1.ExportMetricsServiceResponse;
using ExportTraceServiceRequest = OpenTelemetry.Proto.Collector.Trace.V1.ExportTraceServiceRequest;
using ExportTraceServiceResponse = OpenTelemetry.Proto.Collector.Trace.V1.ExportTraceServiceResponse;

namespace Qyl.Collector.Hosting;

internal static partial class CollectorEndpointExtensions
{
    public static WebApplication MapQylCollectorEndpoints(this WebApplication app)
    {
        app.MapGrpcService<TraceServiceImpl>();
        app.MapGrpcService<LogsServiceImpl>();
        app.MapGrpcService<MetricsServiceImpl>();

        var otlp = app.MapGroup("/v1");
        otlp.MapPost("/traces", IngestOtlpTracesAsync);
        otlp.MapPost("/logs", IngestOtlpLogsAsync);
        otlp.MapPost("/metrics", IngestOtlpMetricsAsync);

        // Every product route names the key role it requires (QYL-A2): the query routes `read`,
        // creating and updating a resource `write`, deleting one `delete`. Running a saved query
        // is a read even though it is a POST.
        var api = app.MapGroup("/api/v1");

        api.MapGet("/sessions", GetSessionsAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/sessions/stats", GetSessionStatsAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/sessions/{session_id}", GetSessionByIdAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/sessions/{session_id}/traces", GetSessionTracesAsync).RequireKeyRole(ApiKeyRoles.Read);

        api.MapGet("/traces", GetTracesAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/traces/{trace_id}", GetTraceAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/traces/{trace_id}/spans", GetTraceSpansAsync).RequireKeyRole(ApiKeyRoles.Read);

        api.MapGet("/metrics", GetMetricsAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/metrics/{metric_name}/series", GetMetricSeriesAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/metrics/{metric_name}/series/{series_id}/points", GetMetricPointsAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/metrics/{metric_name}/query", QueryMetricAsync).RequireKeyRole(ApiKeyRoles.Read);

        api.MapGet("/logs", GetLogsAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/stream/logs", StreamLogsAsync).RequireKeyRole(ApiKeyRoles.Read);

        api.MapPost("/annotations", CreateAnnotationAsync).RequireKeyRole(ApiKeyRoles.Write);
        api.MapGet("/annotations", ListAnnotationsAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/annotations/{annotation_id}", GetAnnotationAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapPatch("/annotations/{annotation_id}", UpdateAnnotationAsync).RequireKeyRole(ApiKeyRoles.Write);
        api.MapDelete("/annotations/{annotation_id}", DeleteAnnotationAsync).RequireKeyRole(ApiKeyRoles.Delete);

        api.MapPost("/saved-queries", CreateSavedQueryAsync).RequireKeyRole(ApiKeyRoles.Write);
        api.MapGet("/saved-queries", ListSavedQueriesAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapGet("/saved-queries/{saved_query_id}", GetSavedQueryAsync).RequireKeyRole(ApiKeyRoles.Read);
        api.MapPatch("/saved-queries/{saved_query_id}", UpdateSavedQueryAsync).RequireKeyRole(ApiKeyRoles.Write);
        api.MapDelete("/saved-queries/{saved_query_id}", DeleteSavedQueryAsync).RequireKeyRole(ApiKeyRoles.Delete);
        api.MapPost("/saved-queries/{saved_query_id}:run", RunSavedQueryAsync).RequireKeyRole(ApiKeyRoles.Read);

        // Read operations share typed contract dispatch; failure-status changes
        // retain their explicit write route below.
        api.MapPost("/analysis/triage", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("triage");
        api.MapPost("/analysis/list_failures", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("list_failures");
        api.MapPost("/analysis/get_failure_context", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("get_failure_context");
        api.MapPost("/analysis/compare_groups", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("compare_groups");
        api.MapPost("/analysis/compare_traces", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("compare_traces");
        api.MapPost("/analysis/verify_fix", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("verify_fix");
        api.MapPost("/analysis/aggregate_telemetry", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("aggregate_telemetry");
        api.MapPost("/analysis/discover_attributes", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("discover_attributes");
        api.MapPost("/analysis/get_agent_run", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("get_agent_run");
        api.MapPost("/analysis/list_releases", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("list_releases");
        api.MapPost("/analysis/get_service_map", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("get_service_map");
        api.MapPost("/analysis/check_telemetry_health", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("check_telemetry_health");
        api.MapPost("/analysis/get_mcp_stats", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("get_mcp_stats");
        api.MapPost("/analysis/list_sessions", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("list_sessions");
        api.MapPost("/analysis/list_traces", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("list_traces");
        api.MapPost("/analysis/get_trace", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("get_trace");
        api.MapPost("/analysis/search_logs", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("search_logs");
        api.MapPost("/analysis/list_metrics", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("list_metrics");
        api.MapPost("/analysis/get_metric_series", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("get_metric_series");
        api.MapPost("/analysis/query_metric", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("query_metric");
        api.MapPost("/analysis/ci_log", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("ci_log");
        api.MapPost("/analysis/list_saved_queries", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("list_saved_queries");
        api.MapPost("/analysis/run_saved_query", AnalyzeReadEndpointAsync).RequireKeyRole(ApiKeyRoles.Read).WithName("run_saved_query");

        api.MapPatch("/failures/{failure_id}", UpdateFailureStatusAsync).RequireKeyRole(ApiKeyRoles.Write);

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
        catch (OtlpPayloadTooLargeException ex)
        {
            // OTLP 1.11 (request size limits): a body over the decompressed bound is 413, never 400.
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpTracesEndpoint");
            OtlpTracesLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status413PayloadTooLarge,
                encoding,
                OtlpPayloadParser.PayloadTooLargeMessage);
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
            var spans = IngestionStorageMapper.ToSpanStorageRows(
                traceBatch, AuthenticatedProject.ForHttpIngest(context));

            if (spans.Count > 0) await store.EnqueueAsync(new SpanBatch(spans), ct);
            return OtlpHttpResult.Success(encoding, OtlpExportAck.Create(traceBatch));
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
        TimeProvider timeProvider,
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
        catch (OtlpPayloadTooLargeException ex)
        {
            // OTLP 1.11 (request size limits): a body over the decompressed bound is 413, never 400.
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpLogsEndpoint");
            OtlpLogsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status413PayloadTooLarge,
                encoding,
                OtlpPayloadParser.PayloadTooLargeMessage);
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

            var logBatch = OtlpConverter.ConvertLogs(otlpData, timeProvider);
            var logs = IngestionStorageMapper.ToLogStorageRows(
                logBatch, AuthenticatedProject.ForHttpIngest(context));

            if (logs.Count > 0) await store.InsertLogsAsync(logs, ct);
            return OtlpHttpResult.Success(encoding, OtlpExportAck.Create(logBatch));
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
        catch (OtlpPayloadTooLargeException ex)
        {
            // OTLP 1.11 (request size limits): a body over the decompressed bound is 413, never 400.
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpMetricsEndpoint");
            OtlpMetricsLog.FailedToProcessPayload(logger, ex);
            return OtlpHttpResult.Failure(
                StatusCodes.Status413PayloadTooLarge,
                encoding,
                OtlpPayloadParser.PayloadTooLargeMessage);
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
                return OtlpHttpResult.Success(encoding, new ExportMetricsServiceResponse());

            var batch = OtlpConverter.ConvertMetrics(otlpData);
            var write = MetricStorageMapper.ToStorageRows(
                batch, AuthenticatedProject.ForHttpIngest(context));

            if (write.Observations.Count > 0)
                await store.InsertMetricsAsync(write.Series, write.Observations, ct);

            return OtlpHttpResult.Success(encoding, OtlpExportAck.Create(batch));
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

    internal static async Task<IResult> GetMetricsAsync(
        HttpContext httpContext,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseMetrics(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit,
                ContractLimits.MaxPageLimit, out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.MaxPageLimit);
        }

        MetricCatalogCursor? cursor = null;
        if (query.Cursor is { } encoded)
        {
            if (!PageCursorCodec.TryDecode(encoded, PageCursorKind.Metrics, 2, out var fields))
                return InvalidCursor(encoded);
            cursor = new MetricCatalogCursor(fields[0], fields[1]);
        }

        // One extra row makes HasMore a fact rather than the coincidence of a page that exactly
        // fills the limit; every list pages the same way.
        var metrics = await store.ListMetricsAsync(
            ResolveProjectScope(httpContext),
            query.NamePrefix,
            boundedLimit + 1,
            cursor,
            ct: ct);

        var hasMore = metrics.Count > boundedLimit;
        IReadOnlyList<MetricCatalogEntry> page = hasMore ? [.. metrics.Take(boundedLimit)] : metrics;
        return Results.Ok(new CursorPageMetricDescriptor
        {
            Items = MetricMapper.ToContracts(page),
            HasMore = hasMore,
            NextCursor = hasMore ? PageCursorCodec.Encode(PageCursorKind.Metrics, page[^1].MetricName, page[^1].InstrumentId) : null
        });
    }

    internal static async Task<IResult> GetMetricSeriesAsync(
        HttpContext httpContext,
        [FromRoute(Name = "metric_name")] string metricName,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseMetricSeries(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit,
                ContractLimits.MaxPageLimit, out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.MaxPageLimit);
        }

        MetricSeriesPageCursor? cursor = null;
        if (query.Cursor is { } encoded)
        {
            if (!PageCursorCodec.TryDecode(encoded, PageCursorKind.MetricSeries, 2, out var fields) ||
                !PageCursorCodec.TryParseHex(fields[0], out var lastSeen))
            {
                return InvalidCursor(encoded);
            }

            cursor = new MetricSeriesPageCursor(lastSeen, fields[1]);
        }

        var projectId = ResolveProjectScope(httpContext);
        var series = await store.ListMetricSeriesAsync(
            projectId,
            metricName,
            query.Matchers,
            boundedLimit + 1,
            cursor,
            ct: ct);

        // An empty first page under a name that was never recorded is a 404: the caller asked
        // about an instrument, not about a filter, and "no such metric" is the answer that
        // tells it to stop asking. A name that exists but matches no filter is an empty 200.
        if (series.Count is 0 && query.Matchers.Count is 0 && cursor is null)
            return ContractErrorResults.NotFound("metric", metricName);

        var hasMore = series.Count > boundedLimit;
        IReadOnlyList<MetricSeriesRow> page = hasMore ? [.. series.Take(boundedLimit)] : series;
        return Results.Ok(new CursorPageMetricSeries
        {
            Items = MetricMapper.ToContracts(page),
            HasMore = hasMore,
            NextCursor = hasMore
                ? PageCursorCodec.Encode(
                    PageCursorKind.MetricSeries,
                    PageCursorCodec.Hex(page[^1].LastSeenUnixNano),
                    page[^1].SeriesId)
                : null
        });
    }

    internal static async Task<IResult> QueryMetricAsync(
        HttpContext httpContext,
        [FromRoute(Name = "metric_name")] string metricName,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseMetricQuery(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (query.StartTime is not { } startTime)
            return MissingRequired("start_time");
        if (query.EndTime is not { } endTime)
            return MissingRequired("end_time");
        if (endTime < startTime)
        {
            return ContractErrorResults.Validation(
                "end_time",
                "The range end must not precede its start.",
                "range.inverted",
                endTime.ToString("O", CultureInfo.InvariantCulture));
        }

        var stepMs = query.StepMs ?? ContractLimits.MetricDefaultStepMs;
        if (stepMs is < 1 or > ContractLimits.MetricMaxStepMs)
        {
            return ContractErrorResults.Validation(
                "step_ms",
                $"Step must be between 1 and {ContractLimits.MetricMaxStepMs} milliseconds.",
                "step.out_of_range",
                stepMs.ToString(CultureInfo.InvariantCulture));
        }

        if (!ContractLimits.TryResolve(query.SeriesLimit, ContractLimits.MetricSeriesDefaultLimit,
                ContractLimits.MetricSeriesMaxLimit, out var boundedSeriesLimit))
        {
            return ContractErrorResults.Validation(
                "series_limit",
                $"Series limit must be between 1 and {ContractLimits.MetricSeriesMaxLimit}.",
                "limit.out_of_range",
                query.SeriesLimit?.ToString(CultureInfo.InvariantCulture));
        }

        var projectId = ResolveProjectScope(httpContext);
        if ((await store.ListMetricsAsync(projectId, metricName, limit: 1, matchers: query.Matchers, seriesId: query.SeriesId, ct: ct))
            .FirstOrDefault(entry => string.Equals(entry.MetricName, metricName, StringComparison.Ordinal)) is not { } descriptor)
            return ContractErrorResults.NotFound(query.SeriesId is null ? "metric" : "metric series", query.SeriesId ?? metricName);

        // Ask for one extra stream so the truncation flag reports a fact rather than the
        // coincidence of the result exactly filling the limit.
        IReadOnlyList<MetricQuerySeries> series;
        try
        {
            series = await store.QueryMetricAsync(new MetricRangeQuery
            {
                ProjectId = projectId,
                MetricName = metricName,
                SeriesId = query.SeriesId,
                StartUnixNano = QylTimeConversions.ToUnixNanoUnsigned(startTime.ToUniversalTime()),
                EndUnixNano = QylTimeConversions.ToUnixNanoUnsigned(endTime.ToUniversalTime()),
                StepUnixNano = (ulong)stepMs * 1_000_000UL,
                Aggregation = query.Aggregation,
                GroupBy = query.GroupBy,
                Matchers = query.Matchers,
                SeriesLimit = boundedSeriesLimit + 1
            }, ct);
        }
        catch (MetricQueryConflictException exception)
        {
            return ContractErrorResults.Conflict(metricName, exception.Message);
        }
        catch (MetricQueryRequiresHistogramException exception)
        {
            return ContractErrorResults.Validation("aggregation", exception.Message,
                "aggregation.requires_histogram", httpContext.Request.Query["aggregation"].ToString());
        }
        catch (MetricProjectionUnavailableException exception)
        {
            return ContractErrorResults.Validation("aggregation", exception.Message, "metric.projection_unavailable");
        }

        var truncated = series.Count > boundedSeriesLimit;
        return Results.Ok(MetricMapper.ToQueryResult(
            descriptor,
            query.Aggregation,
            stepMs,
            startTime,
            endTime,
            truncated ? [.. series.Take(boundedSeriesLimit)] : series,
            truncated));
    }

    private static IResult MissingRequired(string field) =>
        ContractErrorResults.Validation(
            field,
            "Value is required.",
            "query.missing");

    internal static async Task<IResult> GetSessionsAsync(
        HttpContext httpContext,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseSessions(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit, ContractLimits.MaxPageLimit,
                out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.MaxPageLimit);
        }

        SessionPageCursor? cursor = null;
        if (query.Cursor is { } encoded)
        {
            if (!PageCursorCodec.TryDecode(encoded, PageCursorKind.Sessions, 2, out var fields) ||
                !PageCursorCodec.TryParseHex(fields[0], out var lastActivity))
            {
                return InvalidCursor(encoded);
            }

            cursor = new SessionPageCursor(lastActivity, fields[1]);
        }

        var activeCutoffUnixNano = QylSessionActivity.GetActiveCutoffUnixNano(timeProvider.GetUtcNow());
        var sessions = await store.GetSessionsAsync(
            ResolveProjectScope(httpContext),
            activeCutoffUnixNano,
            boundedLimit + 1,
            cursor,
            query.IsActive,
            query.StartTime,
            query.EndTime,
            ct: ct).ConfigureAwait(false);

        var hasMore = sessions.Count > boundedLimit;
        IReadOnlyList<SessionQueryRow> page = hasMore ? [.. sessions.Take(boundedLimit)] : sessions;
        return Results.Ok(SessionMapper.ToPage(
            page,
            activeCutoffUnixNano,
            hasMore,
            hasMore
                ? PageCursorCodec.Encode(
                    PageCursorKind.Sessions,
                    PageCursorCodec.Hex(page[^1].LastActivityUnixNano),
                    page[^1].SessionId)
                : null));
    }

    private static async Task<IResult> GetSessionByIdAsync(
        HttpContext httpContext,
        [FromRoute(Name = "session_id")] string sessionId,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var activeCutoffUnixNano = QylSessionActivity.GetActiveCutoffUnixNano(timeProvider.GetUtcNow());
        return await store.GetSessionAsync(sessionId, ResolveProjectScope(httpContext), ct: ct).ConfigureAwait(false) is not { } session
            ? ContractErrorResults.NotFound("session", sessionId)
            : Results.Ok(SessionMapper.ToContract(session, activeCutoffUnixNano));
    }

    internal static async Task<IResult> GetSessionStatsAsync(
        HttpContext httpContext,
        IQylStore store,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseSessionStats(httpContext.Request, out var query) is { } queryError)
            return queryError;

        var activeCutoffUnixNano = QylSessionActivity.GetActiveCutoffUnixNano(timeProvider.GetUtcNow());
        var stats = await store.GetSessionStatsAsync(
                ResolveProjectScope(httpContext), activeCutoffUnixNano, query.StartTime, query.EndTime, ct: ct)
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

    // Session traces are paged like the trace list (QYL-A4): a long-lived session can touch
    // thousands of traces, and an unbounded response is one no client can hold.
    private static async Task<IResult> GetSessionTracesAsync(
        HttpContext httpContext,
        [FromRoute(Name = "session_id")] string sessionId,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParsePage(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit,
                ContractLimits.MaxPageLimit, out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.MaxPageLimit);
        }

        TracePageCursor? cursor = null;
        if (query.Cursor is { } encoded)
        {
            if (!PageCursorCodec.TryDecode(encoded, PageCursorKind.SessionTraces, out var decoded))
                return InvalidCursor(encoded);
            cursor = decoded;
        }

        var projectId = ResolveProjectScope(httpContext);
        if (await store.GetSessionAsync(sessionId, projectId, ct: ct).ConfigureAwait(false) is null)
            return ContractErrorResults.NotFound("session", sessionId);

        var page = await store.GetTracePageAsync(new TracePageQuery
        {
            ProjectId = projectId,
            Limit = boundedLimit,
            Cursor = cursor,
            SessionId = sessionId
        }, ct).ConfigureAwait(false);
        return Results.Ok(ToTracePage(page, PageCursorKind.SessionTraces));
    }

    internal static async Task<IResult> GetTracesAsync(
        HttpContext httpContext,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseTraces(httpContext.Request, out var parameters) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(parameters.Limit, ContractLimits.DefaultPageLimit, ContractLimits.MaxPageLimit,
                out var boundedLimit))
        {
            return InvalidLimit(parameters.Limit, ContractLimits.MaxPageLimit);
        }

        TracePageCursor? cursor = null;
        if (parameters.Cursor is { } encodedCursor)
        {
            if (!PageCursorCodec.TryDecode(encodedCursor, PageCursorKind.Traces, out var decodedCursor))
                return InvalidCursor(encodedCursor);
            cursor = decodedCursor;
        }

        var page = await store.GetTracePageAsync(new TracePageQuery
            {
                ProjectId = ResolveProjectScope(httpContext),
                Limit = boundedLimit,
                Cursor = cursor,
                StartUnixNano = ToUnixNano(parameters.StartTime),
                EndUnixNano = ToUnixNano(parameters.EndTime),
                ServiceName = parameters.ServiceName,
                ErrorsOnly = parameters.ErrorsOnly is true
            }, ct)
            .ConfigureAwait(false);
        return Results.Ok(ToTracePage(page, PageCursorKind.Traces));
    }

    private static CursorPageTrace ToTracePage(TraceStoragePage page, PageCursorKind kind) =>
        new()
        {
            Items = [.. page.Items.Select(static item => SpanMapper.ToTrace(item.TraceId, SpanMapper.ToContracts(item.Spans)))],
            HasMore = page.HasMore,
            NextCursor = page is { HasMore: true, Items.Count: > 0 }
                ? PageCursorCodec.Encode(kind, new TracePageCursor(page.Items[^1].ActivityUnixNano, page.Items[^1].TraceId))
                : null
        };

    private static async Task<IResult> GetTraceSpansAsync(
        HttpContext httpContext,
        [FromRoute(Name = "trace_id")] string traceId,
        IQylStore store,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParsePage(httpContext.Request, out var query) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(query.Limit, ContractLimits.DefaultPageLimit, ContractLimits.MaxPageLimit,
                out var boundedLimit))
        {
            return InvalidLimit(query.Limit, ContractLimits.MaxPageLimit);
        }

        SpanPageCursor? cursor = null;
        if (query.Cursor is { } encoded)
        {
            if (!PageCursorCodec.TryDecode(encoded, PageCursorKind.TraceSpans, 2, out var fields) ||
                !PageCursorCodec.TryParseHex(fields[0], out var start) ||
                !ContractIdentifiers.IsSpanId(fields[1]))
            {
                return InvalidCursor(encoded);
            }

            cursor = new SpanPageCursor(start, fields[1]);
        }

        var projectId = ResolveProjectScope(httpContext);
        var spans = await store.GetTraceSpanPageAsync(traceId, projectId, cursor, boundedLimit + 1, ct)
            .ConfigureAwait(false);
        if (spans.Count is 0 &&
            (cursor is null || !await store.TraceExistsAsync(projectId, traceId, ct).ConfigureAwait(false)))
        {
            return ContractErrorResults.NotFound("trace", traceId);
        }

        var hasMore = spans.Count > boundedLimit;
        IReadOnlyList<SpanStorageRow> page = hasMore ? [.. spans.Take(boundedLimit)] : spans;
        return Results.Ok(new CursorPageSpan
        {
            Items = SpanMapper.ToContracts(page),
            HasMore = hasMore,
            NextCursor = hasMore
                ? PageCursorCodec.Encode(
                    PageCursorKind.TraceSpans,
                    PageCursorCodec.Hex(page[^1].StartTimeUnixNano),
                    page[^1].SpanId)
                : null
        });
    }

    private static async Task<IResult> GetTraceAsync(
        HttpContext httpContext,
        [FromRoute(Name = "trace_id")] string traceId,
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
        [FromQuery(Name = "session_id")] string? sessionId,
        [FromQuery(Name = "trace_id")] string? traceId,
        [FromQuery(Name = "service_name")] string? serviceName,
        string? level,
        string? query,
        CancellationToken ct)
    {
        if (ContractQueryParser.ParseLogs(httpContext.Request, out var typedQuery) is { } queryError)
            return queryError;

        if (!ContractLimits.TryResolve(typedQuery.Limit, ContractLimits.DefaultPageLimit, ContractLimits.MaxPageLimit,
                out var boundedLimit))
        {
            return InvalidLimit(typedQuery.Limit, ContractLimits.MaxPageLimit);
        }

        if (typedQuery.SeverityMin is < ContractLimits.MinimumLogSeverity or > ContractLimits.MaximumLogSeverity)
        {
            return ContractErrorResults.Validation(
                "severity_min",
                $"Severity must be between {ContractLimits.MinimumLogSeverity} and {ContractLimits.MaximumLogSeverity}.",
                "severity.out_of_range",
                typedQuery.SeverityMin.Value.ToString(CultureInfo.InvariantCulture));
        }

        LogPageCursor? cursor = null;
        if (typedQuery.Cursor is { } encoded)
        {
            if (!TryDecodeLogCursor(encoded, PageCursorKind.Logs, out var decoded))
                return InvalidCursor(encoded);
            cursor = decoded;
        }

        var logs = await store.GetLogsAsync(
            ResolveProjectScope(httpContext),
            sessionId: sessionId,
            traceId: traceId,
            severityText: level,
            minSeverity: typedQuery.SeverityMin,
            search: query,
            start: ToUnixNano(typedQuery.StartTime),
            before: ToUnixNano(typedQuery.EndTime),
            serviceName: serviceName,
            limit: boundedLimit + 1,
            cursor: cursor,
            ct: ct);

        return Results.Ok(ToLogPage(logs, boundedLimit, PageCursorKind.Logs));
    }

    private static CursorPageLogRecord ToLogPage(
        IReadOnlyList<LogStorageRow> logs,
        int limit,
        PageCursorKind kind,
        params string[] cursorPrefix)
    {
        var hasMore = logs.Count > limit;
        IReadOnlyList<LogStorageRow> page = hasMore ? [.. logs.Take(limit)] : logs;
        return new CursorPageLogRecord
        {
            Items = LogMapper.ToContracts(page),
            HasMore = hasMore,
            NextCursor = hasMore
                ? PageCursorCodec.Encode(
                    kind,
                    [.. cursorPrefix, PageCursorCodec.Hex(page[^1].EffectiveTimeUnixNano()), page[^1].LogId])
                : null
        };
    }

    private static bool TryDecodeLogCursor(string encoded, PageCursorKind kind, out LogPageCursor cursor)
    {
        cursor = default;
        if (!PageCursorCodec.TryDecode(encoded, kind, 2, out var fields)) return false;
        ulong? time = null;
        if (fields[0].Length > 0)
        {
            if (!PageCursorCodec.TryParseHex(fields[0], out var known)) return false;
            // Cursors issued before nullable log times represented unknown time as zero.
            time = known == 0 ? null : known;
        }
        cursor = new LogPageCursor(time, fields[1]);
        return true;
    }

    private static ulong? ToUnixNano(DateTimeOffset? value) =>
        value is { } timestamp ? QylTimeConversions.ToUnixNanoUnsigned(timestamp.ToUniversalTime()) : null;

    private static IResult InvalidCursor(string cursor) =>
        ContractErrorResults.Validation(
            "cursor",
            "Cursor is not a next_cursor this list served.",
            "cursor.invalid",
            cursor);

    internal static async Task StreamLogsAsync(
        HttpContext context,
        IQylStore store,
        CollectorStreamCapacity streamCapacity,
        TimeProvider timeProvider,
        [FromQuery(Name = "service_name")] string? serviceName,
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
                "min_severity",
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
                           timeProvider,
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
        TimeProvider timeProvider,
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
                            Timestamp = log.EffectiveTimeUnixNano() is { } timestamp ? QylTimeConversions.NanosToDateTimeOffset(timestamp) : null
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
                    Timestamp = timeProvider.GetUtcNow()
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(1), timeProvider, ct).ConfigureAwait(false);
        }
    }

    private static long? ParseLastEventId(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) && sequence >= 0
            ? sequence
            : null;

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

    private static IResult InvalidLimit(int? limit, int maximum) =>
        ContractErrorResults.Validation(
            "limit",
            $"Limit must be between 1 and {maximum}.",
            "limit.out_of_range",
            limit?.ToString(CultureInfo.InvariantCulture));

    private static string ResolveProjectScope(HttpContext httpContext) =>
        AuthenticatedProject.ForHttpRead(httpContext);

    private static Task FallbackHandler(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Every /api response is problem+json, an unknown route included, so a client parses
        // one error shape instead of special-casing an empty 404.
        if (IsPathOrDescendant(path, "/api"))
            return ContractErrorResults.WriteNotFoundAsync(context.Response, "route", path, context.RequestAborted);

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    private static bool IsPathOrDescendant(string path, string root) =>
        path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);

    internal static class ContractLimits
    {
        // Contract 12.0.0 has one PageLimit for every list route and for SavedQueryRunRequest.limit:
        // 1 to 1000, 100 when omitted. Only series_limit on the metric query keeps its own range.
        public const int DefaultPageLimit = 100;
        public const int MaxPageLimit = 1_000;
        public const int MetricSeriesDefaultLimit = 50;
        public const int MetricSeriesMaxLimit = 500;
        public const long MetricDefaultStepMs = 60_000;
        public const long MetricMaxStepMs = 86_400_000;
        public const int MinimumLogSeverity = 0;
        public const int MinimumStreamSeverity = 1;
        public const int MaximumLogSeverity = 24;

        public static bool TryResolve(int? requested, int defaultValue, int maxValue, out int resolved)
        {
            resolved = requested ?? defaultValue;
            return resolved >= 1 && resolved <= maxValue;
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

internal static partial class OtlpTracesLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP traces payload")]
    public static partial void FailedToProcessPayload(ILogger logger, Exception exception);
}
