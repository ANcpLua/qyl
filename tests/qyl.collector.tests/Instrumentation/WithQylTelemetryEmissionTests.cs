
using ANcpLua.Agents.Testing.ChatClients;
using ANcpLua.Agents.Testing.Diagnostics;
using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Instrumentation;

public sealed class WithQylTelemetryEmissionTests
{
    [Fact]
    public async Task WithQylTelemetry_emits_qyl_genai_activity_on_GetResponseAsync()
    {
        using var collector = new ActivityCollector("qyl.genai");

        var inner = new FakeChatClient
            {
                Metadata = new ChatClientMetadata(
                    "openai",
                    null,
                    "gpt-4o-mini")
            }
            .WithResponse("Hello from fake.");

        var instrumented = inner.WithQylTelemetry("qyl.genai");

        var response = await instrumented.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            new ChatOptions { ModelId = "gpt-4o-mini" },
            CancellationToken.None);

        response.Text.Should().Contain("Hello from fake.");

        collector.Activities.Should().NotBeEmpty(
            "WithQylTelemetry must emit at least one Activity on 'qyl.genai' per invocation");

        var chatActivity = collector.Activities
            .First(static a => a.OperationName.ContainsIgnoreCase("chat")
                               || a.Tags.Any(static t => t.Key == "gen_ai.operation.name"));

        chatActivity.AssertHasTag("gen_ai.operation.name");
        chatActivity.AssertHasTag("gen_ai.request.model");

        chatActivity.Tags.Should().Contain(
            static t => t.Key == "gen_ai.provider.name",
            "GenAI spans must identify the provider via the 1.40 attribute");
    }

    [Fact]
    public async Task WithQylTelemetry_records_call_through_inner_client()
    {
        var inner = new FakeChatClient { Metadata = new ChatClientMetadata("openai", null, "gpt-4o-mini") }
            .WithResponse("ok");

        var instrumented = inner.WithQylTelemetry("qyl.genai");

        await instrumented.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "ping")],
            cancellationToken: CancellationToken.None);

        inner.CallCount.Should().Be(1);
        inner.LastOptions.Should().BeNull();
    }
}
