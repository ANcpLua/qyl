// qyl.collector - AI observability backend
// Native AOT, embedded DuckDB, real-time streaming

using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using qyl.collector;
using qyl.collector.Auth;
using qyl.collector.ConsoleBridge;
using qyl.collector.Contracts;
using qyl.collector.Ingestion;
using qyl.collector.Mapping;
using qyl.collector.Mcp;
using qyl.collector.Query;
using qyl.collector.Realtime;
using qyl.collector.Storage;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure JSON serialization for AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default);
});

// Configuration
var port = builder.Configuration.GetValue("QYL_PORT", 5100);
var token = builder.Configuration["QYL_TOKEN"] ?? TokenGenerator.Generate();
var dataPath = builder.Configuration["QYL_DATA_PATH"] ?? "qyl.duckdb";

// Services
builder.Services.AddSingleton(new TokenAuthOptions { Token = token });
builder.Services.AddSingleton<SseHub>();
builder.Services.AddSingleton<FrontendConsole>();
builder.Services.AddSingleton(_ => new DuckDbStore(dataPath));
builder.Services.AddSingleton<McpServer>();

// SSE streaming (.NET 10 native)
builder.Services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();

// Session aggregation
builder.Services.AddSingleton<SessionAggregator>();

var app = builder.Build();

// Get services
var sseHub = app.Services.GetRequiredService<SseHub>();
var options = app.Services.GetRequiredService<TokenAuthOptions>();

// Middleware
app.UseMiddleware<TokenAuthMiddleware>(options);

// Static files (dashboard)
app.UseDefaultFiles();
app.UseStaticFiles();

// ============================================================================
// AUTH ENDPOINTS
// ============================================================================

app.MapPost("/api/login", (LoginRequest request, HttpContext context) =>
{
    var isValid = CryptographicOperations.FixedTimeEquals(
        System.Text.Encoding.UTF8.GetBytes(request.Token),
        System.Text.Encoding.UTF8.GetBytes(token));

    if (isValid)
    {
        context.Response.Cookies.Append("qyl_token", request.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(3),
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
            System.Text.Encoding.UTF8.GetBytes(cookieToken),
            System.Text.Encoding.UTF8.GetBytes(token));

    return Results.Ok(new { authenticated = isValid });
});

// ============================================================================
// QUERY API
// ============================================================================

app.MapGet("/api/v1/sessions", (SessionAggregator aggregator, int? limit, string? serviceName) =>
{
    var sessions = serviceName is not null
        ? aggregator.GetSessionsByService(serviceName, limit ?? 100)
        : aggregator.GetSessions(limit ?? 100);

    // Map to DTOs aligned with OpenAPI spec
    var response = SessionMapper.ToListResponse(sessions, sessions.Count, hasMore: false);
    return Results.Ok(response);
});

app.MapGet("/api/v1/sessions/{sessionId}", (string sessionId, SessionAggregator aggregator) =>
{
    var session = aggregator.GetSession(sessionId);
    if (session is null) return Results.NotFound();

    // Map to DTO aligned with OpenAPI spec
    return Results.Ok(SessionMapper.ToDto(session));
});

app.MapGet("/api/v1/sessions/{sessionId}/spans", async (string sessionId, DuckDbStore store, SessionAggregator aggregator) =>
{
    var spans = await store.GetSpansBySessionAsync(sessionId).ConfigureAwait(false);

    // Get session info for service name lookup
    var session = aggregator.GetSession(sessionId);
    var serviceName = session?.Services.Count > 0 ? session.Services[0] : "unknown";

    // Map to DTOs aligned with OpenAPI spec
    var spanDtos = spans.Select(s => SpanMapper.ToDto(s, serviceName)).ToList();
    return Results.Ok(new SpanListResponseDto { Spans = spanDtos });
});

app.MapGet("/api/v1/traces/{traceId}", async (string traceId, DuckDbStore store) =>
{
    var spans = await store.GetTraceAsync(traceId).ConfigureAwait(false);
    if (spans.Count == 0) return Results.NotFound();

    // Map to DTOs aligned with OpenAPI spec
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
});

// ============================================================================
// REALTIME API (SSE) - .NET 10 Native
// ============================================================================

app.MapSseEndpoints();

// ============================================================================
// INGESTION API - Wires up: Ingestion → DuckDB Storage + Session Aggregation + SSE
// ============================================================================

app.MapPost("/api/v1/ingest", async (
    HttpContext context,
    DuckDbStore store,
    SessionAggregator aggregator,
    ITelemetrySseBroadcaster broadcaster) =>
{
    // Parse incoming spans (qyl native format)
    SpanBatch? batch;
    try
    {
        batch = await context.Request.ReadFromJsonAsync<SpanBatch>(
            QylSerializerContext.Default.SpanBatch);

        if (batch is null || batch.Spans.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("Empty or invalid batch"));
        }
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new ErrorResponse("Invalid JSON", ex.Message));
    }

    // 1. Store to DuckDB (async, non-blocking via channel)
    await store.EnqueueAsync(batch);

    // 2. Track for session aggregation (in-memory)
    foreach (var span in batch.Spans)
    {
        aggregator.TrackSpan(span);
    }

    // 3. Broadcast to SSE clients
    broadcaster.PublishSpans(batch);

    return Results.Accepted();
});

// OTLP compatibility shim - converts OTLP JSON format to internal format
app.MapPost("/v1/traces", async (
    HttpContext context,
    DuckDbStore store,
    SessionAggregator aggregator,
    ITelemetrySseBroadcaster broadcaster) =>
{
    try
    {
        // Parse OTLP JSON format
        var otlpData = await context.Request.ReadFromJsonAsync<OtlpExportTraceServiceRequest>(
            QylSerializerContext.Default.OtlpExportTraceServiceRequest);

        if (otlpData?.ResourceSpans is null)
        {
            return Results.BadRequest(new ErrorResponse("Invalid OTLP format"));
        }

        var spans = ConvertOtlpToSpanRecords(otlpData);
        if (spans.Count == 0)
        {
            return Results.Accepted(); // Nothing to process
        }

        var batch = new SpanBatch(spans);

        await store.EnqueueAsync(batch);

        foreach (var span in spans)
        {
            aggregator.TrackSpan(span);
        }

        broadcaster.PublishSpans(batch);

        return Results.Accepted();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ErrorResponse("OTLP parse error", ex.Message));
    }
});

// ============================================================================
// FEEDBACK API
// ============================================================================

app.MapPost("/api/v1/feedback", () => Results.Accepted());
app.MapGet("/api/v1/sessions/{sessionId}/feedback", (string sessionId) =>
    Results.Ok(new { feedback = Array.Empty<object>() }));

// ============================================================================
// CONSOLE BRIDGE API - Frontend logs for agent debugging
// ============================================================================

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

app.MapGet("/api/v1/console/live", async (HttpContext ctx, FrontendConsole console, string? session, CancellationToken ct) =>
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

    await ctx.Response.WriteAsync($"event: connected\ndata: {{\"id\":\"{connectionId}\"}}\n\n", ct).ConfigureAwait(false);
    await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);

    try
    {
        await foreach (var entry in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (session != null && entry.Session != session) continue;
            var json = JsonSerializer.Serialize(entry, QylSerializerContext.Default.ConsoleLogEntry);
            await ctx.Response.WriteAsync($"event: console\ndata: {json}\n\n", ct).ConfigureAwait(false);
            await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);
        }
    }
    catch (OperationCanceledException) { }
});

// ============================================================================
// MCP SERVER ENDPOINTS
// ============================================================================

app.MapGet("/mcp/manifest", () => Results.Ok(McpServer.GetManifest()));

app.MapPost("/mcp/tools/call", async (McpToolCall call, McpServer mcp, CancellationToken ct) =>
{
    var response = await mcp.HandleToolCallAsync(call, ct).ConfigureAwait(false);
    return response.IsError ? Results.BadRequest(response) : Results.Ok(response);
});

// ============================================================================
// HEALTH ENDPOINTS
// ============================================================================

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }));

// ============================================================================
// ROOT & FALLBACK
// ============================================================================

app.MapGet("/", () => Results.Redirect("/index.html"));

// SPA fallback - serve index.html for client-side routing
app.MapFallback(async context =>
{
    var path = context.Request.Path.Value ?? "/";

    // Don't serve index.html for API routes
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 404;
        return;
    }

    context.Response.ContentType = "text/html";
    var indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
    if (File.Exists(indexPath))
    {
        await context.Response.SendFileAsync(indexPath).ConfigureAwait(false);
    }
    else
    {
        // Fallback HTML if no dashboard built
        await context.Response.WriteAsync(GetFallbackHtml(token)).ConfigureAwait(false);
    }
});

// ============================================================================
// STARTUP
// ============================================================================

var urls = $"http://0.0.0.0:{port}";
app.Urls.Add(urls);

StartupBanner.Print($"http://localhost:{port}", token, port);

app.Run();

// ============================================================================
// HELPERS
// ============================================================================

// OTLP conversion helper
static List<SpanRecord> ConvertOtlpToSpanRecords(OtlpExportTraceServiceRequest otlp)
{
    var spans = new List<SpanRecord>();

    foreach (var resourceSpan in otlp.ResourceSpans ?? [])
    {
        var serviceName = resourceSpan.Resource?.Attributes?
            .FirstOrDefault(a => a.Key == "service.name")?.Value?.StringValue ?? "unknown";

        foreach (var scopeSpan in resourceSpan.ScopeSpans ?? [])
        {
            foreach (var span in scopeSpan.Spans ?? [])
            {
                var attributes = new Dictionary<string, string> { ["service.name"] = serviceName };

                // Copy span attributes
                foreach (var attr in span.Attributes ?? [])
                {
                    if (attr.Key is not null)
                    {
                        attributes[attr.Key] = attr.Value?.StringValue
                            ?? attr.Value?.IntValue?.ToString()
                            ?? attr.Value?.DoubleValue?.ToString()
                            ?? attr.Value?.BoolValue?.ToString()?.ToLowerInvariant()
                            ?? "";
                    }
                }

                spans.Add(new SpanRecord
                {
                    TraceId = span.TraceId ?? "",
                    SpanId = span.SpanId ?? "",
                    ParentSpanId = span.ParentSpanId,
                    SessionId = attributes.GetValueOrDefault("session.id"),
                    Name = span.Name ?? "unknown",
                    Kind = span.Kind?.ToString(),
                    StartTime = DateTime.UnixEpoch.AddTicks(span.StartTimeUnixNano / 100),
                    EndTime = DateTime.UnixEpoch.AddTicks(span.EndTimeUnixNano / 100),
                    StatusCode = span.Status?.Code,
                    StatusMessage = span.Status?.Message,
                    Attributes = JsonSerializer.Serialize(attributes, QylSerializerContext.Default.DictionaryStringString),
                    Events = null, // TODO: Convert span events
                    // GenAI fields extracted from attributes
                    ProviderName = attributes.GetValueOrDefault("gen_ai.system"),
                    RequestModel = attributes.GetValueOrDefault("gen_ai.request.model"),
                    TokensIn = int.TryParse(attributes.GetValueOrDefault("gen_ai.usage.input_tokens"), out var tin) ? tin : null,
                    TokensOut = int.TryParse(attributes.GetValueOrDefault("gen_ai.usage.output_tokens"), out var tout) ? tout : null,
                    CostUsd = null
                });
            }
        }
    }

    return spans;
}

static string GetFallbackHtml(string token) => $$"""
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
            <p>The authentication token is displayed in the console when qyl.collector starts:</p>
            <pre>Login Token:
{{token}}</pre>
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
