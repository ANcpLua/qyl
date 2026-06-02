using Qyl.Collector.Cost;
using Qyl.Collector.Dashboard;
using Qyl.Collector.Grpc;
using Qyl.Api.Contracts;
using Qyl.Api.Contracts.Common.Pagination;

namespace Qyl.Collector.Hosting;

public static class CollectorEndpointExtensions
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
        api.MapGet("/sessions/{sessionId}/spans", SpanEndpoints.GetSessionSpansAsync);

        api.MapGet("/traces", GetTracesAsync);
        api.MapGet("/traces/{traceId}", SpanEndpoints.GetTraceAsync);

        api.MapGet("/logs", GetLogsAsync);
        api.MapGet("/logs/live", StreamLogsLiveAsync);

        api.MapGet("/profiles", GetProfilesAsync);
        api.MapGet("/profiles/{profileId}", GetProfileByIdAsync);
        api.MapGet("/traces/{traceId}/profiles", GetTraceProfilesAsync);

        api.MapGet("/genai/stats", GetGenAiStatsAsync);
        api.MapGet("/genai/spans", GetGenAiSpansAsync);

        api.MapDelete("/telemetry", ClearTelemetryAsync);
        api.MapGet("/telemetry/stats", GetTelemetryStatsAsync);

        api.MapGet("/meta", GetMeta);

        app.MapGet("/qyl.js", static (IWebHostEnvironment env) =>
            Results.File(Path.Combine(env.WebRootPath, "qyl.js"), "application/javascript"));

        app.MapFallback(FallbackHandler);

        return app;
    }


    private static async Task<IResult> IngestOtlpTracesAsync(
        HttpContext context,
        DuckDbStore store,
        SpanRingBuffer ringBuffer,
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
            var serviceInstances = OtlpConverter.ExtractServiceInstances(otlpData);

            if (spans.Count is 0) return Results.Accepted();

            var batch = pricingService.EnrichBatchWithCost(
                new SpanBatch(spans).WithCodexTransformations());
            ringBuffer.PushRange(batch.Spans);
            await store.EnqueueAsync(batch, ct);

            foreach (var si in serviceInstances)
                await store.UpsertServiceInstanceAsync(si, ct);

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
        SessionQueryService queryService,
        int? limit,
        string? serviceName,
        CancellationToken ct)
    {
        var sessions = await queryService.GetSessionsAsync(limit ?? 100, 0, serviceName, ct: ct).ConfigureAwait(false);
        var response = SessionMapper.ToPage(sessions, sessions.Count, false);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetSessionByIdAsync(
        string sessionId,
        SessionQueryService queryService,
        CancellationToken ct) =>
        await queryService.GetSessionAsync(sessionId, ct).ConfigureAwait(false) is not { } session
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
                var payload = dedupedPayload.Select(LogMapper.ToContract).ToArray();
                yield return new SseItem<object?>(new { logs = payload }, "logs");
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
        return Results.Ok(ContractStatsMapper.ToContract(stats));
    }

    private static async Task<IResult> GetGenAiSpansAsync(
        SessionQueryService queryService,
        string? session_id,
        int? limit,
        CancellationToken ct)
    {
        var boundedLimit = Math.Clamp(limit ?? 100, 1, 500);
        var spans = await queryService.GetGenAiSpansAsync(session_id, boundedLimit, ct).ConfigureAwait(false);
        var spanContracts = SpanMapper.ToContracts(spans, static span => (span.ServiceName ?? "unknown", null));

        return Results.Ok(new CursorPageSpan { Items = spanContracts, HasMore = false });
    }


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
                "profiles" => await store.ClearAllProfilesAsync(ct).ConfigureAwait(false),
                "sessions" => await store.ClearAllSessionsAsync(ct).ConfigureAwait(false),
                _ => throw new ArgumentException($"Unknown telemetry type: {type}")
            };
            return TypedResults.Ok(new ClearTelemetryResponse
            {
                SpansDeleted = deleted,
                LogsDeleted = 0,
                ProfilesDeleted = 0,
                SessionsDeleted = 0,
                ConsoleCleared = 0,
                Type = type
            });
        }

        var result = await store.ClearAllTelemetryAsync(ct).ConfigureAwait(false);
        ringBuffer.Clear();

        return TypedResults.Ok(new ClearTelemetryResponse
        {
            SpansDeleted = result.SpansDeleted,
            LogsDeleted = result.LogsDeleted,
            ProfilesDeleted = result.ProfilesDeleted,
            SessionsDeleted = result.SessionsDeleted,
            ConsoleCleared = 0,
            Type = "all"
        });

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
        return Results.Ok(ContractStatsMapper.ToContract(stats));
    }


    private static IResult GetMeta(
        CollectorPortOptions ports,
        OtlpApiKeyOptions apiKeyOptions,
        IWebHostEnvironment env)
    {
        const string version = BuildVersion.InformationalVersion;
        var hasEmbeddedDashboard = EmbeddedDashboardExtensions.HasEmbeddedDashboard();
        var dashboardBuild = DashboardBuildDescriptorReader.TryRead(env);

        return Results.Ok(new MetaResponse
        {
            Version = version,
            Runtime = $"dotnet/{Environment.Version}",
            Build =
                new MetaBuild
                {
                    InformationalVersion = version,
                    Commit = version.Contains('+') ? version[(version.IndexOf('+') + 1)..] : null,
                    DashboardBuildId = dashboardBuild?.BuildId,
                    DashboardEntryAsset = dashboardBuild?.EntryAsset,
                    DashboardBuiltAtUtc = dashboardBuild?.BuiltAtUtc
                },
            Capabilities = new MetaCapabilities
            {
                Tracing = true,
                Grpc = true,
                GenAi = true,
                Profiles = true,
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
