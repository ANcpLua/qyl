// =============================================================================
// qyl.demo - The Magic Experience
//
// This is all you need. Everything else is automatic:
// - OpenTelemetry tracing, metrics, and logging
// - GenAI instrumentation (OTel 1.39 semconv)
// - Health checks (/health, /alive)
// - OpenAPI documentation
// - HTTP client resilience
// =============================================================================

using qyl.copilot;
using Qyl.ServiceDefaults.AspNetCore.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// ONE LINE: Full observability, health checks, OpenAPI, etc.
// ============================================================
builder.UseQylConventions();

// ============================================================
// ONE LINE: GitHub Copilot integration with auto-instrumentation
// ============================================================
builder.Services.AddQylCopilot();

var app = builder.Build();

// ============================================================
// ONE LINE: Map health endpoints, OpenAPI, etc.
// ============================================================
app.MapQylDefaultEndpoints();

// ============================================================
// Your API - GenAI calls are auto-instrumented at compile time
// ============================================================

app.MapGet("/", () => "qyl.demo - AI Observability Made Simple");

app.MapPost("/chat", async (ChatRequest request, CopilotAdapterFactory factory, CancellationToken ct) =>
{
    // This is it. The generator auto-instruments AIAgent.RunAsync() calls.
    // You get gen_ai.* spans with full OTel 1.39 semantic conventions.
    // No .UseOpenTelemetry(), no manual spans, no configuration.

    var adapter = await factory.GetAdapterAsync(ct);
    var response = await adapter.ChatCompleteAsync(request.Message, ct: ct);
    return new ChatResponse(response);
});

app.MapPost("/chat/stream", async (ChatRequest request, CopilotAdapterFactory factory, HttpContext http, CancellationToken ct) =>
{
    // Streaming also just works
    http.Response.ContentType = "text/event-stream";

    var adapter = await factory.GetAdapterAsync(ct);
    await foreach (var update in adapter.ChatAsync(request.Message, ct: ct))
    {
        await http.Response.WriteAsync($"data: {update.Content}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
});

// Workflow execution endpoint
app.MapPost("/workflow/{name}", async (string name, WorkflowRequest request, WorkflowEngineFactory factory, HttpContext http, CancellationToken ct) =>
{
    // Streaming workflow execution with auto-instrumentation
    http.Response.ContentType = "text/event-stream";

    var engine = await factory.GetEngineAsync(ct);

    await foreach (var update in engine.ExecuteAsync(name, request.Parameters, request.Context, ct))
    {
        await http.Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(update)}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
});

app.Run();

// ============================================================
// DTOs
// ============================================================
sealed record ChatRequest(string Message);
sealed record ChatResponse(string Response);
sealed record WorkflowRequest(string? Context, Dictionary<string, string>? Parameters);
