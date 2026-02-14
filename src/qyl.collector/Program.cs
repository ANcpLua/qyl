using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using qyl.collector;
using qyl.collector.Auth;
using qyl.collector.Copilot;
using qyl.collector.Grpc;
using qyl.collector.Health;
using qyl.collector.Insights;
using qyl.collector.Alerting;
using qyl.collector.Dashboards;
using qyl.collector.Telemetry;
using qyl.copilot;
using qyl.copilot.Auth;
using System.Reflection;
using qyl.collector.Dashboard;
using qyl.collector.Meta;
using Qyl.ServiceDefaults;

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

// Startup diagnostics for Railway debugging
Console.WriteLine(
    $"[qyl] Starting on port {port} (QYL_PORT={Environment.GetEnvironmentVariable("QYL_PORT")}, PORT={Environment.GetEnvironmentVariable("PORT")})");
Console.WriteLine($"[qyl] gRPC port: {grpcPort} (0=disabled)");
var token = builder.Configuration["QYL_TOKEN"] ?? TokenGenerator.Generate();
var dataPath = builder.Configuration["QYL_DATA_PATH"] ?? "qyl.duckdb";

// Ensure parent directory exists for DuckDB file
var dataDir = Path.GetDirectoryName(dataPath);
if (!string.IsNullOrEmpty(dataDir))
    Directory.CreateDirectory(dataDir);

// Configure Kestrel for HTTP (Dashboard/API) and optional gRPC (OTLP) endpoints
// Set QYL_GRPC_PORT=0 to disable gRPC endpoint (useful on Railway/single-port platforms)
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP endpoint for Dashboard, REST API, and OTLP HTTP (/v1/traces)
    options.ListenAnyIP(port, listenOptions => { listenOptions.Protocols = HttpProtocols.Http1AndHttp2; });

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
        "/v1/traces", // OTLP ingestion
        "/api/", // Dashboard API (public)
        "/assets/", // Dashboard static assets
        "/favicon.ico", // Favicon
    ]
});
builder.Services.AddSingleton<FrontendConsole>();
builder.Services.AddSingleton(_ => new DuckDbStore(dataPath));
builder.Services.AddSingleton<MigrationRunner>();

// OTLP CORS configuration
var otlpCorsOptions = new OtlpCorsOptions
{
    AllowedOrigins = builder.Configuration["QYL_OTLP_CORS_ALLOWED_ORIGINS"],
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
builder.Services.AddSingleton<qyl.copilot.Workflows.IExecutionStore>(static sp =>
    new DuckDbExecutionStore(sp.GetRequiredService<DuckDbStore>()));

// Copilot observability tools (backed by DuckDbStore singleton)
builder.Services.AddSingleton<IReadOnlyList<Microsoft.Extensions.AI.AITool>>(static sp =>
    ObservabilityTools.Create(sp.GetRequiredService<DuckDbStore>(), TimeProvider.System));

// GitHub Copilot integration (auto-detect auth, zero config)
builder.Services.AddQylCopilot(o => { o.AuthOptions = new CopilotAuthOptions { AutoDetect = true }; });
builder.Services.AddQylCopilotTelemetry();

// Insights materializer: auto-generates system context from telemetry every 5 minutes
builder.Services.AddHostedService<InsightsMaterializerService>();

// SQL alerting: YAML-defined rules, periodic evaluation, webhook/console/SSE notifications
builder.Services.AddAlertingServices();

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

var app = builder.Build();

// Register storage size callback for metrics (bridges DI-managed store with static metrics)
var duckDbStore = app.Services.GetRequiredService<DuckDbStore>();
QylMetrics.RegisterStorageSizeCallback(duckDbStore.GetStorageSizeBytes);

// Initialize alert history schema (must happen after DuckDbStore is resolved)
duckDbStore.InitializeAlertSchema();

// Apply pending DuckDB schema migrations (after all base DDL has run)
var migrationRunner = app.Services.GetRequiredService<MigrationRunner>();
migrationRunner.ApplyPendingMigrations(duckDbStore.Connection, DuckDbSchema.Version);

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

app.MapPost("/api/login", (LoginRequest request, HttpContext context) =>
{
    var isValid = CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(request.Token),
        Encoding.UTF8.GetBytes(token));

    if (isValid)
    {
        context.Response.Cookies.Append("qyl_token", request.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = TimeProvider.System.GetUtcNow().AddDays(3),
                Path = "/"
            });

        return Results.Ok(new LoginResponse(true));
    }

    return Results.BadRequest(new LoginResponse(false, "Invalid token"));
});

app.MapPost("/api/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete("qyl_token");
    return Results.Ok(new { success = true });
});

app.MapGet("/api/auth/check", (HttpContext context) =>
{
    var cookieToken = context.Request.Cookies["qyl_token"];
    var isValid = !string.IsNullOrEmpty(cookieToken) &&
                  CryptographicOperations.FixedTimeEquals(
                      Encoding.UTF8.GetBytes(cookieToken),
                      Encoding.UTF8.GetBytes(token));

    return Results.Ok(new AuthCheckResponse(isValid));
});

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

app.MapGet("/api/v1/traces/{traceId}", SpanEndpoints.GetTraceAsync);


app.MapCopilotEndpoints();
app.MapSseEndpoints();
app.MapSpanMemoryEndpoints();
app.MapInsightsEndpoints();
app.MapAlertEndpoints();
app.MapDashboardEndpoints();

app.MapGet("/api/v1/meta", () =>
{
    var version = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";

    return Results.Ok(new MetaResponse
    {
        Version = version,
        Runtime = $"dotnet/{Environment.Version}",
        Build = new MetaBuild
        {
            InformationalVersion = version,
            Commit = version.Contains('+') ? version[(version.IndexOf('+') + 1)..] : null,
        },
        Capabilities = new MetaCapabilities
        {
            Tracing = true,
            Grpc = true,
            Alerting = true,
            GenAi = true,
            Copilot = true,
            EmbeddedDashboard = hasEmbeddedDashboard,
        },
        Status = new MetaStatus
        {
            GrpcEnabled = grpcPort > 0,
            AuthMode = otlpApiKeyOptions.IsApiKeyMode ? "api-key" : "unsecured",
        },
        Links = new MetaLinks
        {
            Dashboard = hasEmbeddedDashboard ? $"http://localhost:{port}" : null,
            OtlpHttp = $"http://localhost:{port}/v1/traces",
            OtlpGrpc = grpcPort > 0 ? $"http://localhost:{grpcPort}" : null,
        },
        Ports = new MetaPorts { Http = port, Grpc = grpcPort },
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
        if (OtlpProtobufParser.IsProtobufContentType(contentType))
        {
            var protoRequest = await OtlpProtobufParser.ParseFromRequestAsync(context.Request, ct);
            if (protoRequest.ResourceSpans.Count is 0)
                return Results.Accepted();

            spans = OtlpConverter.ConvertProtoToStorageRows(protoRequest);
        }
        // Handle JSON format (application/json or default)
        else
        {
            var otlpData = await context.Request.ReadFromJsonAsync<OtlpExportTraceServiceRequest>(
                QylSerializerContext.Default.OtlpExportTraceServiceRequest, ct);

            if (otlpData?.ResourceSpans is null)
                return Results.BadRequest(new ErrorResponse("Invalid OTLP format"));

            spans = OtlpConverter.ConvertJsonToStorageRows(otlpData);
        }

        if (spans.Count is 0) return Results.Accepted();

        // Apply Codex telemetry transformations (codex.* -> gen_ai.*)
        var batch = new SpanBatch(spans).WithCodexTransformations();

        // Push to ring buffer for real-time queries
        ringBuffer.PushRange(batch.Spans.Select(SpanMapper.ToRecord));

        await store.EnqueueAsync(batch, ct);

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
            QylSerializerContext.Default.OtlpExportLogsServiceRequest);

        if (otlpData?.ResourceLogs is null) return Results.BadRequest(new ErrorResponse("Invalid OTLP logs format"));

        var logs = OtlpConverter.ConvertLogsToStorageRows(otlpData);
        if (logs.Count is 0) return Results.Accepted();

        await store.InsertLogsAsync(logs);

        return Results.Accepted();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ErrorResponse("OTLP logs parse error", ex.Message));
    }
});

app.MapPost("/api/v1/feedback", () => Results.Accepted());
app.MapGet("/api/v1/sessions/{sessionId}/feedback", (string sessionId) =>
    Results.Ok(new { sessionId, feedback = Array.Empty<object>() }));

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


// Health check endpoints (Aspire standard)
// /alive = Liveness (is the process running?) - only "live" tagged checks
// /health = Readiness (is the service ready for traffic?) - only "ready" tagged checks
app.MapGet("/alive", RunHealthCheck("healthy", "live")).WithName("LivenessCheck");
app.MapGet("/health", RunHealthCheck("healthy", "ready")).WithName("HealthCheck");
app.MapGet("/ready", RunHealthCheck("ready", "ready")).WithName("ReadyCheck");

static Func<IServiceProvider, CancellationToken, Task<IResult>> RunHealthCheck(string label, string tag) =>
    async (sp, ct) =>
    {
        var healthService = sp.GetService<HealthCheckService>();
        if (healthService is null)
            return Results.Ok(new qyl.collector.HealthResponse(label));

        var result = await healthService.CheckHealthAsync(
            c => c.Tags.Contains(tag), ct).ConfigureAwait(false);
        return result.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
            ? Results.Ok(new qyl.collector.HealthResponse(label))
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    };

// =============================================================================
// API Stubs for OpenAPI Compliance
// =============================================================================

app.MapGet("/api/v1/traces", (string? serviceName, int? limit) =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapPost("/api/v1/traces/search", () =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapGet("/api/v1/metrics", (string? serviceName) =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapPost("/api/v1/metrics/query", () =>
    Results.Ok(new { series = Array.Empty<object>() }));

app.MapGet("/api/v1/metrics/{metricName}", (string metricName) =>
    Results.NotFound());

app.MapErrorEndpoints();

app.MapGet("/api/v1/exceptions", (string? serviceName) =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapGet("/api/v1/exceptions/stats", () =>
    Results.Ok(new { total_count = 0 }));

app.MapGet("/api/v1/deployments", (string? serviceName) =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapPost("/api/v1/deployments", () =>
    Results.Created("/api/v1/deployments/1", new { id = "1" }));

app.MapGet("/api/v1/deployments/metrics/dora", () =>
    Results.Ok(new
    {
        deployment_frequency = 0,
        lead_time_hours = 0,
        change_failure_rate = 0,
        mttr_hours = 0,
        performance_level = "low"
    }));

app.MapGet("/api/v1/pipelines", () =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapGet("/api/v1/pipelines/stats", () =>
    Results.Ok(new { total_runs = 0, success_rate = 0 }));

app.MapGet("/api/v1/services", () =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapGet("/api/v1/services/{serviceName}", (string serviceName) =>
    Results.Ok(new { name = serviceName, instance_count = 1 }));


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

StartupBanner.Print($"http://localhost:{port}", token, port, grpcPort, otlpCorsOptions, otlpApiKeyOptions);

// Log when application is ready to accept requests
app.Lifetime.ApplicationStarted.Register(() =>
    Console.WriteLine($"[qyl] Application started and listening on port {port}"));

app.Run();
