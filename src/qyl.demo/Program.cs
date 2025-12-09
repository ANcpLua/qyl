// qyl.demo - Modern Agent Demo with OpenTelemetry v1.38 Observability
// Demonstrates: workflows, tools, DevUI, and GenAI semantic conventions

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenTelemetry;
using OpenTelemetry.Trace;
using qyl.agents.telemetry;
using qyl.sdk.aspnetcore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// OpenTelemetry v1.38 GenAI Observability Setup
// ============================================================================

// Add qyl observability services (sets OTEL_SEMCONV_STABILITY_OPT_IN=gen_ai_latest_experimental)
builder.Services.AddQylAgentObservability();

// Configure OpenTelemetry tracing with console export for demo visibility
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(GenAiSemanticConventions.SourceName)
        .AddConsoleExporter());

// ============================================================================
// Chat Client Setup (Ollama with llama3.2)
// ============================================================================

// You will need to have Ollama running locally with the llama3.2 model installed
// Visit https://ollama.com for installation instructions
var chatClient = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");
builder.Services.AddChatClient(chatClient);

// ============================================================================
// Agent Registration with OpenTelemetry Instrumentation
// ============================================================================

// Writer agent - generates short stories
builder.AddAIAgent("writer", (sp, key) =>
{
    var client = sp.GetRequiredService<IChatClient>();
    return client.CreateAIAgent(
            name: key,
            instructions: "You write short stories (300 words or less) about the specified topic.")
        .AsBuilder()
        .UseQylOpenTelemetry()  // v1.38 GenAI semantic conventions
        .Build();
});

// Editor agent - refines and formats stories
builder.AddAIAgent("editor", (sp, key) =>
{
    var client = sp.GetRequiredService<IChatClient>();
    return client.CreateAIAgent(
            name: key,
            instructions: """
                You edit short stories to improve grammar and style, ensuring the stories
                are less than 300 words. Once finished editing, you select a title and
                format the story for publishing using the FormatStory tool.
                """,
            tools: [AIFunctionFactory.Create(FormatStory)])
        .AsBuilder()
        .UseQylOpenTelemetry()  // v1.38 GenAI semantic conventions
        .Build();
});

// Publisher workflow - chains writer -> editor
builder.AddWorkflow("publisher", (sp, key) =>
{
    var writer = sp.GetRequiredKeyedService<AIAgent>("writer");
    var editor = sp.GetRequiredKeyedService<AIAgent>("editor");

    return AgentWorkflowBuilder.BuildSequential(
        workflowName: key,
        writer,
        editor);
}).AddAsAIAgent();

// ============================================================================
// OpenAI-compatible API endpoints (required for DevUI)
// ============================================================================

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

var app = builder.Build();
app.UseHttpsRedirection();

// Map endpoints for OpenAI responses and conversations
app.MapOpenAIResponses();
app.MapOpenAIConversations();

if (builder.Environment.IsDevelopment())
{
    // Map DevUI endpoint to /devui for interactive testing
    app.MapDevUI();
}

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", schema = "v1.38.0" }));

await app.RunAsync();
return;

// ============================================================================
// Tool Definitions
// ============================================================================

[Description("Formats the story for publication, revealing its title.")]
static string FormatStory(string title, string story)
{
    // Record tool execution as an Activity with v1.38 attributes
    using var activity = new ActivitySource(GenAiSemanticConventions.SourceName)
        .StartActivity(GenAiSemanticConventions.ExecuteTool);

    activity?.SetTag(GenAiSemanticConventions.Operation.Name, GenAiSemanticConventions.Operation.Values.ExecuteTool);
    activity?.SetTag(GenAiSemanticConventions.Tool.Name, nameof(FormatStory));
    activity?.SetTag(GenAiSemanticConventions.Tool.Type, "function");

    var result = $"""
        **Title**: {title}

        {story}
        """;

    activity?.SetTag(GenAiSemanticConventions.Tool.Call.Result, result);

    return result;
}
