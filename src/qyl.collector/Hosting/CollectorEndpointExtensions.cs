using Qyl.Collector.AgentRuns;
using Qyl.Collector.Analytics;
using Qyl.Collector.Artifacts;
using Qyl.Collector.Autofix;
using Qyl.Collector.Cost;
using Qyl.Collector.Dashboard;
using Qyl.Collector.Dashboards;
using Qyl.Collector.Grpc;
using Qyl.Collector.Health;
using Qyl.Collector.Identity;
using Qyl.Collector.Insights;
using Qyl.Collector.Intelligence;
using Qyl.Collector.Meta;
using Qyl.Collector.Observe;
using Qyl.Collector.Provisioning;
using Qyl.Collector.SchemaControl;
using Qyl.Collector.Search;
using Qyl.Collector.Services;

namespace Qyl.Collector.Hosting;

public static class CollectorEndpointExtensions
{
    public static WebApplication MapQylCollectorEndpoints(this WebApplication app)
    {
        // gRPC OTLP ingestion on port 4317
        app.MapGrpcService<TraceServiceImpl>();

        // OTLP ingestion route group (/v1)
        var otlp = app.MapGroup("/v1");
        otlp.MapPost("/traces", IngestOtlpTracesAsync);
        otlp.MapPost("/logs", IngestOtlpLogsAsync);

        // REST API route group (/api/v1)
        var api = app.MapGroup("/api/v1");

        // Sessions
        api.MapGet("/sessions", GetSessionsAsync);
        api.MapGet("/sessions/{sessionId}", GetSessionByIdAsync);
        api.MapGet("/sessions/{sessionId}/spans", SpanEndpoints.GetSessionSpansAsync);

        // Traces
        api.MapGet("/traces", GetTracesAsync);
        api.MapGet("/traces/{traceId}", SpanEndpoints.GetTraceAsync);

        // Native ingest
        api.MapPost("/ingest", IngestNativeAsync);

        // Logs
        api.MapGet("/logs", GetLogsAsync);
        api.MapGet("/logs/live", StreamLogsLiveAsync);

        // GenAI
        api.MapGet("/genai/stats", GetGenAiStatsAsync);
        api.MapGet("/genai/spans", GetGenAiSpansAsync);

        // Telemetry management
        api.MapDelete("/telemetry", ClearTelemetryAsync);
        api.MapGet("/telemetry/stats", GetTelemetryStatsAsync);

        // Meta
        api.MapGet("/meta", GetMeta);

        // Delegated endpoint groups
        app.MapServiceEndpoints();
        app.MapSseEndpoints();
        app.MapSpanMemoryEndpoints();
        app.MapInsightsEndpoints();
        app.MapDashboardEndpoints();
        app.MapAnalyticsEndpoints();
        app.MapObserveEndpoints();
        app.MapQylHealthChecks();
        app.MapSchemaEndpoints();
        app.MapSearchEndpoints();
        app.MapSearchDocumentEndpoints();
        app.MapErrorEndpoints();
        app.MapIdentityEndpoints();
        app.MapGitHubEndpoints();
        app.MapProvisioningEndpoints();
        app.MapIssueEndpoints();
        app.MapIssueAnalyticsEndpoints();
        app.MapAnomalyEndpoints();
        app.MapAutofixEndpoints();
        app.MapRegressionEndpoints();
        app.MapAgentHandoffEndpoints();
        app.MapCodeReviewEndpoints();
        app.MapGitHubWebhookEndpoints();
        app.MapLoomEndpoints();
        app.MapLoomSettingsEndpoints();
        app.MapCodingAgentEndpoints();
        app.MapLoomWorkerEndpoints();
        app.MapTriageEndpoints();
        app.MapAgentRunEndpoints();
        app.MapAgentInsightsEndpoints();
        app.MapArtifactEndpoints();
        app.MapCostEndpoints();
        app.MapIntelligenceEndpoints();
        app.MapQueryEndpoints();
        app.MapLogSummaryEndpoints();

        // Browser SDK
        app.MapGet("/qyl.js", static (IWebHostEnvironment env) =>
            Results.File(Path.Combine(env.WebRootPath, "qyl.js"), "application/javascript"));

        // SPA fallback
        app.MapFallback(FallbackHandler);

        return app;
    }

    // =========================================================================
    // OTLP Ingestion
    // =========================================================================

    private static async Task<IResult> IngestOtlpTracesAsync(
        HttpContext context,
        DuckDbStore store,
        ITelemetrySseBroadcaster broadcaster,
        SpanRingBuffer ringBuffer,
        ModelPricingService pricingService,
        CancellationToken ct)
    {
        try
        {
            List<SpanStorageRow> spans;
            var contentType = context.Request.ContentType;

            List<ServiceInstanceRecord> serviceInstances;
            if (OtlpProtobufParser.IsProtobufContentType(contentType))
            {
                var protoRequest = await OtlpProtobufParser.ParseFromRequestAsync(context.Request, ct);
                if (protoRequest.ResourceSpans.Count is 0)
                    return Results.Accepted();

                spans = OtlpConverter.ConvertProtoToStorageRows(protoRequest);
                serviceInstances = OtlpConverter.ExtractServiceInstancesFromProto(protoRequest);
            }
            else
            {
                var otlpData = await context.Request.ReadFromJsonAsync<OtlpExportTraceServiceRequest>(
                    QylSerializerContext.Default.OtlpExportTraceServiceRequest, ct);

                if (otlpData?.ResourceSpans is null)
                    return Results.BadRequest(new ErrorResponse("Invalid OTLP format"));

                spans = OtlpConverter.ConvertJsonToStorageRows(otlpData);
                serviceInstances = OtlpConverter.ExtractServiceInstancesFromJson(otlpData);
            }

            if (spans.Count is 0) return Results.Accepted();

            var batch = pricingService.EnrichBatchWithCost(
                new SpanBatch(spans).WithCodexTransformations());
            ringBuffer.PushRange(batch.Spans.Select(SpanMapper.ToRecord));
            await store.EnqueueAsync(batch, ct);

            foreach (var si in serviceInstances)
                await store.UpsertServiceInstanceAsync(si, ct);

            broadcaster.PublishSpans(batch);
            return Results.Accepted();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new ErrorResponse("OTLP parse error", ex.Message));
        }
    }

    private static async Task<IResult> IngestOtlpLogsAsync(
        HttpContext context,
        DuckDbStore store,
        CancellationToken ct)
    {
        try
        {
            List<LogStorageRow> logs;
            var contentType = context.Request.ContentType;

            if (OtlpProtobufParser.IsProtobufContentType(contentType))
            {
                var protoRequest = await OtlpLogProtobufParser.ParseFromRequestAsync(context.Request, ct);
                if (protoRequest.ResourceLogs.Count is 0)
                    return Results.Accepted();

                logs = OtlpConverter.ConvertProtoLogsToStorageRows(protoRequest);
            }
            else
            {
                var otlpData = await context.Request.ReadFromJsonAsync<OtlpExportLogsServiceRequest>(ct);

                if (otlpData?.ResourceLogs is null)
                    return Results.BadRequest(new ErrorResponse("Invalid OTLP logs format"));

                logs = OtlpConverter.ConvertLogsToStorageRows(otlpData);
            }

            if (logs.Count is 0) return Results.Accepted();

            await store.InsertLogsAsync(logs, ct);
            return Results.Accepted();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpLogsEndpoint");
            OtlpLogsLog.FailedToProcessPayload(logger, ex);
            return Results.BadRequest(new ErrorResponse("OTLP logs parse error", ex.Message));
        }
    }

    // =========================================================================
    // Sessions
    // =========================================================================

    private static async Task<IResult> GetSessionsAsync(
        SessionQueryService queryService,
        int? limit,
        string? serviceName,
        CancellationToken ct)
    {
        var sessions = await queryService.GetSessionsAsync(limit ?? 100, 0, serviceName, ct: ct).ConfigureAwait(false);
        var response = SessionMapper.ToListResponse(sessions, sessions.Count, false);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetSessionByIdAsync(
        string sessionId,
        SessionQueryService queryService,
        CancellationToken ct) =>
        await queryService.GetSessionAsync(sessionId, ct).ConfigureAwait(false) is not { } session
            ? Results.NotFound()
            : Results.Ok(SessionMapper.ToDto(session));

    // =========================================================================
    // Traces
    // =========================================================================

    private static async Task<IResult> GetTracesAsync(
        DuckDbStore store,
        int? limit,
        CancellationToken ct)
    {
        var boundedLimit = Math.Clamp(limit ?? 100, 1, 500);
        var spans = await store.GetSpansAsync(limit: boundedLimit, ct: ct).ConfigureAwait(false);
        return Results.Ok(new { items = spans, total = spans.Count });
    }

    // =========================================================================
    // Native Ingest
    // =========================================================================

    private static async Task<IResult> IngestNativeAsync(
        HttpContext context,
        DuckDbStore store,
        ITelemetrySseBroadcaster broadcaster,
        SpanRingBuffer ringBuffer,
        CancellationToken ct)
    {
        SpanBatch? batch;
        try
        {
            batch = await context.Request.ReadFromJsonAsync<SpanBatch>(
                QylSerializerContext.Default.SpanBatch, ct);

            if (batch is null || batch.Spans.Count is 0)
                return Results.BadRequest(new ErrorResponse("Empty or invalid batch"));
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new ErrorResponse("Invalid JSON", ex.Message));
        }

        ringBuffer.PushRange(batch.Spans.Select(SpanMapper.ToRecord));
        await store.EnqueueAsync(batch, ct);
        broadcaster.PublishSpans(batch);
        return Results.Accepted();
    }

    // =========================================================================
    // Logs
    // =========================================================================

    private static async Task<IResult> GetLogsAsync(
        DuckDbStore store,
        string? session,
        string? trace,
        string? serviceName,
        string? level,
        string? search,
        int? minSeverity,
        int? limit,
        CancellationToken ct)
    {
        var logs = await store.GetLogsAsync(
            session, trace, level, minSeverity, search,
            serviceName: serviceName,
            limit: limit ?? 500,
            ct: ct);

        return Results.Ok(new { logs, total = logs.Count, has_more = logs.Count >= (limit ?? 500) });
    }

    private static ServerSentEventsResult<SseItem<object?>> StreamLogsLiveAsync(
        DuckDbStore store,
        string? session,
        string? trace,
        bool? dedupe,
        int? dedupeWindowSeconds,
        CancellationToken ct) =>
        TypedResults.ServerSentEvents(
            StreamLogEventsAsync(store, session, trace, dedupe, dedupeWindowSeconds, ct), null);

    private static async IAsyncEnumerable<SseItem<object?>> StreamLogEventsAsync(
        DuckDbStore store,
        string? session,
        string? trace,
        bool? dedupe,
        int? dedupeWindowSeconds,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var dedupeEnabled = dedupe.GetValueOrDefault(true);
        var dedupeWindow = TimeSpan.FromSeconds(Math.Clamp(dedupeWindowSeconds ?? 5, 1, 60));
        var deduplicator = dedupeEnabled ? new LiveLogDeduplicator(dedupeWindow) : null;

        yield return new SseItem<object?>(new { status = "ok" }, "connected");

        ulong? after = null;
        while (!ct.IsCancellationRequested)
        {
            var rows = await store.GetLogsAsync(
                session, trace, after: after, limit: 250, ct: ct).ConfigureAwait(false);

            var dedupedPayload = new List<DeduplicatedLiveLog>(rows.Count + 4);

            if (rows.Count > 0)
            {
                var ordered = rows.OrderBy(static l => l.TimeUnixNano).ToArray();
                after = ordered[^1].TimeUnixNano;

                if (deduplicator is null)
                    dedupedPayload.AddRange(ordered.Select(static log => new DeduplicatedLiveLog(log)));
                else
                    dedupedPayload.AddRange(deduplicator.ProcessBatch(ordered));
            }

            if (deduplicator is not null)
                dedupedPayload.AddRange(deduplicator.FlushExpired(TimeProvider.System.GetUtcNow().UtcDateTime));

            if (dedupedPayload.Count > 0)
            {
                var payload = dedupedPayload.Select(LiveLogProjection.ToDto).ToArray();
                yield return new SseItem<object?>(new { logs = payload }, "logs");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
    }

    // =========================================================================
    // GenAI
    // =========================================================================

    private static async Task<IResult> GetGenAiStatsAsync(
        SessionQueryService queryService,
        int? hours,
        string? session_id,
        CancellationToken ct)
    {
        DateTime? after = null;
        if (hours is > 0)
            after = TimeProvider.System.GetUtcNow().UtcDateTime.AddHours(-hours.Value);

        var stats = await queryService.GetGenAiStatsAsync(session_id, after, ct).ConfigureAwait(false);
        return Results.Ok(new
        {
            requestCount = stats.RequestCount,
            totalInputTokens = stats.InputTokens,
            totalOutputTokens = stats.OutputTokens,
            totalCostUsd = stats.TotalCostUsd,
            averageEvalScore = (double?)null
        });
    }

    private static async Task<IResult> GetGenAiSpansAsync(
        SessionQueryService queryService,
        string? session_id,
        int? limit,
        CancellationToken ct)
    {
        var boundedLimit = Math.Clamp(limit ?? 100, 1, 500);
        var spans = await queryService.GetGenAiSpansAsync(session_id, boundedLimit, ct).ConfigureAwait(false);

        var items = spans.Select(static span => new
        {
            spanId = span.SpanId,
            traceId = span.TraceId,
            name = span.Name,
            kind = span.Kind,
            startTimeUnixNano = span.StartTimeUnixNano,
            endTimeUnixNano = span.EndTimeUnixNano,
            durationNs = span.DurationNs,
            statusCode = span.StatusCode,
            statusMessage = span.StatusMessage,
            serviceName = span.ServiceName,
            genAiProviderName = span.GenAiProviderName,
            genAiRequestModel = span.GenAiRequestModel,
            genAiResponseModel = span.GenAiResponseModel,
            genAiInputTokens = span.GenAiInputTokens,
            genAiOutputTokens = span.GenAiOutputTokens,
            genAiTemperature = span.GenAiTemperature,
            genAiStopReason = span.GenAiStopReason,
            genAiToolName = span.GenAiToolName,
            genAiToolCallId = span.GenAiToolCallId,
            genAiCostUsd = span.GenAiCostUsd,
            attributesJson = span.AttributesJson
        });

        return Results.Ok(new { spans = items, total = spans.Count });
    }

    // =========================================================================
    // Telemetry Management
    // =========================================================================

    private static async Task<IResult> ClearTelemetryAsync(
        DuckDbStore store,
        SpanRingBuffer ringBuffer,
        string? type,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(type))
        {
            var deleted = type.ToLowerInvariant() switch
            {
                "spans" or "traces" => await ClearSpansAsync(store, ringBuffer, ct).ConfigureAwait(false),
                "logs" => await store.ClearAllLogsAsync(ct).ConfigureAwait(false),
                "sessions" => await store.ClearAllSessionsAsync(ct).ConfigureAwait(false),
                _ => throw new ArgumentException($"Unknown telemetry type: {type}")
            };
            return TypedResults.Ok(new ClearTelemetryResponse(deleted, 0, 0, 0, type));
        }

        var result = await store.ClearAllTelemetryAsync(ct).ConfigureAwait(false);
        ringBuffer.Clear();

        return TypedResults.Ok(new ClearTelemetryResponse(
            result.SpansDeleted, result.LogsDeleted, result.SessionsDeleted, 0, "all"));

        static async Task<int> ClearSpansAsync(DuckDbStore store, SpanRingBuffer ringBuffer, CancellationToken ct)
        {
            var deleted = await store.ClearAllSpansAsync(ct).ConfigureAwait(false);
            ringBuffer.Clear();
            return deleted;
        }
    }

    private static async Task<IResult> GetTelemetryStatsAsync(DuckDbStore store, CancellationToken ct)
    {
        var stats = await store.GetStorageStatsAsync(ct).ConfigureAwait(false);
        return Results.Ok(stats);
    }

    // =========================================================================
    // Meta
    // =========================================================================

    private static IResult GetMeta(CollectorPortOptions ports, OtlpApiKeyOptions apiKeyOptions)
    {
        var version = BuildVersion.InformationalVersion;
        var hasEmbeddedDashboard = EmbeddedDashboardExtensions.HasEmbeddedDashboard();

        return Results.Ok(new MetaResponse
        {
            Version = version,
            Runtime = $"dotnet/{Environment.Version}",
            Build =
                new MetaBuild
                {
                    InformationalVersion = version,
                    Commit = version.Contains('+') ? version[(version.IndexOf('+') + 1)..] : null
                },
            Capabilities = new MetaCapabilities
            {
                Tracing = true,
                Grpc = true,
                GenAi = true,
                Copilot = false,
                EmbeddedDashboard = hasEmbeddedDashboard
            },
            Status = new MetaStatus
            {
                GrpcEnabled = ports.Grpc > 0, AuthMode = apiKeyOptions.IsApiKeyMode ? "api-key" : "unsecured"
            },
            Links = new MetaLinks
            {
                Dashboard = hasEmbeddedDashboard ? $"http://localhost:{ports.Http}" : null,
                OtlpHttp = ports.OtlpHttp > 0
                    ? $"http://localhost:{ports.OtlpHttp}/v1/traces"
                    : $"http://localhost:{ports.Http}/v1/traces",
                OtlpGrpc = ports.Grpc > 0 ? $"http://localhost:{ports.Grpc}" : null
            },
            Ports = new MetaPorts { Http = ports.Http, Grpc = ports.Grpc, OtlpHttp = ports.OtlpHttp }
        });
    }

    // =========================================================================
    // SPA Fallback
    // =========================================================================

    private static Task FallbackHandler(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        if (path.StartsWithIgnoreCase("/api/") ||
            path.StartsWithIgnoreCase("/v1/") ||
            path.StartsWithIgnoreCase("/assets/"))
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
                "Dashboard not found. Build with: nuke FrontendBuild && nuke DockerImageBuild");
        }

        var indexPath = Path.Combine(webRootPath, "index.html");
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html";
            return context.Response.SendFileAsync(indexPath);
        }

        context.Response.StatusCode = 404;
        return context.Response.WriteAsync(
            "Dashboard not found. Build with: nuke FrontendBuild && nuke DockerImageBuild");
    }
}

internal static partial class OtlpLogsLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP logs payload")]
    public static partial void FailedToProcessPayload(ILogger logger, Exception ex);
}
