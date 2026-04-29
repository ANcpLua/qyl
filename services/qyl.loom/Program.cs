using Microsoft.AspNetCore.Hosting;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.Mcp;
using Qyl.Loom;
using Qyl.Loom.Agents;
using Qyl.Loom.Autofix;
using Qyl.Loom.Autofix.Workflow;
using Qyl.Loom.CodeReview;
using Qyl.Loom.Endpoints;
using Qyl.Loom.Hosting;
using Qyl.Loom.Workflows;
using Qyl.Loom.Workflows.Prompts;

var builder = WebApplication.CreateBuilder(args);

// Railway / PaaS convention: respect $PORT when provided, fall back to the qyl default.
// Matches qyl.collector's CollectorPortOptions and qyl.mcp's QylMcpServiceCollectionExtensions.
// WebApplicationBuilder doesn't expose UseUrls directly; UseSetting("urls", ...) is the
// minimal-API equivalent and runs before Kestrel bind.
if (int.TryParse(builder.Configuration["PORT"], out var port) && port > 0)
{
    builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://0.0.0.0:{port}");
}

builder.AddQylServiceDefaults(options => options.AdditionalActivitySources.Add("Qyl.Loom"));
builder.AddQylLoomDefaults();

// MCP server — tools dispatched by the official MCP SDK; telemetry via the qyl
// instrumentation facade so loom's MCP surface produces the same JSON-RPC envelope
// spans, gen_ai.execute_tool spans, and silent-error capture as qyl.mcp does.
builder.Services.AddSingleton<LoomGodAnalyzerServer>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .UseQylMcpInstrumentation(ActivitySources.McpSource, options => options.Transport = "http")
    .WithTools<LoomGodAnalyzerServer>()
    .WithTools<LoomWorkflowTools>()
    .WithTools<Qyl.Loom.Autofix.Workflow.AutofixContextToolsWrapper>()
    .WithPrompts<CodeReviewPrompt>()
    .WithPrompts<LoomHandoffPrompts>()
    .WithPrompts<LoomAutofixPrompts>()
    .WithPrompts<FixIssuePrompts>()
    .WithPrompts<ReviewBotPrompts>();

var app = builder.Build();
app.MapQylEndpoints();
app.MapQylLoomEndpoints();
app.Run();
