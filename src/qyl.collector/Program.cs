using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using qyl.collector;
using qyl.collector.AgentRuns;
using qyl.collector.Observe;
using qyl.collector.Analytics;
using qyl.collector.Auth;
using qyl.collector.Autofix;
using qyl.collector.BuildFailures;
using qyl.collector.CodingAgent;
using qyl.collector.ClaudeCode;
using Microsoft.Agents.AI;
using qyl.collector.Copilot;
using qyl.copilot.Agents;
using qyl.collector.Dashboard;
using qyl.collector.Dashboards;
using qyl.collector.Grpc;
using qyl.collector.Health;
using qyl.collector.Identity;
using qyl.collector.Insights;
using qyl.collector.Meta;
using qyl.collector.Provisioning;
using qyl.collector.SchemaControl;
using qyl.collector.Search;
using qyl.collector.Services;
using qyl.collector.Telemetry;
using qyl.collector.Workflow;
using qyl.copilot;
using qyl.copilot.Auth;
using qyl.copilot.Workflows;
using Qyl.ServiceDefaults.Instrumentation;
Console.WriteLine($"[qyl] Process starting at {TimeProvider.System.GetUtcNow():O}");

var builder = WebApplication.CreateSlimBuilder(args);

// Service defaults: OpenTelemetry (with GenAI sources), resilience, service discovery
builder.UseQyl(options =>
{
    // Collector has custom OpenAPI via stubs, not the standard AddOpenApi
    options.EnableOpenApi = false;
    // Collector has specialized DuckDB health checks
    options.AdditionalActivitySources.Add("qyl.collector");
});

// Request decompression for OTLP clients sending gzip/deflate compressed payloads
builder.Services.AddRequestDecompression();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default);
});

// QYL_PORT takes precedence, but fallback to PORT (Railway standard) for PaaS compatibility
var port = builder.Configuration.GetValue<int?>("QYL_PORT")
           ?? builder.Configuration.GetValue<int?>("PORT")
           ?? 5100;
var grpcPort = builder.Configuration.GetValue("QYL_GRPC_PORT", 4317);
var otlpHttpPort = builder.Configuration.GetValue("QYL_OTLP_PORT", 4318);

// Startup diagnostics for Railway debugging
Console.WriteLine(
    $"[qyl] Starting on port {port} (QYL_PORT={Environment.GetEnvironmentVariable("QYL_PORT")}, PORT={Environment.GetEnvironmentVariable("PORT")})");
Console.WriteLine($"[qyl] gRPC port: {grpcPort} (0=disabled)");
Console.WriteLine($"[qyl] OTLP HTTP port: {otlpHttpPort} (0=disabled, falls back to {port})");
var token = builder.Configuration["QYL_TOKEN"] ?? TokenGenerator.Generate();
var dataPath = builder.Configuration["QYL_DATA_PATH"] ?? "qyl.duckdb";

// Ensure parent directory exists for DuckDB file
var dataDir = Path.GetDirectoryName(dataPath);
if (!string.IsNullOrEmpty(dataDir))
    Directory.CreateDirectory(dataDir);

// Configure Kestrel for Dashboard/API, OTLP HTTP (standard 4318), and gRPC (standard 4317)
// Set QYL_GRPC_PORT=0 or QYL_OTLP_PORT=0 to disable individual listeners
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP endpoint for Dashboard, REST API, and SSE streaming
    options.ListenAnyIP(port, listenOptions => { listenOptions.Protocols = HttpProtocols.Http1AndHttp2; });

    // OTLP HTTP endpoint (standard port 4318) — dedicated listener for /v1/traces, /v1/logs
    if (otlpHttpPort > 0 && otlpHttpPort != port)
    {
        options.ListenAnyIP(otlpHttpPort, listenOptions => { listenOptions.Protocols = HttpProtocols.Http1AndHttp2; });
    }

    // gRPC endpoint for OTLP gRPC (TraceService.Export) - disabled when grpcPort <= 0
    if (grpcPort > 0)
    {
        options.ListenAnyIP(grpcPort, listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
    }
});

// Register gRPC services with compression support for OTLP clients
builder.Services.AddGrpc(options =>
{
    options.ResponseCompressionLevel = CompressionLevel.Optimal;
    options.ResponseCompressionAlgorithm = "gzip";
});
builder.Services.AddSingleton<IServiceMethodProvider<TraceServiceImpl>, TraceServiceMethodProvider>();

builder.Services.AddSingleton(new TokenAuthOptions
{
    Token = token,
    ExcludedPaths =
    [
        "/health", "/ready", "/alive", // Health checks
        "/v1/traces", "/v1/logs", // OTLP ingestion
        "/api/", // Dashboard API (public)
        "/assets/", // Dashboard static assets
        "/favicon.ico" // Favicon
    ]
});

// Keycloak JWKS validator: validates Bearer JWTs from qyl.mcp (client-credentials flow).
// NullKeycloakJwksValidator is registered when QYL_KEYCLOAK_AUTHORITY is not set,
// preserving existing auth behaviour unchanged.
var keycloakAuthority = builder.Configuration["QYL_KEYCLOAK_AUTHORITY"]
    ?? Environment.GetEnvironmentVariable("QYL_KEYCLOAK_AUTHORITY");
var keycloakAudience = builder.Configuration["QYL_KEYCLOAK_AUDIENCE"]
    ?? Environment.GetEnvironmentVariable("QYL_KEYCLOAK_AUDIENCE");

if (!string.IsNullOrWhiteSpace(keycloakAuthority))
{
    builder.Services.AddHttpClient("Keycloak").AddStandardResilienceHandler();
    builder.Services.AddSingleton<IKeycloakJwksValidator>(sp =>
        new KeycloakJwksValidator(
            keycloakAuthority,
            keycloakAudience,
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("Keycloak"),
            sp.GetRequiredService<ILogger<KeycloakJwksValidator>>()));
}
else
{
    builder.Services.AddSingleton<IKeycloakJwksValidator>(NullKeycloakJwksValidator.Instance);
}
builder.Services.AddSingleton<FrontendConsole>();
builder.Services.AddSingleton(_ => new DuckDbStore(dataPath));
builder.Services.AddSingleton<MigrationRunner>();
builder.Services.AddSingleton<SourceLocationCache>();
builder.Services.AddSingleton<PdbSourceResolver>();
builder.Services.AddSingleton<IBuildFailureStore>(_ =>
{
    var maxRetainedFailures = builder.Configuration.GetValue("QYL_MAX_BUILD_FAILURES", 10);
    return new DuckDbBuildFailureStore(dataPath, maxRetainedFailures);
});

// OTLP CORS configuration
var otlpCorsOptions = new OtlpCorsOptions
{
    AllowedOrigins = builder.Configuration["QYL_OTLP_CORS_ALLOWED_ORIGINS"] ?? "*",
    AllowedHeaders = builder.Configuration["QYL_OTLP_CORS_ALLOWED_HEADERS"]
};

// OTLP API key configuration
var otlpApiKeyOptions = new OtlpApiKeyOptions
{
    AuthMode = builder.Configuration["QYL_OTLP_AUTH_MODE"] ?? "Unsecured",
    PrimaryApiKey = builder.Configuration["QYL_OTLP_PRIMARY_API_KEY"],
    SecondaryApiKey = builder.Configuration["QYL_OTLP_SECONDARY_API_KEY"]
};

builder.Services.AddSingleton(otlpCorsOptions);
builder.Services.AddSingleton(otlpApiKeyOptions);

// SSE broadcasting with backpressure support for live telemetry streaming
builder.Services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();

// In-memory ring buffer for real-time span queries (sub-ms latency)
var ringBufferCapacity = builder.Configuration.GetValue("QYL_RINGBUFFER_CAPACITY", 10_000);
builder.Services.AddSingleton(new SpanRingBuffer(ringBufferCapacity));

// Workflow execution persistence (DuckDB-backed)
builder.Services.AddSingleton<IExecutionStore>(static sp =>
    new DuckDbExecutionStore(sp.GetRequiredService<DuckDbStore>()));

// Copilot observability tools (backed by DuckDbStore singleton)
builder.Services.AddSingleton<IReadOnlyList<AITool>>(static sp =>
    ObservabilityTools.Create(sp.GetRequiredService<DuckDbStore>(), TimeProvider.System));

// GitHub Copilot integration — bridges GitHubService token to Copilot auth (ADR-002)
builder.Services.AddQylCopilot(builder.Configuration);
// AG-UI SSE infrastructure (CopilotKit-compatible protocol)
builder.Services.AddQylAgui();
// Override auth options to bridge GitHubService token into Copilot (ADR-002 token bridge)
builder.Services.AddSingleton(sp => new CopilotAuthOptions
{
    AutoDetect = true, ExternalTokenProvider = () => sp.GetRequiredService<GitHubService>().GetToken()
});
builder.Services.AddQylCopilotTelemetry();

// Insights materializer: auto-generates system context from telemetry every 5 minutes
builder.Services.AddHostedService<InsightsMaterializerService>();
builder.Services.AddHostedService<ServiceMaterializerService>();
builder.Services.AddHostedService<EmbeddingClusterWorker>();

// Seer triage pipeline: auto-score and route untriaged error issues
builder.Services.AddSingleton<TriagePipelineService>();
builder.Services.AddHostedService(static sp => sp.GetRequiredService<TriagePipelineService>());

// Schema control: plan and apply additive schema promotions
builder.Services.AddSingleton<SchemaPlanner>();
builder.Services.AddSingleton<SchemaExecutor>();

// Identity + workspace services
builder.Services.AddSingleton<WorkspaceService>();
builder.Services.AddSingleton<HandshakeService>();
builder.Services.AddSingleton<ProjectService>();

// GitHub identity integration (ADR-002)
builder.Services.AddSingleton<GitHubService>();
builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue("GitHub:BaseAddress", "https://api.github.com/"));
    client.DefaultRequestHeaders.Add("User-Agent", "qyl/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
}).AddStandardResilienceHandler();

// Provisioning: profiles + code generation
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddSingleton<GenerationProfileService>();
builder.Services.AddSingleton<GenerationJobService>();

// Anomaly detection service (Z-score analysis on DuckDB metrics)
builder.Services.AddSingleton<AnomalyService>();

// Error issue engine + lifecycle + autofix
builder.Services.AddSingleton<IssueService>();
builder.Services.AddSingleton<ErrorLifecycleService>();
builder.Services.AddSingleton<AutofixOrchestrator>();
builder.Services.AddSingleton<PrCreationService>();

// Workflow run service
builder.Services.AddSingleton<WorkflowRunService>();

// Search: document-indexed full-text search with relevance scoring
builder.Services.AddSingleton<SearchService>();

// Zero-cost-until-observed: dynamic ActivityListener wiring on demand
builder.Services.AddSingleton<SubscriptionManager>();

// Auto-generated dashboards: telemetry detection, dynamic widgets
builder.Services.AddDashboardServices();

// .NET 10 telemetry: enrichment, redaction, buffering
builder.Services.AddQylTelemetry();

// Configure logging based on environment
builder.Logging.AddQylLogging(builder.Environment);

// Health checks with DuckDB connectivity
builder.Services.AddQylHealthChecks();

// Auto-activation for fail-fast at startup (catch config errors immediately)
builder.Services.ActivateSingleton<DuckDbStore>();

builder.Services.AddSingleton(sp =>
{
    var store = sp.GetRequiredService<DuckDbStore>();
    return new SessionQueryService(store);
});

builder.Services.AddSingleton(sp =>
{
    var store = sp.GetRequiredService<DuckDbStore>();
    return new AnalyticsQueryService(store);
});

builder.Services.AddSingleton(sp =>
{
    var store = sp.GetRequiredService<DuckDbStore>();
    return new AgentInsightsService(store);
});

var app = builder.Build();

// Register storage size callback for metrics (bridges DI-managed store with static metrics)
var duckDbStore = app.Services.GetRequiredService<DuckDbStore>();
QylMetrics.RegisterStorageSizeCallback(duckDbStore.GetStorageSizeBytes);

// Apply pending DuckDB schema migrations (after all base DDL has run)
var migrationRunner = app.Services.GetRequiredService<MigrationRunner>();
var migrationDirectory = Path.Combine(app.Environment.ContentRootPath, "Storage", "Migrations");
const int CollectorSchemaVersion = 20260214;
migrationRunner.ApplyPendingMigrations(duckDbStore.Connection, CollectorSchemaVersion, migrationDirectory);

// Initialize GitHub service: load persisted token from DuckDB (ADR-002)
app.Services.GetRequiredService<GitHubService>().InitializeAsync().GetAwaiter().GetResult();

// OTLP middleware (before token auth - OTLP has its own auth)
if (otlpCorsOptions.IsEnabled)
{
    app.UseMiddleware<OtlpCorsMiddleware>(otlpCorsOptions);
}

app.UseMiddleware<OtlpApiKeyMiddleware>(otlpApiKeyOptions);

var options = app.Services.GetRequiredService<TokenAuthOptions>();

app.UseMiddleware<TokenAuthMiddleware>(options);

// Request decompression must be before endpoints that read request body (OTLP)
app.UseRequestDecompression();

// .NET 10 telemetry middleware: request latency telemetry
app.UseQylTelemetry();

var hasEmbeddedDashboard = EmbeddedDashboardExtensions.HasEmbeddedDashboard();
if (hasEmbeddedDashboard)
    app.UseEmbeddedDashboard();
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// Map gRPC TraceService for OTLP ingestion on port 4317
app.MapGrpcService<TraceServiceImpl>();

app.MapGet("/api/v1/sessions",
    async (SessionQueryService queryService, int? limit, string? serviceName, CancellationToken ct) =>
    {
        var sessions = await queryService.GetSessionsAsync(limit ?? 100, 0, serviceName, ct: ct).ConfigureAwait(false);
        var response = SessionMapper.ToListResponse(sessions, sessions.Count, false);
        return Results.Ok(response);
    });

app.MapGet("/api/v1/sessions/{sessionId}",
    async (string sessionId, SessionQueryService queryService, CancellationToken ct) =>
        await queryService.GetSessionAsync(sessionId, ct).ConfigureAwait(false) is not { } session
            ? Results.NotFound()
            : Results.Ok(SessionMapper.ToDto(session)));

app.MapGet("/api/v1/sessions/{sessionId}/spans", SpanEndpoints.GetSessionSpansAsync);

app.MapGet("/api/v1/traces", async (
    DuckDbStore store,
    int? limit,
    CancellationToken ct) =>
{
    var boundedLimit = Math.Clamp(limit ?? 100, 1, 500);
    var spans = await store.GetSpansAsync(limit: boundedLimit, ct: ct).ConfigureAwait(false);
    return Results.Ok(new { items = spans, total = spans.Count });
});

app.MapGet("/api/v1/traces/{traceId}", SpanEndpoints.GetTraceAsync);


app.MapCopilotEndpoints();
// AG-UI endpoint — active when IChatClient is configured (QYL_LLM_* env vars)
if (app.Services.GetService<IChatClient>() is { } aguiChatClient)
{
    app.MapQylAguiChat(QylAgentBuilder.FromChatClient(aguiChatClient, agentName: "qyl-llm"));
}
app.MapClaudeCodeEndpoints();
app.MapServiceEndpoints();
app.MapSseEndpoints();
app.MapSpanMemoryEndpoints();
app.MapInsightsEndpoints();
app.MapDashboardEndpoints();
app.MapAnalyticsEndpoints();
app.MapObserveEndpoints();

app.MapGet("/api/v1/meta", () =>
{
    var version = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";

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
            Copilot = true,
            EmbeddedDashboard = hasEmbeddedDashboard
        },
        Status =
            new MetaStatus
            {
                GrpcEnabled = grpcPort > 0, AuthMode = otlpApiKeyOptions.IsApiKeyMode ? "api-key" : "unsecured"
            },
        Links = new MetaLinks
        {
            Dashboard = hasEmbeddedDashboard ? $"http://localhost:{port}" : null,
            OtlpHttp = otlpHttpPort > 0
                ? $"http://localhost:{otlpHttpPort}/v1/traces"
                : $"http://localhost:{port}/v1/traces",
            OtlpGrpc = grpcPort > 0 ? $"http://localhost:{grpcPort}" : null
        },
        Ports = new MetaPorts { Http = port, Grpc = grpcPort, OtlpHttp = otlpHttpPort }
    });
});

// Browser SDK script tag endpoint — serves the pre-built IIFE bundle
app.MapGet("/qyl.js", () =>
    Results.File(Path.Combine(app.Environment.WebRootPath, "qyl.js"), "application/javascript"));

app.MapPost("/api/v1/ingest", async (
    HttpContext context,
    DuckDbStore store,
    ITelemetrySseBroadcaster broadcaster,
    SpanRingBuffer ringBuffer,
    CancellationToken ct) =>
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

    // Push to ring buffer for real-time queries
    ringBuffer.PushRange(batch.Spans.Select(SpanMapper.ToRecord));

    await store.EnqueueAsync(batch, ct);

    broadcaster.PublishSpans(batch);

    return Results.Accepted();
});

app.MapPost("/v1/traces", async (
    HttpContext context,
    DuckDbStore store,
    ITelemetrySseBroadcaster broadcaster,
    SpanRingBuffer ringBuffer,
    CancellationToken ct) =>
{
    try
    {
        List<SpanStorageRow> spans;
        var contentType = context.Request.ContentType;

        // Handle protobuf format (application/x-protobuf)
        List<ServiceInstanceRecord> serviceInstances;
        if (OtlpProtobufParser.IsProtobufContentType(contentType))
        {
            var protoRequest = await OtlpProtobufParser.ParseFromRequestAsync(context.Request, ct);
            if (protoRequest.ResourceSpans.Count is 0)
                return Results.Accepted();

            spans = OtlpConverter.ConvertProtoToStorageRows(protoRequest);
            serviceInstances = OtlpConverter.ExtractServiceInstancesFromProto(protoRequest);
        }
        // Handle JSON format (application/json or default)
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

        // Apply Codex telemetry transformations (codex.* -> gen_ai.*)
        var batch = new SpanBatch(spans).WithCodexTransformations();

        // Push to ring buffer for real-time queries
        ringBuffer.PushRange(batch.Spans.Select(SpanMapper.ToRecord));

        await store.EnqueueAsync(batch, ct);

        // Upsert discovered services (idempotent, through write channel)
        foreach (var si in serviceInstances)
            await store.UpsertServiceInstanceAsync(si, ct);

        broadcaster.PublishSpans(batch);

        return Results.Accepted();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ErrorResponse("OTLP parse error", ex.Message));
    }
});

// OTLP Logs ingestion endpoint
app.MapPost("/v1/logs", async (
    HttpContext context,
    DuckDbStore store) =>
{
    try
    {
        var otlpData = await context.Request.ReadFromJsonAsync<OtlpExportLogsServiceRequest>(
            context.RequestAborted);

        if (otlpData?.ResourceLogs is null) return Results.BadRequest(new ErrorResponse("Invalid OTLP logs format"));

        var logs = OtlpConverter.ConvertLogsToStorageRows(otlpData);
        if (logs.Count is 0) return Results.Accepted();

        await store.InsertLogsAsync(logs);

        return Results.Accepted();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("OtlpLogsEndpoint");
        OtlpLogsLog.FailedToProcessPayload(logger, ex);
        return Results.BadRequest(new ErrorResponse("OTLP logs parse error", ex.Message));
    }
});

app.MapPost("/api/v1/console", (ConsoleIngestBatch batch, FrontendConsole console) =>
{
    foreach (var req in batch.Logs)
        console.Ingest(req);
    return Results.Accepted();
});

// OTLP Logs REST query endpoint
app.MapGet("/api/v1/logs", async (
    DuckDbStore store,
    string? session,
    string? trace,
    string? level,
    string? search,
    int? minSeverity,
    int? limit,
    CancellationToken ct) =>
{
    var logs = await store.GetLogsAsync(
        session,
        trace,
        level,
        minSeverity,
        search,
        limit: limit ?? 500,
        ct: ct);

    return Results.Ok(new { logs, total = logs.Count, has_more = logs.Count >= (limit ?? 500) });
});

app.MapGet("/api/v1/logs/live", async (
    HttpContext ctx,
    DuckDbStore store,
    string? session,
    string? trace,
    CancellationToken ct) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";

    static string MapSeverity(string? severityText, byte severityNumber)
    {
        var normalized = severityText?.Trim().ToLowerInvariant();
        if (normalized is "trace" or "debug" or "info" or "warn" or "error" or "fatal")
            return normalized;
        if (normalized is "warning") return "warn";
        if (normalized is "log") return "info";

        return severityNumber switch
        {
            >= 21 => "fatal",
            >= 17 => "error",
            >= 13 => "warn",
            >= 9 => "info",
            >= 5 => "debug",
            _ => "trace"
        };
    }

    static IReadOnlyDictionary<string, object> ParseAttributes(string? attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
            return new Dictionary<string, object>();

        try
        {
            using var document = JsonDocument.Parse(attributesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, object>();

            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.TryGetInt64(out var i) ? i : property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => property.Value.GetRawText()
                };
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    static object ToLiveLog(LogStorageRow log)
    {
        var timestamp = TimeConversions.UnixNanoToDateTime(log.TimeUnixNano).ToString("O");
        var observedTimestamp = log.ObservedTimeUnixNano.HasValue
            ? TimeConversions.UnixNanoToDateTime(log.ObservedTimeUnixNano.Value).ToString("O")
            : timestamp;

        return new
        {
            timestamp,
            observedTimestamp,
            traceId = log.TraceId,
            spanId = log.SpanId,
            severityNumber = (int)log.SeverityNumber,
            severityText = MapSeverity(log.SeverityText, log.SeverityNumber),
            body = log.Body ?? string.Empty,
            attributes = ParseAttributes(log.AttributesJson),
            serviceName = log.ServiceName ?? "unknown"
        };
    }

    await ctx.Response.WriteAsync("event: connected\ndata: {\"status\":\"ok\"}\n\n", ct).ConfigureAwait(false);
    await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);

    ulong? after = null;
    try
    {
        while (!ct.IsCancellationRequested)
        {
            var rows = await store.GetLogsAsync(
                sessionId: session,
                traceId: trace,
                after: after,
                limit: 250,
                ct: ct).ConfigureAwait(false);

            if (rows.Count > 0)
            {
                var ordered = rows.OrderBy(static l => l.TimeUnixNano).ToArray();
                after = ordered[^1].TimeUnixNano;

                var payload = ordered.Select(ToLiveLog).ToArray();
                var json = JsonSerializer.Serialize(new { logs = payload });
                await ctx.Response.WriteAsync($"event: logs\ndata: {json}\n\n", ct).ConfigureAwait(false);
                await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
    }
    catch (OperationCanceledException)
    {
        // Client disconnected.
    }
});

app.MapGet("/api/v1/genai/stats", async (
    SessionQueryService queryService,
    int? hours,
    string? session_id,
    CancellationToken ct) =>
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
});

app.MapGet("/api/v1/genai/spans", async (
    SessionQueryService queryService,
    string? session_id,
    int? limit,
    CancellationToken ct) =>
{
    var boundedLimit = Math.Clamp(limit ?? 100, 1, 500);
    var spans = await queryService.GetGenAiSpansAsync(session_id, boundedLimit, ct).ConfigureAwait(false);

    var items = spans.Select(span => new
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
});

app.MapGet("/api/v1/console", (FrontendConsole console, string? session, string? level, int? limit) =>
{
    var minLevel = level?.ToLowerInvariant() switch
    {
        "warn" => ConsoleLevel.Warn,
        "error" => ConsoleLevel.Error,
        _ => (ConsoleLevel?)null
    };
    return Results.Ok(console.Query(minLevel, session, null, limit ?? 50));
});

app.MapGet("/api/v1/console/errors", (FrontendConsole console, int? limit) =>
    Results.Ok(console.Errors(limit ?? 20)));

app.MapGet("/api/v1/console/live",
    async (HttpContext ctx, FrontendConsole console, string? session, CancellationToken ct) =>
    {
        ctx.Response.Headers.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers.Connection = "keep-alive";

        var channel = Channel.CreateBounded<ConsoleLogEntry>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        var connectionId = Guid.NewGuid().ToString("N")[..8];

        using var _ = console.Subscribe(connectionId, channel);

        await ctx.Response.WriteAsync($"event: connected\ndata: {{\"id\":\"{connectionId}\"}}\n\n", ct)
            .ConfigureAwait(false);
        await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);

        try
        {
            await foreach (var entry in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (session is not null && entry.Session != session) continue;
                var json = JsonSerializer.Serialize(entry, QylSerializerContext.Default.ConsoleLogEntry);
                await ctx.Response.WriteAsync($"event: console\ndata: {json}\n\n", ct).ConfigureAwait(false);
                await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - expected for SSE streams
        }
    });

// =============================================================================
// Telemetry Management API - Clear/Reset telemetry data
// =============================================================================

app.MapDelete("/api/v1/telemetry", async (
    DuckDbStore store,
    SpanRingBuffer ringBuffer,
    FrontendConsole console,
    string? type,
    CancellationToken ct) =>
{
    // If type is specified, clear only that type
    if (!string.IsNullOrEmpty(type))
    {
        var deleted = type.ToLowerInvariant() switch
        {
            "spans" or "traces" => await ClearSpansAsync(store, ringBuffer, ct).ConfigureAwait(false),
            "logs" => await store.ClearAllLogsAsync(ct).ConfigureAwait(false),
            "sessions" => await store.ClearAllSessionsAsync(ct).ConfigureAwait(false),
            "console" => ClearConsole(console),
            _ => throw new ArgumentException($"Unknown telemetry type: {type}")
        };
        return Results.Ok(new ClearTelemetryResponse(deleted, 0, 0, 0, type));
    }

    // Clear all telemetry
    var result = await store.ClearAllTelemetryAsync(ct).ConfigureAwait(false);
    ringBuffer.Clear();
    console.Clear();

    return Results.Ok(new ClearTelemetryResponse(
        result.SpansDeleted,
        result.LogsDeleted,
        result.SessionsDeleted,
        0, // console count not tracked
        "all"));

    static async Task<int> ClearSpansAsync(DuckDbStore store, SpanRingBuffer ringBuffer, CancellationToken ct)
    {
        var deleted = await store.ClearAllSpansAsync(ct).ConfigureAwait(false);
        ringBuffer.Clear();
        return deleted;
    }

    static int ClearConsole(FrontendConsole console)
    {
        console.Clear();
        return 0; // Console doesn't track count
    }
});

app.MapGet("/api/v1/telemetry/stats", async (DuckDbStore store, CancellationToken ct) =>
{
    var stats = await store.GetStorageStatsAsync(ct).ConfigureAwait(false);
    return Results.Ok(stats);
});


// Health check endpoints: /alive, /health, /ready (K8s probes) + /health/ui (dashboard)
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
app.MapCodingAgentEndpoints();
app.MapSeerSettingsEndpoints();
app.MapTriageEndpoints();
app.MapWorkflowEndpoints();
app.MapWorkflowRunEndpoints();
app.MapWorkflowEventEndpoints();
app.MapAgentRunEndpoints();
app.MapAgentInsightsEndpoints();
var buildFailureCaptureEnabled = builder.Configuration.GetValue("QYL_BUILD_FAILURE_CAPTURE_ENABLED", true);
if (buildFailureCaptureEnabled)
{
    app.MapBuildFailureEndpoints();
}

app.MapFallback(context =>
{
    var path = context.Request.Path.Value ?? "/";

    // Return 404 for API routes and static asset paths
    if (path.StartsWithIgnoreCase("/api/") ||
        path.StartsWithIgnoreCase("/v1/") ||
        path.StartsWithIgnoreCase("/assets/"))
    {
        context.Response.StatusCode = 404;
        return Task.CompletedTask;
    }

    // Embedded mode: middleware handles SPA routing, fallback is API-only 404
    if (hasEmbeddedDashboard)
    {
        context.Response.StatusCode = 404;
        return Task.CompletedTask;
    }

    // Physical files mode: serve index.html for SPA client-side routing
    var indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html";
        return context.Response.SendFileAsync(indexPath);
    }

    context.Response.StatusCode = 404;
    return context.Response.WriteAsync("Dashboard not found. Build with: nuke FrontendBuild && nuke DockerImageBuild");
});

// Note: Kestrel endpoints are configured via ConfigureKestrel above
// No need to set app.Urls when using explicit Kestrel configuration

StartupBanner.Print($"http://localhost:{port}", port, grpcPort, otlpHttpPort, otlpCorsOptions, otlpApiKeyOptions);

// Log when application is ready to accept requests
app.Lifetime.ApplicationStarted.Register(() =>
    Console.WriteLine($"[qyl] Application started and listening on port {port}"));

app.Run();

namespace qyl.collector
{
    internal static partial class OtlpLogsLog
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP logs payload")]
        public static partial void FailedToProcessPayload(ILogger logger, Exception ex);
    }
}
