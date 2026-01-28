using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using qyl.collector;
using qyl.collector.Auth;
using qyl.collector.Grpc;
using qyl.collector.Health;
using qyl.collector.Mcp;
using qyl.collector.Telemetry;

var builder = WebApplication.CreateSlimBuilder(args);

// Request decompression for OTLP clients sending gzip/deflate compressed payloads
builder.Services.AddRequestDecompression();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default);
});

var port = builder.Configuration.GetValue("QYL_PORT", 5100);
var grpcPort = builder.Configuration.GetValue("QYL_GRPC_PORT", 4317);
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
    Token = token
});
builder.Services.AddSingleton<FrontendConsole>();
builder.Services.AddSingleton(_ => new DuckDbStore(dataPath));
builder.Services.AddSingleton<McpServer>();

// OTLP CORS configuration
var otlpCorsOptions = new OtlpCorsOptions
{
    AllowedOrigins = builder.Configuration["QYL_OTLP_CORS_ALLOWED_ORIGINS"], AllowedHeaders = builder.Configuration["QYL_OTLP_CORS_ALLOWED_HEADERS"]
};

// OTLP API key configuration
var otlpApiKeyOptions = new OtlpApiKeyOptions
{
    AuthMode = builder.Configuration["QYL_OTLP_AUTH_MODE"] ?? "Unsecured", PrimaryApiKey = builder.Configuration["QYL_OTLP_PRIMARY_API_KEY"], SecondaryApiKey = builder.Configuration["QYL_OTLP_SECONDARY_API_KEY"]
};

builder.Services.AddSingleton(otlpCorsOptions);
builder.Services.AddSingleton(otlpApiKeyOptions);

// SSE broadcasting with backpressure support for live telemetry streaming
builder.Services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();

// In-memory ring buffer for real-time span queries (sub-ms latency)
var ringBufferCapacity = builder.Configuration.GetValue("QYL_RINGBUFFER_CAPACITY", 10_000);
builder.Services.AddSingleton(new SpanRingBuffer(ringBufferCapacity));

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

app.UseDefaultFiles();
app.UseStaticFiles();

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
    return Results.Ok(new
    {
        success = true
    });
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
    {
        var session = await queryService.GetSessionAsync(sessionId, ct).ConfigureAwait(false);
        if (session is null) return Results.NotFound();

        return Results.Ok(SessionMapper.ToDto(session));
    });

app.MapGet("/api/v1/sessions/{sessionId}/spans", (string sessionId, DuckDbStore store, CancellationToken ct) =>
    SpanEndpoints.GetSessionSpansAsync(sessionId, store, ct));

app.MapGet("/api/v1/traces/{traceId}", (string traceId, DuckDbStore store) =>
    SpanEndpoints.GetTraceAsync(traceId, store));

app.MapSseEndpoints();
app.MapSpanMemoryEndpoints();

app.MapPost("/api/v1/ingest", async (
    HttpContext context,
    DuckDbStore store,
    ITelemetrySseBroadcaster broadcaster,
    SpanRingBuffer ringBuffer) =>
{
    SpanBatch? batch;
    try
    {
        batch = await context.Request.ReadFromJsonAsync<SpanBatch>(
            QylSerializerContext.Default.SpanBatch);

        if (batch is null || batch.Spans.Count is 0)
            return Results.BadRequest(new ErrorResponse("Empty or invalid batch"));
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new ErrorResponse("Invalid JSON", ex.Message));
    }

    // Push to ring buffer for real-time queries
    ringBuffer.PushRange(batch.Spans.Select(SpanMapper.ToRecord));

    await store.EnqueueAsync(batch);

    broadcaster.PublishSpans(batch);

    return Results.Accepted();
});

app.MapPost("/v1/traces", async (
    HttpContext context,
    DuckDbStore store,
    ITelemetrySseBroadcaster broadcaster,
    SpanRingBuffer ringBuffer) =>
{
    try
    {
        var otlpData = await context.Request.ReadFromJsonAsync<OtlpExportTraceServiceRequest>(
            QylSerializerContext.Default.OtlpExportTraceServiceRequest);

        if (otlpData?.ResourceSpans is null) return Results.BadRequest(new ErrorResponse("Invalid OTLP format"));

        var spans = OtlpConverter.ConvertJsonToStorageRows(otlpData);
        if (spans.Count is 0) return Results.Accepted();

        var batch = new SpanBatch(spans);

        // Push to ring buffer for real-time queries
        ringBuffer.PushRange(batch.Spans.Select(SpanMapper.ToRecord));

        await store.EnqueueAsync(batch);

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
    Results.Ok(new
    {
        sessionId,
        feedback = Array.Empty<object>()
    }));

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

    return Results.Ok(new
    {
        logs, total = logs.Count, has_more = logs.Count >= (limit ?? 500)
    });
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

app.MapGet("/mcp/manifest", () => Results.Ok(McpServer.GetManifest()));

app.MapPost("/mcp/tools/call", async (McpToolCall call, McpServer mcp, CancellationToken ct) =>
{
    var response = await mcp.HandleToolCallAsync(call, ct).ConfigureAwait(false);
    return response.IsError ? Results.BadRequest(response) : Results.Ok(response);
});

// Health check endpoints (Aspire standard)
// /alive = Liveness (is the process running?) - only "live" tagged checks
// /health = Readiness (is the service ready for traffic?) - only "ready" tagged checks
app.MapGet("/alive", async (IServiceProvider sp, CancellationToken ct) =>
{
    var healthService = sp.GetService<HealthCheckService>();
    if (healthService is null)
        return Results.Ok(new qyl.collector.HealthResponse("healthy"));

    // Only check "live" tagged health checks - NOT DuckDB (which is "ready" only)
    var result = await healthService.CheckHealthAsync(
        c => c.Tags.Contains("live"), ct).ConfigureAwait(false);
    if (result.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy)
        return Results.Ok(new qyl.collector.HealthResponse("healthy"));

    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}).WithName("LivenessCheck");

app.MapGet("/health", async (IServiceProvider sp, CancellationToken ct) =>
{
    var healthService = sp.GetService<HealthCheckService>();
    if (healthService is null)
        return Results.Ok(new qyl.collector.HealthResponse("healthy"));

    var result = await healthService.CheckHealthAsync(
        c => c.Tags.Contains("ready"), ct).ConfigureAwait(false);
    if (result.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy)
        return Results.Ok(new qyl.collector.HealthResponse("healthy"));

    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}).WithName("ReadinessCheck");

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

app.MapGet("/api/v1/errors", (string? serviceName) =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapGet("/api/v1/errors/stats", () =>
    Results.Ok(new { total_count = 0 }));

app.MapGet("/api/v1/errors/{errorId}", (string errorId) =>
    Results.NotFound());

app.MapGet("/api/v1/exceptions", (string? serviceName) =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapGet("/api/v1/exceptions/stats", () =>
    Results.Ok(new { total_count = 0 }));

app.MapGet("/api/v1/deployments", (string? serviceName) =>
    Results.Ok(new { items = Array.Empty<object>(), total = 0 }));

app.MapPost("/api/v1/deployments", () =>
    Results.Created("/api/v1/deployments/1", new { id = "1" }));

app.MapGet("/api/v1/deployments/metrics/dora", () =>
    Results.Ok(new {
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

    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 404;
        return Task.CompletedTask;
    }

    return Task.CompletedTask;
});

// Note: Kestrel endpoints are configured via ConfigureKestrel above
// No need to set app.Urls when using explicit Kestrel configuration

StartupBanner.Print($"http://localhost:{port}", token, port, grpcPort, otlpCorsOptions, otlpApiKeyOptions);

app.Run();

namespace qyl.collector
{
    /// <summary>
    ///     Span API endpoints.
    /// </summary>
    internal static class SpanEndpoints
    {
        public static async Task<IResult> GetSessionSpansAsync(string sessionId, DuckDbStore store,
            CancellationToken ct)
        {
            var spans = await store.GetSpansBySessionAsync(sessionId, ct).ConfigureAwait(false);

            // Extract service name from first span's attributes if available
            var serviceName = "unknown";
            if (spans.Count > 0 && spans[0].AttributesJson is { } attrJson)
            {
                try
                {
                    var attrs = JsonSerializer.Deserialize(attrJson,
                        QylSerializerContext.Default.DictionaryStringString);
                    if (attrs?.TryGetValue("service.name", out var svc) == true)
                        serviceName = svc;
                }
                catch
                {
                    /* ignore parse errors */
                }
            }

            var spanDtos = spans.Select(s => SpanMapper.ToDto(s, serviceName)).ToList();
            return Results.Ok(new SpanListResponseDto
            {
                Spans = spanDtos
            });
        }

        public static async Task<IResult> GetTraceAsync(string traceId, DuckDbStore store)
        {
            var spans = await store.GetTraceAsync(traceId).ConfigureAwait(false);
            if (spans.Count is 0) return Results.NotFound();

            var spanDtos = SpanMapper.ToDtos(spans, static r => (r.Name.Split(' ').LastOrDefault() ?? "unknown", null));
            var rootSpan = spanDtos.FirstOrDefault(static s => s.ParentSpanId is null);

            return Results.Ok(new TraceResponseDto
            {
                TraceId = traceId,
                Spans = spanDtos,
                RootSpan = rootSpan,
                DurationMs = rootSpan?.DurationMs,
                Status = rootSpan?.Status
            });
        }
    }

    // =============================================================================
    // GenAI Provider Detection - OTel 1.39 gen_ai.provider.name values
    // =============================================================================

    /// <summary>
    ///     Provider identifiers per OTel 1.39 gen_ai.provider.name values.
    ///     Supports host-based provider detection for automatic attribution.
    /// </summary>
    public static class GenAiProviders
    {
        // OTel 1.39 provider name constants
        public const string OpenAi = "openai";
        public const string Anthropic = "anthropic";
        public const string GcpGemini = "gcp.gemini";
        public const string AwsBedrock = "aws.bedrock";
        public const string AzureOpenAi = "azure.openai";
        public const string Cohere = "cohere";
        public const string Mistral = "mistral";

        private static readonly FrozenDictionary<string, string> SHostToProvider =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["api.openai.com"] = OpenAi,
                ["api.anthropic.com"] = Anthropic,
                ["generativelanguage.googleapis.com"] = GcpGemini,
                ["openai.azure.com"] = AzureOpenAi,
                ["api.cohere.ai"] = Cohere,
                ["api.mistral.ai"] = Mistral
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Attempts to detect the GenAI provider from an API host.
        /// </summary>
        /// <param name="host">The API host (e.g., "api.openai.com")</param>
        /// <param name="provider">The detected provider name if successful</param>
        /// <returns>True if provider was detected, false otherwise</returns>
        public static bool TryDetectFromHost(ReadOnlySpan<char> host, [NotNullWhen(true)] out string? provider)
        {
            // Check frozen dict first for exact matches
            var hostStr = host.ToString();
            if (SHostToProvider.TryGetValue(hostStr, out provider))
                return true;

            // Partial match for AWS Bedrock (contains region in hostname)
            if (host.Contains("bedrock-runtime", StringComparison.OrdinalIgnoreCase))
            {
                provider = AwsBedrock;
                return true;
            }

            provider = null;
            return false;
        }

        /// <summary>
        ///     Attempts to detect the GenAI provider from a full URL.
        /// </summary>
        public static bool TryDetectFromUrl(Uri url, [NotNullWhen(true)] out string? provider) =>
            TryDetectFromHost(url.Host.AsSpan(), out provider);
    }

// =============================================================================
// qyl GenAI Attribute Extractor - SINGLE SOURCE OF TRUTH
// Extracts GenAI semantic convention attributes from span attributes
// Supports both dictionary and JSON-based attribute access
// =============================================================================

    /// <summary>
    ///     Extracts GenAI-specific attributes from span attribute collections.
    ///     Handles both current (OTel 1.39) and deprecated attribute names.
    /// </summary>
    public static class GenAiExtractor
    {
        // =========================================================================
        // JSON-based extraction (string or JsonElement)
        // =========================================================================

        /// <summary>
        ///     Extracts GenAI fields from a JSON string of attributes.
        /// </summary>
        public static GenAiFields Extract(string? attributesJson)
        {
            if (string.IsNullOrEmpty(attributesJson))
                return GenAiFields.Empty;

            try
            {
                using var doc = JsonDocument.Parse(attributesJson);
                return Extract(doc.RootElement);
            }
            catch
            {
                return GenAiFields.Empty;
            }
        }

        // =========================================================================
        // Dictionary helpers
        // =========================================================================

        private static string? GetString(IReadOnlyDictionary<string, object?> attrs, string key) =>
            attrs.TryGetValue(key, out var value) ? value?.ToString() : null;

        private static long? GetLong(IReadOnlyDictionary<string, object?> attrs, string key)
        {
            if (!attrs.TryGetValue(key, out var value) || value is null)
                return null;

            return value switch
            {
                long l => l,
                int i => i,
                double d => (long)d,
                string s when long.TryParse(s, out var parsed) => parsed,
                _ => null
            };
        }

        private static double? GetDouble(IReadOnlyDictionary<string, object?> attrs, string key)
        {
            if (!attrs.TryGetValue(key, out var value) || value is null)
                return null;

            return value switch
            {
                double d => d,
                float f => f,
                long l => l,
                int i => i,
                string s when double.TryParse(s, out var parsed) => parsed,
                _ => null
            };
        }

        // =========================================================================
        // JSON helpers
        // =========================================================================

        private static string? GetJsonString(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static long? GetJsonLong(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.TryGetInt64(out var l) ? l : null,
                JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        private static double? GetJsonDouble(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.TryGetDouble(out var d) ? d : null,
                JsonValueKind.String when double.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        // =========================================================================
        // Dictionary-based extraction (IReadOnlyDictionary<string, object?>)
        // =========================================================================

        /// <summary>
        ///     Extracts GenAI fields from a dictionary of attributes.
        /// </summary>
        public static GenAiFields Extract(IReadOnlyDictionary<string, object?> attributes)
        {
            Throw.IfNull(attributes);

            var inputTokens = GetLong(attributes, GenAiUsageAttributes.InputTokens);
            var outputTokens = GetLong(attributes, GenAiUsageAttributes.OutputTokens);

            return new GenAiFields
            {
                ProviderName = GetString(attributes, GenAiProviderAttributes.Name),
                OperationName = GetString(attributes, GenAiOperationAttributes.Name),
                RequestModel = GetString(attributes, GenAiRequestAttributes.Model),
                ResponseModel = GetString(attributes, GenAiResponseAttributes.Model),
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = (inputTokens ?? 0) + (outputTokens ?? 0),
                Temperature = GetDouble(attributes, GenAiRequestAttributes.Temperature),
                MaxTokens = GetLong(attributes, GenAiRequestAttributes.MaxTokens),
                FinishReason = GetString(attributes, GenAiResponseAttributes.FinishReasons),
                CostUsd = GetDouble(attributes, QylAttributes.CostUsd),
                SessionId = GetString(attributes, QylAttributes.SessionId)
                            ?? GetString(attributes, GenAiConversationAttributes.Id),
                ToolName = GetString(attributes, GenAiToolAttributes.Name),
                ToolCallId = GetString(attributes, GenAiToolAttributes.CallId)
            };
        }

        /// <summary>
        ///     Checks if the attributes contain any GenAI-related keys.
        /// </summary>
        public static bool IsGenAiSpan(IReadOnlyDictionary<string, object?> attributes)
        {
            Throw.IfNull(attributes);

            return attributes.ContainsKey(GenAiProviderAttributes.Name) ||
                   attributes.ContainsKey(GenAiRequestAttributes.Model);
        }

        /// <summary>
        ///     Extracts GenAI fields from a JsonElement.
        /// </summary>
        public static GenAiFields Extract(JsonElement attributes)
        {
            var inputTokens = GetJsonLong(attributes, GenAiUsageAttributes.InputTokens);
            var outputTokens = GetJsonLong(attributes, GenAiUsageAttributes.OutputTokens);

            return new GenAiFields
            {
                ProviderName = GetJsonString(attributes, GenAiProviderAttributes.Name),
                OperationName = GetJsonString(attributes, GenAiOperationAttributes.Name),
                RequestModel = GetJsonString(attributes, GenAiRequestAttributes.Model),
                ResponseModel = GetJsonString(attributes, GenAiResponseAttributes.Model),
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = (inputTokens ?? 0) + (outputTokens ?? 0),
                Temperature = GetJsonDouble(attributes, GenAiRequestAttributes.Temperature),
                MaxTokens = GetJsonLong(attributes, GenAiRequestAttributes.MaxTokens),
                FinishReason = GetJsonString(attributes, GenAiResponseAttributes.FinishReasons),
                CostUsd = GetJsonDouble(attributes, QylAttributes.CostUsd),
                SessionId = GetJsonString(attributes, QylAttributes.SessionId)
                            ?? GetJsonString(attributes, GenAiConversationAttributes.Id),
                ToolName = GetJsonString(attributes, GenAiToolAttributes.Name),
                ToolCallId = GetJsonString(attributes, GenAiToolAttributes.CallId)
            };
        }

        /// <summary>
        ///     Checks if JSON attributes contain any GenAI-related keys.
        /// </summary>
        public static bool IsGenAiSpan(string? attributesJson)
        {
            if (string.IsNullOrEmpty(attributesJson))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(attributesJson);
                return IsGenAiSpan(doc.RootElement);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Checks if JsonElement contains any GenAI-related keys.
        /// </summary>
        public static bool IsGenAiSpan(JsonElement attributes) =>
            attributes.TryGetProperty(GenAiProviderAttributes.Name, out _) ||
            attributes.TryGetProperty(GenAiRequestAttributes.Model, out _);
    }

    /// <summary>
    ///     Extracted GenAI span data.
    /// </summary>
    public sealed record GenAiFields
    {
        public static readonly GenAiFields Empty = new();

        /// <summary>Provider name (e.g., "anthropic", "openai", "google").</summary>
        public string? ProviderName { get; init; }

        /// <summary>Operation name (e.g., "chat", "completion", "embedding").</summary>
        public string? OperationName { get; init; }

        /// <summary>Model ID from the request.</summary>
        public string? RequestModel { get; init; }

        /// <summary>Model ID from the response (may differ from request).</summary>
        public string? ResponseModel { get; init; }

        /// <summary>Number of input/prompt tokens.</summary>
        public long? InputTokens { get; init; }

        /// <summary>Number of output/completion tokens.</summary>
        public long? OutputTokens { get; init; }

        /// <summary>Total tokens (if provided explicitly).</summary>
        public long? TotalTokens { get; init; }

        /// <summary>Request temperature parameter.</summary>
        public double? Temperature { get; init; }

        /// <summary>Request max tokens parameter.</summary>
        public long? MaxTokens { get; init; }

        /// <summary>Response finish/stop reason.</summary>
        public string? FinishReason { get; init; }

        /// <summary>Estimated cost in USD.</summary>
        public double? CostUsd { get; init; }

        /// <summary>Session/conversation ID.</summary>
        public string? SessionId { get; init; }

        /// <summary>Tool name (for tool calls).</summary>
        public string? ToolName { get; init; }

        /// <summary>Tool call ID (for tool calls).</summary>
        public string? ToolCallId { get; init; }

        /// <summary>Effective model (response model if available, else request model).</summary>
        public string? Model => ResponseModel ?? RequestModel;

        /// <summary>Whether this span has token usage data.</summary>
        public bool HasTokenUsage => InputTokens.HasValue || OutputTokens.HasValue;

        /// <summary>Whether this represents a GenAI span.</summary>
        public bool IsGenAi => ProviderName is not null || Model is not null;

        /// <summary>Whether this is a tool call span.</summary>
        public bool IsToolCall => ToolName is not null || ToolCallId is not null;

        /// <summary>Computed total tokens (sum of input + output if total not provided).</summary>
        public long ComputedTotalTokens => TotalTokens ?? (InputTokens ?? 0) + (OutputTokens ?? 0);
    }

    public static class QylAttributes
    {
        private const string Prefix = "qyl";

        public const string CostUsd = $"{Prefix}.cost.usd";

        public const string CostCurrency = $"{Prefix}.cost.currency";

        public const string SessionId = $"{Prefix}.session.id";

        public const string SessionName = $"{Prefix}.session.name";

        public const string FeedbackScore = $"{Prefix}.feedback.score";

        public const string FeedbackComment = $"{Prefix}.feedback.comment";

        public const string AgentId = $"{Prefix}.agent.id";

        public const string AgentName = $"{Prefix}.agent.name";

        public const string AgentRole = $"{Prefix}.agent.role";

        public const string WorkflowId = $"{Prefix}.workflow.id";

        public const string WorkflowStep = $"{Prefix}.workflow.step";

        public const string WorkflowStepIndex = $"{Prefix}.workflow.step.index";
    }
}

namespace qyl.collector
{
    /// <summary>JSON-serializable OTLP event representation for AOT compatibility.</summary>
    public sealed record OtlpEventJson(string? Name, ulong TimeUnixNano, Dictionary<string, string?>? Attributes);

    public sealed record ConsoleIngestBatch(List<ConsoleIngestRequest> Logs);

    public sealed record FeedbackResponse(IReadOnlyList<object> Feedback);

    public sealed record HealthResponse(string Status);

    public sealed record AuthCheckResponse(bool Authenticated);

    public sealed record SseConnectedEvent(string ConnectionId);

    public sealed record ErrorResponse(string Error, string? Message = null);

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false)]
    [JsonSerializable(typeof(LoginRequest))]
    [JsonSerializable(typeof(LoginResponse))]
    [JsonSerializable(typeof(AuthCheckResponse))]
    [JsonSerializable(typeof(StorageStats))]
    [JsonSerializable(typeof(GenAiStats))]
    [JsonSerializable(typeof(McpToolCall))]
    [JsonSerializable(typeof(McpResponse))]
    [JsonSerializable(typeof(McpContent))]
    [JsonSerializable(typeof(McpManifest))]
    [JsonSerializable(typeof(McpTool))]
    [JsonSerializable(typeof(McpTool[]))]
    [JsonSerializable(typeof(ConsoleLogEntry))]
    [JsonSerializable(typeof(ConsoleLogEntry[]))]
    [JsonSerializable(typeof(ConsoleIngestRequest))]
    [JsonSerializable(typeof(ConsoleIngestBatch))]
    [JsonSerializable(typeof(TelemetryEventDto))]
    [JsonSerializable(typeof(TelemetryMessage))]
    [JsonSerializable(typeof(SseConnectedEvent))]
    [JsonSerializable(typeof(SpanDto))]
    [JsonSerializable(typeof(SpanDto[]))]
    [JsonSerializable(typeof(List<SpanDto>))]
    [JsonSerializable(typeof(GenAiSpanDataDto))]
    [JsonSerializable(typeof(SpanEventDto))]
    [JsonSerializable(typeof(SpanLinkDto))]
    [JsonSerializable(typeof(SessionDto))]
    [JsonSerializable(typeof(SessionDto[]))]
    [JsonSerializable(typeof(List<SessionDto>))]
    [JsonSerializable(typeof(SessionGenAiStatsDto))]
    [JsonSerializable(typeof(SessionListResponseDto))]
    [JsonSerializable(typeof(SpanListResponseDto))]
    [JsonSerializable(typeof(TraceResponseDto))]
    [JsonSerializable(typeof(SpanBatchDto))]
    [JsonSerializable(typeof(HealthResponse))]
    [JsonSerializable(typeof(ErrorResponse))]
    [JsonSerializable(typeof(FeedbackResponse))]
    [JsonSerializable(typeof(SpanBatch))]
    [JsonSerializable(typeof(SpanStorageRow))]
    [JsonSerializable(typeof(List<SpanStorageRow>))]
    [JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "DictionaryStringString")]
    [JsonSerializable(typeof(OtlpExportTraceServiceRequest))]
    [JsonSerializable(typeof(OtlpResourceSpans))]
    [JsonSerializable(typeof(OtlpResource))]
    [JsonSerializable(typeof(OtlpScopeSpans))]
    [JsonSerializable(typeof(OtlpSpan))]
    [JsonSerializable(typeof(OtlpStatus))]
    [JsonSerializable(typeof(OtlpKeyValue))]
    [JsonSerializable(typeof(OtlpAnyValue))]
    [JsonSerializable(typeof(OtlpExportLogsServiceRequest))]
    [JsonSerializable(typeof(OtlpResourceLogs))]
    [JsonSerializable(typeof(OtlpScopeLogs))]
    [JsonSerializable(typeof(OtlpLogRecord))]
    [JsonSerializable(typeof(LogStorageRow))]
    [JsonSerializable(typeof(List<LogStorageRow>))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(OtlpEventJson[]))]
    // Ring buffer response types
    [JsonSerializable(typeof(RecentSpansResponse))]
    [JsonSerializable(typeof(TraceFromMemoryResponse))]
    [JsonSerializable(typeof(SessionSpansFromMemoryResponse))]
    [JsonSerializable(typeof(BufferStatsResponse))]
    internal partial class QylSerializerContext : JsonSerializerContext;
}
