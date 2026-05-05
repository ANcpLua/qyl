
using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Qyl.Instrumentation.Instrumentation.Inventory;
using Qyl.Instrumentation.Instrumentation;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

var endpoint = Environment.GetEnvironmentVariable("QYL_LLM_BASE_URL")
               ?? "http://localhost:11434/v1";
var model = Environment.GetEnvironmentVariable("QYL_LLM_MODEL")
            ?? "qwen2.5:0.5b";
var apiKey = Environment.GetEnvironmentVariable("QYL_LLM_API_KEY") ?? "ollama";
var conversationId = Environment.GetEnvironmentVariable("QYL_SMOKE_CONVERSATION_ID")
                     ?? $"smoke:{TimeProvider.System.GetUtcNow():yyyyMMddHHmmss}";
var turns = int.TryParse(Environment.GetEnvironmentVariable("QYL_SMOKE_TURNS"), out var t) && t > 0
    ? t
    : 2;

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
{
    Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4318");
}

Console.WriteLine("# qyl smoke — PRD #173 quality gate");
Console.WriteLine($"#   endpoint          {endpoint}");
Console.WriteLine($"#   model             {model}");
Console.WriteLine($"#   conversation.id   {conversationId}");
Console.WriteLine($"#   turns             {turns}");
Console.WriteLine($"#   otlp              {Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")}");

var builder = Host.CreateApplicationBuilder(args);
builder.AddQylServiceDefaults(static opts =>
{
    opts.AdditionalActivitySources.Add("qyl.smoke");
    opts.EnableDefaultHealthChecks = false;
    opts.EnableDefaultHealthEndpoints = false;
});

using var host = builder.Build();
await host.StartAsync().ConfigureAwait(false);

var inventory = host.Services.GetRequiredService<IQylAgentInventory>();

var oai = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

var chatClient = oai
    .GetChatClient(model)
    .AsIChatClient()
    .WithQylTelemetry();

const string Instructions =
    "Respond with a single short word. No preamble, no punctuation, no emoji.";

var agent = chatClient
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "SmokeAgent",
        Description = "PRD #173 quality gate — exercises cost + activity processors and inventory.",
        ChatOptions = new ChatOptions { Instructions = Instructions }
    })
    .AsBuilder()
    .UseQylAgentTelemetry()
    .Build()
    .RecordInQylInventory(
        inventory,
        key: "SmokeAgent",
        instructions: Instructions,
        description: "PRD #173 quality gate — exercises cost + activity processors and inventory.",
        providerName: "ollama");

using var smokeSource = new ActivitySource("qyl.smoke");

using (var conversation = smokeSource.StartActivity(name: "smoke conversation"))
{
    conversation?.SetTag(GenAiAttributes.ConversationId, conversationId);

    for (var i = 1; i <= turns; i++)
    {
        Console.WriteLine($"# turn {i}/{turns}: invoking SmokeAgent…");
        var response = await agent
            .RunAsync($"Are you alive? (turn {i})")
            .ConfigureAwait(false);
        var text = response.Text.Trim();
        Console.WriteLine($"  → {text}");
    }
}

await Task.Delay(2_500).ConfigureAwait(false);
await host.StopAsync().ConfigureAwait(false);

Console.WriteLine("# done — telemetry flushed");
Console.WriteLine($"# probe: curl -s {Environment.GetEnvironmentVariable("QYL_COLLECTOR_URL") ?? "http://localhost:5100"}/api/v1/conversations | jq '.items[] | select(.sessionId == \"{conversationId}\")'");
Console.WriteLine($"# probe: curl -s {Environment.GetEnvironmentVariable("QYL_COLLECTOR_URL") ?? "http://localhost:5100"}/qyl/inventory/agents | jq '.items[] | select(.name == \"SmokeAgent\")'");
