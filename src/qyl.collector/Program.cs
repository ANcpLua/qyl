using System.Security.Cryptography;
using System.Text;
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

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default);
});

int port = builder.Configuration.GetValue("QYL_PORT", 5100);
string token = builder.Configuration["QYL_TOKEN"] ?? TokenGenerator.Generate();
string dataPath = builder.Configuration["QYL_DATA_PATH"] ?? "qyl.duckdb";

builder.Services.AddSingleton(new TokenAuthOptions
{
    Token = token
});
builder.Services.AddSingleton<SseHub>();
builder.Services.AddSingleton<FrontendConsole>();
builder.Services.AddSingleton(_ => new DuckDbStore(dataPath));
builder.Services.AddSingleton<McpServer>();

builder.Services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();

builder.Services.AddSingleton<SessionAggregator>();

WebApplication app = builder.Build();

SseHub sseHub = app.Services.GetRequiredService<SseHub>();
TokenAuthOptions options = app.Services.GetRequiredService<TokenAuthOptions>();

app.UseMiddleware<TokenAuthMiddleware>(options);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/login", (LoginRequest request, HttpContext context) =>
{
    bool isValid = CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(request.Token),
        Encoding.UTF8.GetBytes(token));

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
    return Results.Ok(new
    {
        success = true
    });
});

app.MapGet("/api/auth/check", (HttpContext context) =>
{
    string? cookieToken = context.Request.Cookies["qyl_token"];
    bool isValid = !string.IsNullOrEmpty(cookieToken) &&
                   CryptographicOperations.FixedTimeEquals(
                       Encoding.UTF8.GetBytes(cookieToken),
                       Encoding.UTF8.GetBytes(token));

    return Results.Ok(new
    {
        authenticated = isValid
    });
});

app.MapGet("/api/v1/sessions", (SessionAggregator aggregator, int? limit, string? serviceName) =>
{
    IReadOnlyList<SessionSummary> sessions = serviceName is not null
        ? aggregator.GetSessionsByService(serviceName, limit ?? 100)
        : aggregator.GetSessions(limit ?? 100);

    SessionListResponseDto response = SessionMapper.ToListResponse(sessions, sessions.Count, false);
    return Results.Ok(response);
});

app.MapGet("/api/v1/sessions/{sessionId}", (string sessionId, SessionAggregator aggregator) =>
{
    SessionSummary? session = aggregator.GetSession(sessionId);
    if (session is null) return Results.NotFound();

    return Results.Ok(SessionMapper.ToDto(session));
});

app.MapGet("/api/v1/sessions/{sessionId}/spans",
    async (string sessionId, DuckDbStore store, SessionAggregator aggregator) =>
    {
        IReadOnlyList<SpanRecord> spans = await store.GetSpansBySessionAsync(sessionId).ConfigureAwait(false);

        SessionSummary? session = aggregator.GetSession(sessionId);
        string serviceName = session?.Services.Count > 0 ? session.Services[0] : "unknown";

        var spanDtos = spans.Select(s => SpanMapper.ToDto(s, serviceName)).ToList();
        return Results.Ok(new SpanListResponseDto
        {
            Spans = spanDtos
        });
    });

app.MapGet("/api/v1/traces/{traceId}", async (string traceId, DuckDbStore store) =>
{
    IReadOnlyList<SpanRecord> spans = await store.GetTraceAsync(traceId).ConfigureAwait(false);
    if (spans.Count == 0) return Results.NotFound();

    List<SpanDto> spanDtos = SpanMapper.ToDtos(spans, r => (r.Name.Split(' ').LastOrDefault() ?? "unknown", null));
    SpanDto? rootSpan = spanDtos.FirstOrDefault(s => s.ParentSpanId is null);

    return Results.Ok(new TraceResponseDto
    {
        TraceId = traceId,
        Spans = spanDtos,
        RootSpan = rootSpan,
        DurationMs = rootSpan?.DurationMs,
        Status = rootSpan?.Status
    });
});

app.MapSseEndpoints();

app.MapPost("/api/v1/ingest", async (
    HttpContext context,
    DuckDbStore store,
    SessionAggregator aggregator,
    ITelemetrySseBroadcaster broadcaster) =>
{
    SpanBatch? batch;
    try
    {
        batch = await context.Request.ReadFromJsonAsync<SpanBatch>(
            QylSerializerContext.Default.SpanBatch);

        if (batch is null || batch.Spans.Count == 0)
            return Results.BadRequest(new ErrorResponse("Empty or invalid batch"));
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new ErrorResponse("Invalid JSON", ex.Message));
    }

    await store.EnqueueAsync(batch);

    foreach (SpanRecord span in batch.Spans) aggregator.TrackSpan(span);

    broadcaster.PublishSpans(batch);

    return Results.Accepted();
});

app.MapPost("/v1/traces", async (
    HttpContext context,
    DuckDbStore store,
    SessionAggregator aggregator,
    ITelemetrySseBroadcaster broadcaster) =>
{
    try
    {
        OtlpExportTraceServiceRequest? otlpData = await context.Request.ReadFromJsonAsync<OtlpExportTraceServiceRequest>(
            QylSerializerContext.Default.OtlpExportTraceServiceRequest);

        if (otlpData?.ResourceSpans is null) return Results.BadRequest(new ErrorResponse("Invalid OTLP format"));

        List<SpanRecord> spans = ConvertOtlpToSpanRecords(otlpData);
        if (spans.Count == 0) return Results.Accepted();

        var batch = new SpanBatch(spans);

        await store.EnqueueAsync(batch);

        foreach (SpanRecord span in spans) aggregator.TrackSpan(span);

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
    Results.Ok(new
    {
        feedback = Array.Empty<object>()
    }));

app.MapPost("/api/v1/console", (ConsoleIngestBatch batch, FrontendConsole console) =>
{
    foreach (ConsoleIngestRequest req in batch.Logs)
        console.Ingest(req);
    return Results.Accepted();
});

app.MapGet("/api/v1/console", (FrontendConsole console, string? session, string? level, int? limit) =>
{
    ConsoleLevel? minLevel = level?.ToLowerInvariant() switch
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
        string connectionId = Guid.NewGuid().ToString("N")[..8];

        using IDisposable _ = console.Subscribe(connectionId, channel);

        await ctx.Response.WriteAsync($"event: connected\ndata: {{\"id\":\"{connectionId}\"}}\n\n", ct)
            .ConfigureAwait(false);
        await ctx.Response.Body.FlushAsync(ct).ConfigureAwait(false);

        try
        {
            await foreach (ConsoleLogEntry entry in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (session != null && entry.Session != session) continue;
                string json = JsonSerializer.Serialize(entry, QylSerializerContext.Default.ConsoleLogEntry);
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
    McpResponse response = await mcp.HandleToolCallAsync(call, ct).ConfigureAwait(false);
    return response.IsError ? Results.BadRequest(response) : Results.Ok(response);
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy"
}));
app.MapGet("/ready", () => Results.Ok(new
{
    status = "ready"
}));

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapFallback(async context =>
{
    string path = context.Request.Path.Value ?? "/";

    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 404;
        return;
    }

    context.Response.ContentType = "text/html";
    string indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
    if (File.Exists(indexPath))
        await context.Response.SendFileAsync(indexPath).ConfigureAwait(false);
    else

        await context.Response.WriteAsync(GetFallbackHtml(token)).ConfigureAwait(false);
});

string urls = $"http://0.0.0.0:{port}";
app.Urls.Add(urls);

StartupBanner.Print($"http://localhost:{port}", token, port);

app.Run();

static List<SpanRecord> ConvertOtlpToSpanRecords(OtlpExportTraceServiceRequest otlp)
{
    var spans = new List<SpanRecord>();

    foreach (OtlpResourceSpans resourceSpan in otlp.ResourceSpans ?? [])
    {
        string serviceName = resourceSpan.Resource?.Attributes?
            .FirstOrDefault(a => a.Key == "service.name")?.Value?.StringValue ?? "unknown";

        foreach (OtlpScopeSpans scopeSpan in resourceSpan.ScopeSpans ?? [])
        {
            foreach (OtlpSpan span in scopeSpan.Spans ?? [])
            {
                var attributes = new Dictionary<string, string>
                {
                    ["service.name"] = serviceName
                };

                foreach (OtlpKeyValue attr in span.Attributes ?? [])
                {
                    if (attr.Key is not null)
                        attributes[attr.Key] = attr.Value?.StringValue
                                               ?? attr.Value?.IntValue?.ToString()
                                               ?? attr.Value?.DoubleValue?.ToString()
                                               ?? attr.Value?.BoolValue?.ToString()?.ToLowerInvariant()
                                               ?? "";
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
                    Attributes =
                        JsonSerializer.Serialize(attributes, QylSerializerContext.Default.DictionaryStringString),
                    Events = null,

                    ProviderName = attributes.GetValueOrDefault("gen_ai.system"),
                    RequestModel = attributes.GetValueOrDefault("gen_ai.request.model"),
                    TokensIn = int.TryParse(attributes.GetValueOrDefault("gen_ai.usage.input_tokens"), out int tin)
                        ? tin
                        : null,
                    TokensOut = int.TryParse(attributes.GetValueOrDefault("gen_ai.usage.output_tokens"), out int tout)
                        ? tout
                        : null,
                    CostUsd = null
                });
            }
        }
    }

    return spans;
}

static string GetFallbackHtml(string token) =>
    $$"""
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
