using Microsoft.AspNetCore.Server.Kestrel.Core;
using qyl.collector;
using qyl.collector.Auth;
using qyl.collector.Grpc;
using qyl.collector.Mapping;
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

// Configure Kestrel for HTTP (Dashboard/API) and gRPC (OTLP) endpoints
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP endpoint for Dashboard, REST API, and OTLP HTTP (/v1/traces)
    options.ListenAnyIP(port, listenOptions => { listenOptions.Protocols = HttpProtocols.Http1AndHttp2; });

    // gRPC endpoint for OTLP gRPC (TraceService.Export) on port 4317
    options.ListenAnyIP(grpcPort, listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
});

// Register gRPC services with compression support for OTLP clients
builder.Services.AddGrpc(options =>
{
    options.ResponseCompressionLevel = CompressionLevel.Optimal;
    options.ResponseCompressionAlgorithm = "gzip";
});
builder.Services.AddSingleton<IServiceMethodProvider<TraceServiceImpl>, TraceServiceMethodProvider>();

builder.Services.AddSingleton(new TokenAuthOptions { Token = token });
builder.Services.AddSingleton<FrontendConsole>();
builder.Services.AddSingleton(_ => new DuckDbStore(dataPath));
builder.Services.AddSingleton<McpServer>();

// SSE broadcasting with backpressure support for live telemetry streaming
builder.Services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();

// .NET 10 telemetry: enrichment, redaction, buffering
builder.Services.AddQylTelemetry();

builder.Services.AddSingleton(sp =>
{
    var store = sp.GetRequiredService<DuckDbStore>();
    return new SessionQueryService(store.Connection);
});

var app = builder.Build();

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
    return Results.Ok(new { success = true });
});

app.MapGet("/api/auth/check", (HttpContext context) =>
{
    var cookieToken = context.Request.Cookies["qyl_token"];
    var isValid = !string.IsNullOrEmpty(cookieToken) &&
                  CryptographicOperations.FixedTimeEquals(
                      Encoding.UTF8.GetBytes(cookieToken),
                      Encoding.UTF8.GetBytes(token));

    return Results.Ok(new { authenticated = isValid });
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

app.MapGet("/api/v1/sessions/{sessionId}/spans",
    async (string sessionId, DuckDbStore store, CancellationToken ct) =>
        await SpanEndpoints.GetSessionSpansAsync(sessionId, store, ct).ConfigureAwait(false));

app.MapGet("/api/v1/traces/{traceId}",
    async (string traceId, DuckDbStore store) =>
        await SpanEndpoints.GetTraceAsync(traceId, store).ConfigureAwait(false));

app.MapSseEndpoints();

app.MapPost("/api/v1/ingest", async (
    HttpContext context,
    DuckDbStore store,
    ITelemetrySseBroadcaster broadcaster) =>
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

    await store.EnqueueAsync(batch);

    broadcaster.PublishSpans(batch);

    return Results.Accepted();
});

app.MapPost("/v1/traces", async (
    HttpContext context,
    DuckDbStore store,
    ITelemetrySseBroadcaster broadcaster) =>
{
    try
    {
        var otlpData = await context.Request.ReadFromJsonAsync<OtlpExportTraceServiceRequest>(
            QylSerializerContext.Default.OtlpExportTraceServiceRequest);

        if (otlpData?.ResourceSpans is null) return Results.BadRequest(new ErrorResponse("Invalid OTLP format"));

        var spans = OtlpConverter.ConvertJsonToStorageRows(otlpData);
        if (spans.Count is 0) return Results.Accepted();

        var batch = new SpanBatch(spans);

        await store.EnqueueAsync(batch);

        broadcaster.PublishSpans(batch);

        return Results.Accepted();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ErrorResponse("OTLP parse error", ex.Message));
    }
});

app.MapPost("/api/v1/feedback", () => Results.Accepted());
app.MapGet("/api/v1/sessions/{sessionId}/feedback", (string sessionId) =>
    Results.Ok(new { feedback = Array.Empty<object>() }));

app.MapPost("/api/v1/console", (ConsoleIngestBatch batch, FrontendConsole console) =>
{
    foreach (var req in batch.Logs)
        console.Ingest(req);
    return Results.Accepted();
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
        }
    });

app.MapGet("/mcp/manifest", () => Results.Ok(McpServer.GetManifest()));

app.MapPost("/mcp/tools/call", async (McpToolCall call, McpServer mcp, CancellationToken ct) =>
{
    var response = await mcp.HandleToolCallAsync(call, ct).ConfigureAwait(false);
    return response.IsError ? Results.BadRequest(response) : Results.Ok(response);
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }));

app.MapFallback(async context =>
{
    var path = context.Request.Path.Value ?? "/";

    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 404;
        return;
    }

    context.Response.ContentType = "text/html";
    var indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
    if (File.Exists(indexPath))
        await context.Response.SendFileAsync(indexPath).ConfigureAwait(false);
    else

        await context.Response.WriteAsync(GetFallbackHtml()).ConfigureAwait(false);
});

// Note: Kestrel endpoints are configured via ConfigureKestrel above
// No need to set app.Urls when using explicit Kestrel configuration

StartupBanner.Print($"http://localhost:{port}", token, port, grpcPort);

app.Run();

static string GetFallbackHtml() =>
    """
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>qyl. Dashboard</title>
        <style>
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body {
                font-family: system-ui, -apple-system, sans-serif;
                background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
                min-height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
                color: #e0e0e0;
            }
            .container {
                text-align: center;
                padding: 2rem;
                max-width: 500px;
            }
            h1 {
                font-size: 3rem;
                margin-bottom: 1rem;
                background: linear-gradient(90deg, #00d4ff, #7c3aed);
                -webkit-background-clip: text;
                -webkit-text-fill-color: transparent;
            }
            .subtitle { color: #888; margin-bottom: 2rem; }
            .login-form {
                background: rgba(255,255,255,0.05);
                border: 1px solid rgba(255,255,255,0.1);
                border-radius: 12px;
                padding: 2rem;
                margin-bottom: 1.5rem;
            }
            .form-group { margin-bottom: 1rem; text-align: left; }
            label { display: block; margin-bottom: 0.5rem; color: #aaa; font-size: 0.9rem; }
            input {
                width: 100%;
                padding: 0.75rem 1rem;
                border: 1px solid rgba(255,255,255,0.2);
                border-radius: 8px;
                background: rgba(0,0,0,0.3);
                color: #fff;
                font-size: 1rem;
                font-family: monospace;
            }
            input:focus { outline: none; border-color: #00d4ff; }
            button {
                width: 100%;
                padding: 0.75rem;
                border: none;
                border-radius: 8px;
                background: linear-gradient(90deg, #00d4ff, #7c3aed);
                color: #fff;
                font-size: 1rem;
                font-weight: 600;
                cursor: pointer;
                transition: transform 0.2s, box-shadow 0.2s;
            }
            button:hover { transform: translateY(-2px); box-shadow: 0 4px 20px rgba(0,212,255,0.3); }
            .help-link {
                color: #00d4ff;
                text-decoration: none;
                font-size: 0.9rem;
            }
            .help-link:hover { text-decoration: underline; }
            .help-modal {
                display: none;
                position: fixed;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: rgba(0,0,0,0.8);
                align-items: center;
                justify-content: center;
            }
            .help-modal.show { display: flex; }
            .help-content {
                background: #1a1a2e;
                border: 1px solid rgba(255,255,255,0.1);
                border-radius: 12px;
                padding: 2rem;
                max-width: 500px;
                text-align: left;
            }
            .help-content h2 { margin-bottom: 1rem; }
            .help-content pre {
                background: rgba(0,0,0,0.3);
                padding: 1rem;
                border-radius: 8px;
                overflow-x: auto;
                color: #00d4ff;
                margin: 1rem 0;
            }
            .help-content .close {
                float: right;
                background: none;
                border: none;
                color: #888;
                font-size: 1.5rem;
                cursor: pointer;
                width: auto;
                padding: 0;
            }
            .error { color: #ff6b6b; margin-top: 1rem; display: none; }
        </style>
    </head>
    <body>
        <div class="container">
            <h1>qyl.</h1>
            <p class="subtitle">AI Observability Dashboard</p>

            <div class="login-form">
                <form id="loginForm">
                    <div class="form-group">
                        <label for="token">Authentication Token</label>
                        <input type="password" id="token" name="token" placeholder="Enter your token" autocomplete="off">
                    </div>
                    <button type="submit">Login</button>
                    <p class="error" id="error">Invalid token. Please try again.</p>
                </form>
            </div>

            <a href="#" class="help-link" onclick="showHelp()">Where do I find the token?</a>
        </div>

        <div class="help-modal" id="helpModal">
            <div class="help-content">
                <button class="close" onclick="hideHelp()">&times;</button>
                <h2>Finding Your Token</h2>
                <p>The authentication token is displayed in the console when qyl.collector starts.</p>
                <p>Look for the line starting with <code>Login Token:</code> in your terminal output.</p>
                <p>You can also:</p>
                <ul style="margin-left: 1.5rem; margin-top: 0.5rem;">
                    <li>Click the login URL in the console output</li>
                    <li>Set the <code>QYL_TOKEN</code> environment variable</li>
                    <li>Pass <code>--QYL_TOKEN=yourtoken</code> on startup</li>
                </ul>
            </div>
        </div>

        <script>
            document.getElementById('loginForm').addEventListener('submit', async (e) => {
                e.preventDefault();
                const token = document.getElementById('token').value;
                const error = document.getElementById('error');

                try {
                    const res = await fetch('/api/login', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ token })
                    });

                    if (res.ok) {
                        window.location.reload();
                    } else {
                        error.style.display = 'block';
                    }
                } catch (err) {
                    error.style.display = 'block';
                }
            });

            function showHelp() { document.getElementById('helpModal').classList.add('show'); }
            function hideHelp() { document.getElementById('helpModal').classList.remove('show'); }
            document.getElementById('helpModal').addEventListener('click', (e) => {
                if (e.target.id === 'helpModal') hideHelp();
            });

            // Check if already authenticated
            fetch('/api/auth/check')
                .then(r => r.json())
                .then(data => {
                    if (data.authenticated) {
                        document.querySelector('.container').innerHTML = `
                            <h1>qyl.</h1>
                            <p class="subtitle">Dashboard not built yet</p>
                            <p style="margin-top: 2rem;">Run <code>npm run build</code> in <code>src/qyl.dashboard</code></p>
                            <p style="margin-top: 1rem;"><a href="/api/logout" class="help-link" onclick="fetch('/api/logout', {method:'POST'}).then(()=>location.reload());return false;">Logout</a></p>
                        `;
                    }
                });
        </script>
    </body>
    </html>
    """;

namespace qyl.collector
{
    /// <summary>
    ///     Span API endpoints with proper AOT attributes for dynamic OTLP deserialization.
    /// </summary>
    internal static class SpanEndpoints
    {
        [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
            Justification =
                "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification =
                "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
        public static async Task<IResult> GetSessionSpansAsync(string sessionId, DuckDbStore store,
            CancellationToken ct)
        {
            var spans = await store.GetSpansBySessionAsync(sessionId, ct).ConfigureAwait(false);

            // Extract service name from first span's attributes if available
            var serviceName = "unknown";
            if (spans.Count > 0 && spans[0].Attributes is { } attrJson)
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
            return Results.Ok(new SpanListResponseDto { Spans = spanDtos });
        }

        [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
            Justification =
                "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification =
                "OTLP telemetry contains dynamic user-defined attributes that cannot be statically analyzed")]
        public static async Task<IResult> GetTraceAsync(string traceId, DuckDbStore store)
        {
            var spans = await store.GetTraceAsync(traceId).ConfigureAwait(false);
            if (spans.Count is 0) return Results.NotFound();

            var spanDtos = SpanMapper.ToDtos(spans, r => (r.Name.Split(' ').LastOrDefault() ?? "unknown", null));
            var rootSpan = spanDtos.FirstOrDefault(s => s.ParentSpanId is null);

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
    // GenAI Provider Detection - OTel 1.38 gen_ai.provider.name values
    // =============================================================================

    /// <summary>
    ///     Provider identifiers per OTel 1.38 gen_ai.provider.name values.
    ///     Supports host-based provider detection for automatic attribution.
    /// </summary>
    public static class GenAiProviders
    {
        // OTel 1.38 provider name constants
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
    ///     Handles both current (OTel 1.38) and deprecated attribute names.
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

        private static decimal? GetDecimal(IReadOnlyDictionary<string, object?> attrs, string key)
        {
            if (!attrs.TryGetValue(key, out var value) || value is null)
                return null;

            return value switch
            {
                decimal d => d,
                double dbl => (decimal)dbl,
                float f => (decimal)f,
                long l => l,
                int i => i,
                string s when decimal.TryParse(s, out var parsed) => parsed,
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

        private static decimal? GetJsonDecimal(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.TryGetDecimal(out var d) ? d : null,
                JsonValueKind.String when decimal.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null
            };
        }
        // =========================================================================
        // Dictionary-based extraction (IReadOnlyDictionary<string, object?>)
        // =========================================================================

        /// <summary>
        ///     Extracts GenAI fields from a dictionary of attributes.
        /// </summary>
#pragma warning disable CS0618 // Intentional use of deprecated attributes for backward compatibility
        public static GenAiFields Extract(IReadOnlyDictionary<string, object?> attributes)
        {
            Throw.IfNull(attributes);

            var provider = GetString(attributes, GenAiAttributes.ProviderName)
                           ?? GetString(attributes, GenAiAttributes.Deprecated.System);

            var inputTokens = GetLong(attributes, GenAiAttributes.UsageInputTokens)
                              ?? GetLong(attributes, GenAiAttributes.Deprecated.UsagePromptTokens);

            var outputTokens = GetLong(attributes, GenAiAttributes.UsageOutputTokens)
                               ?? GetLong(attributes, GenAiAttributes.Deprecated.UsageCompletionTokens);

            return new GenAiFields
            {
                ProviderName = provider,
                OperationName = GetString(attributes, GenAiAttributes.OperationName),
                RequestModel = GetString(attributes, GenAiAttributes.RequestModel),
                ResponseModel = GetString(attributes, GenAiAttributes.ResponseModel),
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = GetLong(attributes, GenAiAttributes.UsageTotalTokens),
                Temperature = GetDouble(attributes, GenAiAttributes.RequestTemperature),
                MaxTokens = GetLong(attributes, GenAiAttributes.RequestMaxTokens),
                FinishReason = GetString(attributes, GenAiAttributes.ResponseFinishReasons),
                CostUsd = GetDecimal(attributes, QylAttributes.CostUsd),
                SessionId = GetString(attributes, QylAttributes.SessionId)
                            ?? GetString(attributes, GenAiAttributes.ConversationId),
                ToolName = GetString(attributes, GenAiAttributes.ToolName),
                ToolCallId = GetString(attributes, GenAiAttributes.ToolCallId)
            };
        }

        /// <summary>
        ///     Checks if the attributes contain any GenAI-related keys.
        /// </summary>
        public static bool IsGenAiSpan(IReadOnlyDictionary<string, object?> attributes)
        {
            Throw.IfNull(attributes);

            return attributes.ContainsKey(GenAiAttributes.ProviderName) ||
                   attributes.ContainsKey(GenAiAttributes.Deprecated.System) ||
                   attributes.ContainsKey(GenAiAttributes.RequestModel);
        }

        /// <summary>
        ///     Checks if the attributes use deprecated GenAI attribute names.
        /// </summary>
        public static bool UsesDeprecatedAttributes(IReadOnlyDictionary<string, object?> attributes)
        {
            Throw.IfNull(attributes);

            return attributes.ContainsKey(GenAiAttributes.Deprecated.System) ||
                   attributes.ContainsKey(GenAiAttributes.Deprecated.UsagePromptTokens) ||
                   attributes.ContainsKey(GenAiAttributes.Deprecated.UsageCompletionTokens);
        }
#pragma warning restore CS0618

        /// <summary>
        ///     Extracts GenAI fields from a JsonElement.
        /// </summary>
#pragma warning disable CS0618 // Intentional use of deprecated attributes for backward compatibility
        public static GenAiFields Extract(JsonElement attributes)
        {
            var provider = GetJsonString(attributes, GenAiAttributes.ProviderName)
                           ?? GetJsonString(attributes, GenAiAttributes.Deprecated.System);

            var inputTokens = GetJsonLong(attributes, GenAiAttributes.UsageInputTokens)
                              ?? GetJsonLong(attributes, GenAiAttributes.Deprecated.UsagePromptTokens);

            var outputTokens = GetJsonLong(attributes, GenAiAttributes.UsageOutputTokens)
                               ?? GetJsonLong(attributes, GenAiAttributes.Deprecated.UsageCompletionTokens);

            return new GenAiFields
            {
                ProviderName = provider,
                OperationName = GetJsonString(attributes, GenAiAttributes.OperationName),
                RequestModel = GetJsonString(attributes, GenAiAttributes.RequestModel),
                ResponseModel = GetJsonString(attributes, GenAiAttributes.ResponseModel),
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = GetJsonLong(attributes, GenAiAttributes.UsageTotalTokens),
                Temperature = GetJsonDouble(attributes, GenAiAttributes.RequestTemperature),
                MaxTokens = GetJsonLong(attributes, GenAiAttributes.RequestMaxTokens),
                FinishReason = GetJsonString(attributes, GenAiAttributes.ResponseFinishReasons),
                CostUsd = GetJsonDecimal(attributes, QylAttributes.CostUsd),
                SessionId = GetJsonString(attributes, QylAttributes.SessionId)
                            ?? GetJsonString(attributes, GenAiAttributes.ConversationId),
                ToolName = GetJsonString(attributes, GenAiAttributes.ToolName),
                ToolCallId = GetJsonString(attributes, GenAiAttributes.ToolCallId)
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
            attributes.TryGetProperty(GenAiAttributes.ProviderName, out _) ||
            attributes.TryGetProperty(GenAiAttributes.Deprecated.System, out _) ||
            attributes.TryGetProperty(GenAiAttributes.RequestModel, out _);

        /// <summary>
        ///     Checks if JSON attributes use deprecated GenAI attribute names.
        /// </summary>
        public static bool UsesDeprecatedAttributes(string? attributesJson)
        {
            if (string.IsNullOrEmpty(attributesJson))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(attributesJson);
                return UsesDeprecatedAttributes(doc.RootElement);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Checks if JsonElement uses deprecated GenAI attribute names.
        /// </summary>
        public static bool UsesDeprecatedAttributes(JsonElement attributes) =>
            attributes.TryGetProperty(GenAiAttributes.Deprecated.System, out _) ||
            attributes.TryGetProperty(GenAiAttributes.Deprecated.UsagePromptTokens, out _) ||
            attributes.TryGetProperty(GenAiAttributes.Deprecated.UsageCompletionTokens, out _);
#pragma warning restore CS0618
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
        public decimal? CostUsd { get; init; }

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
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(OtlpEventJson[]))]
    public partial class QylSerializerContext : JsonSerializerContext;
}
