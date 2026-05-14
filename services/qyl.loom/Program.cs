using ANcpLua.Agents.Hosting.BitNet;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.Mcp;
using Qyl.Loom;
using Qyl.Loom.Agents;
using Qyl.Loom.Autofix;
using Qyl.Loom.Autofix.Workflow;
using Qyl.Loom.Clients;
using Qyl.Loom.CodeReview;
using Qyl.Loom.Endpoints;
using Qyl.Loom.Hosting;
using Qyl.Loom.Workflows;
using Qyl.Loom.Workflows.Prompts;

var builder = WebApplication.CreateBuilder(args);

if (int.TryParse(builder.Configuration["PORT"], out var port) && port > 0)
{
    builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://0.0.0.0:{port}");
}

builder.AddQylServiceDefaults(static options => options.AdditionalActivitySources.Add("Qyl.Loom"));
builder.AddQylLoomDefaults();

// BitNet local-LLM hosting. One-line opt-in via ANcpLua.Agents.Hosting.BitNet:
// reads BITNET_URL / BITNET_API_PATH / BITNET_MODEL, registers a keyed IChatClient
// under "bitnet" with OpenTelemetry, LegacyMaxTokensPolicy, and a health check.
// The agent-layer telemetry is applied by QylLoomAgentsBuilder.Compose.
builder.AddQylBitNetChatClient();
builder.Services.Replace(
    ServiceDescriptor.Singleton<IQylLoomChatClientBuilder, QylLoomBitNetChatClientBuilder>());

builder.Services.AddSingleton<LoomGodAnalyzerServer>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .UseQylMcpInstrumentation(ActivitySources.McpSource, static options => options.Transport = "http")
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
