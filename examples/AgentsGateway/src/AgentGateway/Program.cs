using System.ComponentModel;
using System.Reflection;
using AgentGateway.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Blobs;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Infrastructure Setup ---

builder.Services.AddControllers();
builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();

// Telemetry with standard tagging
if (builder.Environment.IsProduction())
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracerProviderBuilder =>
            tracerProviderBuilder.AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                {
                    activity.SetTag("http.request.header.user-agent",
                        httpRequestMessage.Headers.UserAgent.ToString());
                };
            }))
        .UseAzureMonitor(options => { options.Credential = new DefaultAzureCredential(); });

// Authentication (skip in Development for testing)
if (!builder.Environment.IsDevelopment()) builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// AOT-compatible JSON serialization for Gateway Catalog
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AgentGatewayJsonContext.Default);
});

// Storage
builder.Services.AddSingleton<IStorage>(_ =>
{
    var containerName = builder.Configuration["BlobsStorageOptions:ContainerName"] ?? "state";
    if (builder.Environment.IsDevelopment()) return new BlobsStorage("UseDevelopmentStorage=true", containerName);

    var storageAccountName = builder.Configuration["BlobsStorageOptions:StorageAccountName"];
    return new BlobsStorage(
        new Uri($"https://{storageAccountName}.blob.core.windows.net/{containerName}"),
        new DefaultAzureCredential()
    );
});

// --- 2. AI Router & Provider Discovery ---

builder.Services.AddDiscoveredAdapters(builder.Configuration, Assembly.GetExecutingAssembly());
builder.Services.AddSingleton<IProviderSelectionPolicy, HeaderSelectionPolicy>();
builder.Services.AddSingleton<IChatClient, ProviderRouterChatClient>();

// --- 3. Agents & Workflows ---

builder.AddAIAgent("writer", "You write short stories (300 words or less) about the specified topic.");

builder.AddAIAgent("editor", (sp, key) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    return new ChatClientAgent(
        chatClient,
        name: key,
        instructions: "You edit short stories to improve grammar and style.",
        tools: [AIFunctionFactory.Create(FormatStory)]
    );
});

builder.AddWorkflow("publisher", (sp, key) => AgentWorkflowBuilder.BuildSequential(
    key,
    sp.GetRequiredKeyedService<AIAgent>("writer"),
    sp.GetRequiredKeyedService<AIAgent>("editor")
)).AddAsAIAgent();

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

var app = builder.Build();

// --- 4. Middleware & Routes ---

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenAIResponses();
app.MapOpenAIConversations();

// A2A Agent-to-Agent Endpoints
app.MapA2A("writer", "/a2a/writer");
app.MapA2A("editor", "/a2a/editor");
app.MapA2A("publisher", "/a2a/publisher");

// Gateway Catalog Endpoints
app.MapGet("/v1/catalog/providers", (IProviderRegistry reg) => Results.Ok(reg.All));
app.MapGet("/v1/catalog/providers/{id}/models", async (string id, IProviderRegistry reg, IServiceProvider sp) =>
{
    var catalog = reg.ResolveCatalog(id, sp);
    return catalog is not null ? Results.Ok(await catalog.ListModelsAsync()) : Results.NotFound();
});

app.MapGet("/healthz", () => Results.Ok(new
{
    Status = "Healthy"
}));

if (app.Environment.IsDevelopment()) app.MapDevUI();

app.Run();

[Description("Formats the story for publication.")]
static string FormatStory(string title, string story) => $"**Title**: {title}\n\n{story}";