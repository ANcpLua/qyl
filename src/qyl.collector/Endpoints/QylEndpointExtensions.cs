// =============================================================================
// qyl Endpoint Extensions
// Organizes all API endpoints into logical groups with proper routing
// Uses [StringSyntax("Route")] for IDE route validation
// =============================================================================

using qyl.collector.Auth;
using qyl.collector.Mapping;
using qyl.collector.Mcp;

namespace qyl.collector.Endpoints;

/// <summary>
///     Extension methods for mapping qyl API endpoints.
///     Organizes endpoints into logical groups: Auth, Sessions, Traces, Ingestion, Console, MCP, Health.
/// </summary>
public static class QylEndpointExtensions
{
    /// <summary>
    ///     Maps all qyl API endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapQylEndpoints(
        this IEndpointRouteBuilder endpoints,
        string token)
    {
        Throw.IfNull(endpoints);
        Throw.IfNullOrWhitespace(token);

        endpoints.MapAuthEndpoints(token);
        endpoints.MapSessionEndpoints();
        endpoints.MapTraceEndpoints();
        endpoints.MapIngestionEndpoints();
        endpoints.MapConsoleEndpoints();
        endpoints.MapMcpEndpoints();
        endpoints.MapHealthEndpoints();
        endpoints.MapFeedbackEndpoints();
        endpoints.MapSseEndpoints();

        return endpoints;
    }

    /// <summary>
    ///     Maps authentication endpoints: login, logout, auth check.
    /// </summary>
    public static RouteGroupBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints,
        string token,
        [StringSyntax("Route")] string pattern = "/api")
    {
        Throw.IfNull(endpoints);
        Throw.IfNullOrWhitespace(pattern);

        var group = endpoints.MapGroup(pattern.TrimEnd('/'))
            .WithTags("Authentication")
            .WithDescription("Authentication and session management");

        group.MapPost("/login", (LoginRequest request, HttpContext context) =>
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
            })
            .WithName("Login")
            .WithDescription("Authenticate with token and receive session cookie");

        group.MapPost("/logout", static (HttpContext context) =>
            {
                context.Response.Cookies.Delete("qyl_token");
                return Results.Ok(new { success = true });
            })
            .WithName("Logout")
            .WithDescription("Clear authentication session");

        group.MapGet("/auth/check", (HttpContext context) =>
            {
                var cookieToken = context.Request.Cookies["qyl_token"];
                var isValid = !string.IsNullOrEmpty(cookieToken) &&
                              CryptographicOperations.FixedTimeEquals(
                                  Encoding.UTF8.GetBytes(cookieToken),
                                  Encoding.UTF8.GetBytes(token));

                return Results.Ok(new { authenticated = isValid });
            })
            .WithName("CheckAuth")
            .WithDescription("Check if current session is authenticated");

        return group;
    }

    /// <summary>
    ///     Maps session query endpoints.
    /// </summary>
    public static RouteGroupBuilder MapSessionEndpoints(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = "/api/v1/sessions")
    {
        Throw.IfNull(endpoints);

        var group = endpoints.MapGroup(pattern.TrimEnd('/'))
            .WithTags("Sessions")
            .WithDescription("Session listing and details");

        group.MapGet("", static async (
                SessionQueryService queryService,
                int? limit,
                string? serviceName,
                CancellationToken ct) =>
            {
                var sessions = await queryService.GetSessionsAsync(limit ?? 100, 0, serviceName, ct: ct)
                    .ConfigureAwait(false);
                var response = SessionMapper.ToListResponse(sessions, sessions.Count, false);
                return Results.Ok(response);
            })
            .WithName("ListSessions")
            .WithDescription("List all sessions with optional filtering")
            .Produces<SessionListResponseDto>();

        group.MapGet("/{sessionId}", static async (
                string sessionId,
                SessionQueryService queryService,
                CancellationToken ct) =>
            {
                var session = await queryService.GetSessionAsync(sessionId, ct).ConfigureAwait(false);
                return session is null ? Results.NotFound() : Results.Ok(SessionMapper.ToDto(session));
            })
            .WithName("GetSession")
            .WithDescription("Get session details by ID")
            .Produces<SessionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{sessionId}/spans", static (
                string sessionId,
                DuckDbStore store,
                CancellationToken ct) => SpanEndpoints.GetSessionSpansAsync(sessionId, store, ct))
            .WithName("GetSessionSpans")
            .WithDescription("Get all spans for a session")
            .Produces<SpanListResponseDto>();

        return group;
    }

    /// <summary>
    ///     Maps trace query endpoints.
    /// </summary>
    public static RouteGroupBuilder MapTraceEndpoints(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = "/api/v1/traces")
    {
        Throw.IfNull(endpoints);

        var group = endpoints.MapGroup(pattern.TrimEnd('/'))
            .WithTags("Traces")
            .WithDescription("Distributed trace queries");

        group.MapGet("/{traceId}", static async (string traceId, DuckDbStore store) =>
                await SpanEndpoints.GetTraceAsync(traceId, store).ConfigureAwait(false))
            .WithName("GetTrace")
            .WithDescription("Get trace tree by trace ID")
            .Produces<TraceResponseDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    ///     Maps telemetry ingestion endpoints (OTLP HTTP).
    /// </summary>
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        Throw.IfNull(endpoints);

        // Internal qyl format ingestion
        endpoints.MapPost("/api/v1/ingest", static async (
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
            })
            .WithTags("Ingestion")
            .WithName("IngestQylFormat")
            .WithDescription("Ingest spans in qyl internal format");

        // OTLP HTTP/JSON ingestion
        endpoints.MapPost("/v1/traces", static async (
                HttpContext context,
                DuckDbStore store,
                ITelemetrySseBroadcaster broadcaster) =>
            {
                try
                {
                    var otlpData = await context.Request.ReadFromJsonAsync<OtlpExportTraceServiceRequest>(
                        QylSerializerContext.Default.OtlpExportTraceServiceRequest);

                    if (otlpData?.ResourceSpans is null)
                        return Results.BadRequest(new ErrorResponse("Invalid OTLP format"));

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
            })
            .WithTags("Ingestion")
            .WithName("IngestOtlpHttp")
            .WithDescription("Ingest spans via OTLP HTTP/JSON");

        return endpoints;
    }

    /// <summary>
    ///     Maps console/logging endpoints.
    /// </summary>
    public static RouteGroupBuilder MapConsoleEndpoints(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = "/api/v1/console")
    {
        Throw.IfNull(endpoints);

        var group = endpoints.MapGroup(pattern.TrimEnd('/'))
            .WithTags("Console")
            .WithDescription("Frontend console log ingestion and queries");

        group.MapPost("", (ConsoleIngestBatch batch, FrontendConsole console) =>
            {
                foreach (var req in batch.Logs)
                    console.Ingest(req);
                return Results.Accepted();
            })
            .WithName("IngestConsoleLogs")
            .WithDescription("Ingest frontend console logs");

        group.MapGet("", (FrontendConsole console, string? session, string? level, int? limit) =>
            {
                var minLevel = level?.ToLowerInvariant() switch
                {
                    "warn" => ConsoleLevel.Warn,
                    "error" => ConsoleLevel.Error,
                    _ => (ConsoleLevel?)null
                };
                return Results.Ok(console.Query(minLevel, session, null, limit ?? 50));
            })
            .WithName("QueryConsoleLogs")
            .WithDescription("Query console logs with filtering")
            .Produces<ConsoleLogEntry[]>();

        group.MapGet("/errors", (FrontendConsole console, int? limit) =>
                Results.Ok(console.Errors(limit ?? 20)))
            .WithName("GetConsoleErrors")
            .WithDescription("Get recent console errors")
            .Produces<ConsoleLogEntry[]>();

        group.MapGet("/live", async (
                HttpContext ctx,
                FrontendConsole console,
                string? session,
                CancellationToken ct) =>
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
                    // Client disconnected - normal
                }
            })
            .WithName("StreamConsoleLogs")
            .WithDescription("Live stream console logs via SSE");

        return group;
    }

    /// <summary>
    ///     Maps MCP server endpoints.
    /// </summary>
    public static RouteGroupBuilder MapMcpEndpoints(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = "/mcp")
    {
        Throw.IfNull(endpoints);

        var group = endpoints.MapGroup(pattern.TrimEnd('/'))
            .WithTags("MCP")
            .WithDescription("Model Context Protocol server endpoints");

        group.MapGet("/manifest", () => Results.Ok(McpServer.GetManifest()))
            .WithName("GetMcpManifest")
            .WithDescription("Get MCP server manifest with available tools");

        group.MapPost("/tools/call", async (McpToolCall call, McpServer mcp, CancellationToken ct) =>
            {
                var response = await mcp.HandleToolCallAsync(call, ct).ConfigureAwait(false);
                return response.IsError ? Results.BadRequest(response) : Results.Ok(response);
            })
            .WithName("CallMcpTool")
            .WithDescription("Execute an MCP tool");

        return group;
    }

    /// <summary>
    ///     Maps health check endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        Throw.IfNull(endpoints);

        endpoints.MapGet("/health", () => Results.Ok(new HealthResponse("healthy")))
            .WithTags("Health")
            .WithName("HealthCheck")
            .WithDescription("Liveness probe");

        endpoints.MapGet("/ready", () => Results.Ok(new HealthResponse("ready")))
            .WithTags("Health")
            .WithName("ReadinessCheck")
            .WithDescription("Readiness probe");

        return endpoints;
    }

    /// <summary>
    ///     Maps feedback endpoints (placeholder).
    /// </summary>
    public static RouteGroupBuilder MapFeedbackEndpoints(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = "/api/v1")
    {
        Throw.IfNull(endpoints);

        var group = endpoints.MapGroup(pattern.TrimEnd('/'))
            .WithTags("Feedback")
            .WithDescription("User feedback collection (placeholder)");

        group.MapPost("/feedback", () => Results.Accepted())
            .WithName("SubmitFeedback")
            .WithDescription("Submit feedback for a session");

        group.MapGet("/sessions/{sessionId}/feedback", (string sessionId) =>
                Results.Ok(new FeedbackResponse([])))
            .WithName("GetSessionFeedback")
            .WithDescription("Get feedback for a session");

        return group;
    }
}
