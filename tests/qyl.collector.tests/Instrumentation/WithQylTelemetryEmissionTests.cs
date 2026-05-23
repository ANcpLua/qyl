using ANcpLua.Agents.Testing.ChatClients;
using ANcpLua.Agents.Testing.Diagnostics;
using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Instrumentation;

public sealed class WithQylTelemetryEmissionTests
{
    [Fact]
    public async Task WithQylTelemetry_EmitsActivityOn_qyl_genai_Source()
    {
        using var collector = new ActivityCollector("qyl.genai");

        await NewInstrumented().GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            new ChatOptions { ModelId = "gpt-4o-mini" },
            TestContext.Current.CancellationToken);

        collector.Activities.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("gen_ai.operation.name")]
    [InlineData("gen_ai.request.model")]
    [InlineData("gen_ai.provider.name")]
    public async Task WithQylTelemetry_EmittedActivity_Carries(string expectedTagKey)
    {
        using var collector = new ActivityCollector("qyl.genai");

        await NewInstrumented().GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            new ChatOptions { ModelId = "gpt-4o-mini" },
            TestContext.Current.CancellationToken);

        var chat = collector.Activities.First(static a =>
            a.OperationName.ContainsIgnoreCase("chat")
            || a.Tags.Any(static t => t.Key == "gen_ai.operation.name"));

        chat.Tags.Should().Contain(t => t.Key == expectedTagKey);
    }

    [Fact]
    public async Task WithQylTelemetry_DelegatesGetResponse_ToInnerClient()
    {
        var inner = new FakeChatClient { Metadata = new ChatClientMetadata("openai", null, "gpt-4o-mini") }
            .WithResponse("ok");

        await inner.WithQylTelemetry("qyl.genai")
            .GetResponseAsync(
                [new ChatMessage(ChatRole.User, "ping")],
                cancellationToken: TestContext.Current.CancellationToken);

        inner.CallCount.Should().Be(1);
    }

    private static IChatClient NewInstrumented() =>
        new FakeChatClient { Metadata = new ChatClientMetadata("openai", null, "gpt-4o-mini") }
            .WithResponse("Hello from fake.")
            .WithQylTelemetry("qyl.genai");
}
