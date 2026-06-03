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
        api.MapGet("/sessions/{sessionId}/traces", SpanEndpoints.GetSessionTracesAsync);

        api.MapGet("/traces", GetTracesAsync);
        api.MapGet("/traces/{traceId}", SpanEndpoints.GetTraceAsync);
        api.MapGet("/traces/{traceId}/spans", SpanEndpoints.GetTraceSpansAsync);

        api.MapGet("/logs", GetLogsAsync);
        api.MapGet("/stream/logs", StreamLogsAsync);

        api.MapGet("/profiles", GetProfilesAsync);
        api.MapGet("/profiles/{profileId}", GetProfileByIdAsync);
        api.MapGet("/profiles/by-trace/{traceId}", GetTraceProfilesAsync);
        api.MapGet("/profiles/by-span/{spanId}", GetSpanProfilesAsync);

        app.MapGet("/qyl.js", static (IWebHostEnvironment env) =>
            Results.File(Path.Combine(env.WebRootPath, "qyl.js"), "application/javascript"));

        app.MapFallback(FallbackHandler);

        return app;
    }


    private static async Task<IResult> IngestOtlpTracesAsync(
        HttpContext context,
        DuckDbStore store,
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
        catch (Exception)
        {
            return Results.BadRequest();
        }
    }

    private static async Task<IResult> IngestOtlpLogsAsync(
        HttpContext context,
        DuckDbStore store,
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
        DuckDbStore store,
        string? userId,
        bool? isActive,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int? limit,
        string? cursor,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return Results.BadRequest(ContractErrorFactory.Validation(
                "userId",
                "Session userId filtering requires a generated user identity storage projection.",
                "session.user_filter_not_projected",
                userId));
        }

        if (!TryReadOffsetCursor(cursor, out var offset))
        {
            return Results.BadRequest(ContractErrorFactory.Validation(
                "cursor",
                "Cursor must be a non-negative integer offset.",
                "cursor.invalid",
                cursor));
        }

        var boundedLimit = Math.Clamp(limit ?? 100, 1, 1_000);
        var sessions = await store.GetSessionsAsync(
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
        string sessionId,
        DuckDbStore store,
        CancellationToken ct) =>
        await store.GetSessionAsync(sessionId, ct: ct).ConfigureAwait(false) is not { } session
            ? Results.NotFound(ContractErrorFactory.NotFound("session", sessionId))
            : Results.Ok(SessionMapper.ToContract(session));

    private static async Task<IResult> GetSessionStatsAsync(
        DuckDbStore store,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        CancellationToken ct)
    {
        var stats = await store.GetSessionStatsAsync(startTime, endTime, ct: ct).ConfigureAwait(false);
        return Results.Ok(new SessionStats
        {
            ActiveSessions = stats.ActiveSessions,
            TotalSessions = stats.TotalSessions,
            UniqueUsers = 0,
            AvgDurationMs = stats.AvgDurationMs,
            SessionsWithErrors = stats.SessionsWithErrors,
            SessionsWithGenAi = stats.SessionsWithGenAi,
            BounceRate = stats.BounceRate,
            ByDeviceType = [],
            ByCountry = []
        });
    }


    private static async Task<IResult> GetTracesAsync(
        DuckDbStore store,
        int? limit,
        CancellationToken ct)
    {
        var boundedLimit = Math.Clamp(limit ?? 100, 1, 500);
        var spans = await store.GetSpansAsync(limit: boundedLimit, ct: ct).ConfigureAwait(false);
        var traces = spans
            .GroupBy(static span => span.TraceId, StringComparer.Ordinal)
            .Select(static group =>
            {
                var spanContracts = SpanMapper.ToContracts(group, static r => (r.ServiceName ?? "unknown", null));
                return SpanMapper.ToTrace(group.Key, spanContracts);
            })
            .ToList();

        return Results.Ok(new CursorPageTrace { Items = traces, HasMore = false });
    }


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
        var boundedLimit = Math.Clamp(limit ?? 500, 1, 1_000);
        var logs = await store.GetLogsAsync(
            session, trace, level, minSeverity, search,
            serviceName: serviceName,
            limit: boundedLimit,
            ct: ct);

        return Results.Ok(new CursorPageLogRecord { Items = LogMapper.ToContracts(logs), HasMore = logs.Count >= boundedLimit });
    }

    private static async Task StreamLogsAsync(
        HttpContext context,
        DuckDbStore store,
        string? serviceName,
        int? minSeverity,
        string? query,
        CancellationToken ct)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers.Pragma = "no-cache";

        await foreach (var streamEvent in StreamLogEventsAsync(store, serviceName, minSeverity, query, ct)
                           .ConfigureAwait(false))
        {
            await WriteSseEventAsync(context.Response, "log", streamEvent, ct).ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<LogStreamEvent> StreamLogEventsAsync(
        DuckDbStore store,
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
                sessionId: null,
                traceId: null,
                severityText: null,
                minSeverity,
                search: query,
                serviceName: serviceName,
                after: afterTimeUnixNano,
                afterLogId: afterLogId,
                ascending: hasCursor,
                limit: 250,
                ct: ct).ConfigureAwait(false);

            if (rows.Count > 0)
            {
                var ordered = rows
                    .OrderBy(static l => l.TimeUnixNano)
                    .ThenBy(static l => l.LogId, StringComparer.Ordinal)
                    .ToArray();
                var last = ordered[^1];
                hasCursor = true;
                afterTimeUnixNano = last.TimeUnixNano;
                afterLogId = last.LogId;

                foreach (var log in ordered)
                    yield return new LogStreamEvent
                    {
                        Type = "log",
                        Data = LogMapper.ToContract(log),
                        Timestamp = QylTimeConversions.NanosToDateTimeOffset(log.TimeUnixNano)
                    };
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
    }

    private static async Task WriteSseEventAsync(
        HttpResponse response,
        string eventType,
        LogStreamEvent streamEvent,
        CancellationToken ct)
    {
        await response.WriteAsync("event: ", ct).ConfigureAwait(false);
        await response.WriteAsync(eventType, ct).ConfigureAwait(false);
        await response.WriteAsync("\ndata: ", ct).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(
            response.Body,
            streamEvent,
            QylSerializerContext.Default.LogStreamEvent,
            ct).ConfigureAwait(false);
        await response.WriteAsync("\n\n", ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }


    private static async Task<IResult> IngestOtlpProfilesAsync(
        HttpContext context,
        DuckDbStore store,
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
        DuckDbStore store,
        string? session,
        string? trace,
        string? serviceName,
        string? sampleType,
        int? limit,
        CancellationToken ct)
    {
        var profiles = await store.GetProfilesAsync(
            session,
            trace,
            serviceName: serviceName,
            sampleType: sampleType,
            limit: limit ?? 100,
            ct: ct);

        return Results.Ok(ProfileMapper.ToContracts(profiles));
    }

    private static async Task<IResult> GetProfileByIdAsync(
        string profileId,
        DuckDbStore store,
        CancellationToken ct)
    {
        var detail = await store.GetProfileDetailAsync(profileId, ct: ct);
        return detail is not null
            ? Results.Ok(ProfileMapper.ToContract(detail))
            : Results.NotFound(ContractErrorFactory.NotFound("profile", profileId));
    }

    private static async Task<IResult> GetTraceProfilesAsync(
        string traceId,
        DuckDbStore store,
        CancellationToken ct)
    {
        var profiles = await store.GetProfilesAsync(traceId: traceId, ct: ct);
        return Results.Ok(ProfileMapper.ToContracts(profiles));
    }

    private static async Task<IResult> GetSpanProfilesAsync(
        string spanId,
        DuckDbStore store,
        int? limit,
        CancellationToken ct)
    {
        var profiles = await store.GetProfilesAsync(spanId: spanId, limit: limit ?? 100, ct: ct);
        return Results.Ok(ProfileMapper.ToContracts(profiles));
    }


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
    public static partial void FailedToProcessPayload(ILogger logger, Exception exception);
}

internal static partial class OtlpProfilesLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP profiles payload")]
    public static partial void FailedToProcessPayload(ILogger logger, Exception exception);
}
