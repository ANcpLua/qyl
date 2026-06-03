using Qyl.Collector.Cost;
using Qyl.Collector.Dashboard;
using Qyl.Collector.Grpc;
using Qyl.Api.Contracts;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.OTel.Logs;

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
            List<SpanStorageRow> spans;
            var otlpData = await OtlpPayloadParser.ParseTraceRequestAsync(context.Request, ct);
            if (otlpData.ResourceSpans.Count is 0)
                return Results.Accepted();

            spans = OtlpConverter.ConvertTraceRequestToStorageRows(otlpData);

            if (spans.Count is 0) return Results.Accepted();

            var batch = pricingService.EnrichBatchWithCost(new SpanBatch(spans));
            await store.EnqueueAsync(batch, ct);

            return Results.Accepted();
        }
        catch (Exception)
        {
            return Results.BadRequest(new ErrorResponse { Error = "OTLP parse error" });
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
            var otlpData = await OtlpPayloadParser.ParseLogsRequestAsync(context.Request, ct);
            if (otlpData.ResourceLogs.Count is 0)
                return Results.Accepted();

            logs = OtlpConverter.ConvertLogsToStorageRows(otlpData);

            if (logs.Count is 0) return Results.Accepted();

            await store.InsertLogsAsync(logs, ct);
            return Results.Accepted();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpLogsEndpoint");
            OtlpLogsLog.FailedToProcessPayload(logger, ex);
            return Results.BadRequest(new ErrorResponse { Error = "OTLP logs parse error" });
        }
    }


    private static async Task<IResult> GetSessionsAsync(
        DuckDbStore store,
        int? limit,
        string? serviceName,
        CancellationToken ct)
    {
        var sessions = await store.GetSessionsAsync(limit ?? 100, 0, serviceName, ct: ct).ConfigureAwait(false);
        var response = SessionMapper.ToPage(sessions, sessions.Count, false);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetSessionByIdAsync(
        string sessionId,
        DuckDbStore store,
        CancellationToken ct) =>
        await store.GetSessionAsync(sessionId, ct).ConfigureAwait(false) is not { } session
            ? Results.NotFound()
            : Results.Ok(SessionMapper.ToContract(session));


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

    private static ServerSentEventsResult<SseItem<LogRecord>> StreamLogsAsync(
        DuckDbStore store,
        string? serviceName,
        int? minSeverity,
        string? query,
        CancellationToken ct) =>
        TypedResults.ServerSentEvents(
            StreamLogEventsAsync(store, serviceName, minSeverity, query, ct), null);

    private static async IAsyncEnumerable<SseItem<LogRecord>> StreamLogEventsAsync(
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
                    yield return new SseItem<LogRecord>(LogMapper.ToContract(log), "log");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
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

            var results = OtlpConverter.ConvertProfilesToNormalizedRows(otlpData);

            if (results.Count is 0) return Results.Accepted();

            await store.InsertProfilesAsync(results, ct);
            return Results.Accepted();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OtlpProfilesEndpoint");
            OtlpProfilesLog.FailedToProcessPayload(logger, ex);
            return Results.BadRequest(new ErrorResponse { Error = "OTLP profiles parse error" });
        }
    }

    private static async Task<IResult> GetProfilesAsync(
        DuckDbStore store,
        string? session,
        string? trace,
        string? service,
        string? sampleType,
        int? limit,
        CancellationToken ct)
    {
        var profiles = await store.GetProfilesAsync(
            session,
            trace,
            serviceName: service,
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
        var detail = await store.GetProfileDetailAsync(profileId, ct);
        return detail is not null ? Results.Ok(ProfileMapper.ToContract(detail)) : Results.NotFound();
    }

    private static async Task<IResult> GetTraceProfilesAsync(
        string traceId,
        DuckDbStore store,
        CancellationToken ct)
    {
        var profiles = await store.GetProfilesAsync(traceId: traceId, ct: ct);
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
    public static partial void FailedToProcessPayload(ILogger logger, Exception ex);
}

internal static partial class OtlpProfilesLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP profiles payload")]
    public static partial void FailedToProcessPayload(ILogger logger, Exception ex);
}
