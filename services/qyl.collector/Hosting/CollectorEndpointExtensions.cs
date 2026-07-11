using Qyl.Collector;
using Qyl.Collector.Cost;
using Qyl.Collector.Dashboard;
using Qyl.Collector.Grpc;
using Qyl.Api.Contracts;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Api.Contracts.OTel.Logs;
using Qyl.Api.Contracts.Streaming;

namespace Qyl.Collector.Hosting;

internal static class CollectorEndpointExtensions
{
    public static WebApplication MapQylCollectorEndpoints(this WebApplication app)
    {
        app.MapGrpcService<TraceServiceImpl>();
        app.MapGrpcService<LogsServiceImpl>();
        app.MapGrpcService<ProfilesServiceImpl>();

        var otlp = app.MapGroup("/v1");
        otlp.MapPost("/traces", IngestOtlpTracesAsync);
        otlp.MapPost("/logs", IngestOtlpLogsAsync);
        otlp.MapPost("/profiles", IngestOtlpProfilesAsync);

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

        api.MapGet("/profiles", GetProfilesAsync);
        api.MapGet("/profiles/{profileId}", GetProfileByIdAsync);
        api.MapGet("/profiles/by-trace/{traceId}", GetTraceProfilesAsync);
        api.MapGet("/profiles/by-span/{spanId}", GetSpanProfilesAsync);

        app.MapFallback(FallbackHandler);

        return app;
    }


    private static async Task<IResult> IngestOtlpTracesAsync(
        HttpContext context,
        IQylStore store,
        ModelPricingService pricingService,
        CancellationToken ct)
    {
        try
        {
            var otlpData = await OtlpPayloadParser.ParseTraceRequestAsync(context.Request, ct);
            if (otlpData.ResourceSpans.Count is 0)
                return Results.Accepted();

            var traceBatch = OtlpConverter.ConvertTraceRequest(otlpData);
            var spans = IngestionStorageMapper.ToSpanStorageRows(traceBatch);

            if (spans.Count is 0) return Results.Accepted();

            var batch = pricingService.EnrichBatchWithCost(new SpanBatch(spans));
            await store.EnqueueAsync(batch, ct);

            return Results.Accepted();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpTracesEndpoint");
            OtlpTracesLog.FailedToProcessPayload(logger, ex);
            return Results.BadRequest();
        }
    }

    private static async Task<IResult> IngestOtlpLogsAsync(
        HttpContext context,
        IQylStore store,
        CancellationToken ct)
    {
        try
        {
            var otlpData = await OtlpPayloadParser.ParseLogsRequestAsync(context.Request, ct);
            if (otlpData.ResourceLogs.Count is 0)
                return Results.Accepted();

            var logBatch = OtlpConverter.ConvertLogs(otlpData);
            var logs = IngestionStorageMapper.ToLogStorageRows(logBatch);

            if (logs.Count is 0) return Results.Accepted();

            await store.InsertLogsAsync(logs, ct);
            return Results.Accepted();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpLogsEndpoint");
            OtlpLogsLog.FailedToProcessPayload(logger, ex);
            return Results.BadRequest();
        }
    }


    private static async Task<IResult> GetSessionsAsync(
        HttpContext httpContext,
        IQylStore store,
        bool? isActive,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int? limit,
        string? cursor,
        CancellationToken ct)
    {
        if (!TryReadOffsetCursor(cursor, out var offset))
        {
            return Results.BadRequest(ContractErrorFactory.Validation(
                "cursor",
                "Cursor must be a non-negative integer offset.",
                "cursor.invalid",
                cursor));
        }

        var boundedLimit = ContractLimits.Clamp(limit, ContractLimits.DefaultPageLimit, ContractLimits.SessionMaxLimit);
        var sessions = await store.GetSessionsAsync(
            ResolveProjectScope(httpContext),
            boundedLimit + 1,
            offset,
            isActive,
            startTime,
            endTime,
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
            ? Results.NotFound(ContractErrorFactory.NotFound("session", sessionId))
            : Results.Ok(SessionMapper.ToContract(session));

    private static async Task<IResult> GetSessionStatsAsync(
        HttpContext httpContext,
        IQylStore store,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        CancellationToken ct)
    {
        var stats = await store.GetSessionStatsAsync(ResolveProjectScope(httpContext), startTime, endTime, ct: ct)
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
            return Results.NotFound(ContractErrorFactory.NotFound("session", sessionId));

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

    private static async Task<IResult> GetTracesAsync(
        HttpContext httpContext,
        IQylStore store,
        int? limit,
        CancellationToken ct)
    {
        var boundedLimit = ContractLimits.Clamp(limit, ContractLimits.DefaultPageLimit, ContractLimits.TraceMaxLimit);
        var spans = await store.GetSpansAsync(ResolveProjectScope(httpContext), limit: boundedLimit, ct: ct)
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

    private static async Task<IResult> GetTraceSpansAsync(
        HttpContext httpContext,
        string traceId,
        IQylStore store,
        CancellationToken ct)
    {
        var spans = await store.GetTraceAsync(traceId, ResolveProjectScope(httpContext), ct: ct).ConfigureAwait(false);
        if (spans.Count is 0) return Results.NotFound(ContractErrorFactory.NotFound("trace", traceId));

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
        if (spans.Count is 0) return Results.NotFound(ContractErrorFactory.NotFound("trace", traceId));

        var spanContracts = SpanMapper.ToContracts(spans);

        return Results.Ok(SpanMapper.ToTrace(traceId, spanContracts));
    }

    private static async Task<IResult> GetLogsAsync(
        HttpContext httpContext,
        IQylStore store,
        string? sessionId,
        string? traceId,
        string? serviceName,
        string? level,
        string? query,
        int? severityMin,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int? limit,
        CancellationToken ct)
    {
        var boundedLimit = ContractLimits.Clamp(limit, ContractLimits.DefaultPageLimit, ContractLimits.LogMaxLimit);
        var logs = await store.GetLogsAsync(
            ResolveProjectScope(httpContext),
            sessionId: sessionId,
            traceId: traceId,
            severityText: level,
            minSeverity: severityMin,
            search: query,
            start: startTime.HasValue ? QylTimeConversions.ToUnixNanoUnsigned(startTime.Value.ToUniversalTime()) : null,
            before: endTime.HasValue ? QylTimeConversions.ToUnixNanoUnsigned(endTime.Value.ToUniversalTime()) : null,
            serviceName: serviceName,
            limit: boundedLimit,
            ct: ct);

        return Results.Ok(new CursorPageLogRecord { Items = LogMapper.ToContracts(logs), HasMore = logs.Count >= boundedLimit });
    }

    private static async Task StreamLogsAsync(
        HttpContext context,
        IQylStore store,
        string? serviceName,
        int? minSeverity,
        string? query,
        CancellationToken ct)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers.Pragma = "no-cache";

        await foreach (var streamEvent in StreamLogEventsAsync(store, ResolveProjectScope(context), serviceName, minSeverity, query, ct)
                           .ConfigureAwait(false))
        {
            if (streamEvent.Log is { } log)
            {
                await WriteSseEventAsync(
                    context.Response,
                    streamEvent.EventType,
                    log,
                    QylSerializerContext.Default.LogStreamEvent,
                    ct).ConfigureAwait(false);
            }
            else if (streamEvent.Heartbeat is { } heartbeat)
            {
                await WriteSseEventAsync(
                    context.Response,
                    streamEvent.EventType,
                    heartbeat,
                    QylSerializerContext.Default.HeartbeatEvent,
                    ct).ConfigureAwait(false);
            }
        }
    }

    private static async IAsyncEnumerable<(string EventType, LogStreamEvent? Log, HeartbeatEvent? Heartbeat)>
        StreamLogEventsAsync(
        IQylStore store,
        string projectId,
        string? serviceName,
        int? minSeverity,
        string? query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var hasCursor = false;
        ulong? afterTimeUnixNano = null;
        string? afterLogId = null;
        while (!ct.IsCancellationRequested)
        {
            var rows = await store.GetLogsAsync(
                projectId,
                sessionId: null,
                traceId: null,
                severityText: null,
                minSeverity,
                search: query,
                serviceName: serviceName,
                after: afterTimeUnixNano,
                afterLogId: afterLogId,
                ascending: hasCursor,
                latestPageAscending: !hasCursor,
                limit: 250,
                ct: ct).ConfigureAwait(false);

            if (rows.Count > 0)
            {
                LogStorageRow? last = null;
                foreach (var log in rows)
                {
                    last = log;
                    yield return ("log", new LogStreamEvent
                        {
                            Type = "log",
                            Data = LogMapper.ToContract(log),
                            Timestamp = QylTimeConversions.NanosToDateTimeOffset(log.TimeUnixNano)
                        },
                        null);
                }

                hasCursor = true;
                afterTimeUnixNano = last!.TimeUnixNano;
                afterLogId = last.LogId;
            }
            else
            {
                yield return ("heartbeat", null, new HeartbeatEvent
                {
                    Type = "heartbeat",
                    Timestamp = TimeProvider.System.GetUtcNow()
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
    }

    private static async Task WriteSseEventAsync<T>(
        HttpResponse response,
        string eventType,
        T streamEvent,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken ct)
    {
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
        try
        {
            var otlpData = await OtlpPayloadParser.ParseProfilesRequestAsync(context.Request, ct);
            if (otlpData.ResourceProfiles.Count is 0)
                return Results.Accepted();

            var profileBatch = OtlpConverter.ConvertProfiles(otlpData);
            var results = IngestionStorageMapper.ToProfileStorageRows(profileBatch);

            if (results.Count is 0) return Results.Accepted();

            await store.InsertProfilesAsync(results, ct);
            return Results.Accepted();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpProfilesEndpoint");
            OtlpProfilesLog.FailedToProcessPayload(logger, ex);
            return Results.BadRequest();
        }
    }

    private static async Task<IResult> GetProfilesAsync(
        HttpContext httpContext,
        IQylStore store,
        string? sessionId,
        string? traceId,
        string? serviceName,
        string? sampleType,
        int? limit,
        CancellationToken ct)
    {
        var boundedLimit = ContractLimits.Clamp(limit, ContractLimits.DefaultPageLimit, ContractLimits.ProfileMaxLimit);
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
            : Results.NotFound(ContractErrorFactory.NotFound("profile", profileId));
    }

    private static async Task<IResult> GetTraceProfilesAsync(
        HttpContext httpContext,
        string traceId,
        IQylStore store,
        CancellationToken ct)
    {
        var profiles = await store.GetProfilesAsync(ResolveProjectScope(httpContext), traceId: traceId, ct: ct);
        return Results.Ok(ProfileMapper.ToContracts(profiles));
    }

    private static async Task<IResult> GetSpanProfilesAsync(
        HttpContext httpContext,
        string spanId,
        IQylStore store,
        int? limit,
        CancellationToken ct)
    {
        var boundedLimit = ContractLimits.Clamp(limit, ContractLimits.DefaultPageLimit, ContractLimits.ProfileMaxLimit);
        var profiles = await store.GetProfilesAsync(ResolveProjectScope(httpContext), spanId: spanId, limit: boundedLimit, ct: ct);
        return Results.Ok(ProfileMapper.ToContracts(profiles));
    }


    // Phase-5 decision: the read API resolves its project scope from the X-Qyl-Project header,
    // absent meaning "default" (ingest already persists real project ids from qyl.project.id /
    // qyl.workspace.id resource attributes). Header-based selection is wire-compatible with the
    // published contract; a formal contract axis goes through qyl-api-schema when multi-project
    // goes public.
    private static string ResolveProjectScope(HttpContext httpContext) =>
        ProjectScope.Normalize(httpContext.Request.Headers["X-Qyl-Project"].FirstOrDefault());

    private static Task FallbackHandler(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
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

    private static class ContractLimits
    {
        public const int DefaultPageLimit = 100;
        public const int SessionMaxLimit = 1_000;
        public const int TraceMaxLimit = 1_000;
        public const int LogMaxLimit = 10_000;
        public const int ProfileMaxLimit = 1_000;

        public static int Clamp(int? requested, int defaultValue, int maxValue) =>
            Math.Clamp(requested ?? defaultValue, 1, maxValue);
    }
}

internal static partial class OtlpLogsLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP logs payload")]
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
