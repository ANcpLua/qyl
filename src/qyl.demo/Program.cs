using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenTelemetry.Trace;
using qyl.agents.telemetry;
using qyl.sdk.aspnetcore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddQylAgentObservability();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(GenAiAttributes.SourceName)
        .AddConsoleExporter());

var chatClient = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");
builder.Services.AddChatClient(chatClient);

builder.AddAIAgent("writer", (sp, key) =>
{
    var client = sp.GetRequiredService<IChatClient>();
    return client.CreateAIAgent(
            name: key,
            instructions: "You write short stories (300 words or less) about the specified topic.")
        .AsBuilder()
        .UseQylOpenTelemetry()
        .Build();
});

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
        .UseQylOpenTelemetry()
        .Build();
});

builder.AddWorkflow("publisher", (sp, key) =>
{
    var writer = sp.GetRequiredKeyedService<AIAgent>("writer");
    var editor = sp.GetRequiredKeyedService<AIAgent>("editor");

    return AgentWorkflowBuilder.BuildSequential(
        key,
        writer,
        editor);
}).AddAsAIAgent();

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

var app = builder.Build();
app.UseHttpsRedirection();

app.MapOpenAIResponses();
app.MapOpenAIConversations();

if (builder.Environment.IsDevelopment())

    app.MapDevUI();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    schema = "v1.38.0"
}));

await app.RunAsync().ConfigureAwait(false);
return;

[Description("Formats the story for publication, revealing its title.")]
static string FormatStory(string title, string story)
{
    using var activity = new ActivitySource(GenAiAttributes.SourceName)
        .StartActivity(GenAiAttributes.ExecuteTool);

    activity?.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.ExecuteTool);
    activity?.SetTag(GenAiAttributes.ToolName, nameof(FormatStory));
    activity?.SetTag(GenAiAttributes.ToolType, "function");

    var result = $"""
                  **Title**: {title}

                  {story}
                  """;

    activity?.SetTag(GenAiAttributes.ToolCallResult, result);

    return result;
}
