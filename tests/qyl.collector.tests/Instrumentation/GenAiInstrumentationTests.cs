using ANcpLua.Agents.Instrumentation;
using ANcpLua.Agents.Testing.ChatClients;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Instrumentation;

public sealed class GenAiInstrumentationTests
{
    [Fact]
    public void WithQylTelemetry_wraps_OpenTelemetryChatClient_in_ToolInstrumenting()
    {
        var inner = new FakeChatClient { Metadata = new ChatClientMetadata("test-provider", null, "test-model") };
        var otel = new OpenTelemetryChatClient(inner, sourceName: "test");

        var result = otel.WithQylTelemetry();

        result.Should().BeOfType<ToolDecoratingChatClient>();
    }

    [Fact]
    public void WithQylTelemetry_does_not_double_wrap_ToolDecoratingChatClient()
    {
        var inner = new FakeChatClient { Metadata = new ChatClientMetadata("test-provider", null, "test-model") };
        var toolClient = new ToolDecoratingChatClient(inner, GenAiInstrumentation.WrapTool);

        var result = toolClient.WithQylTelemetry();

        result.Should().BeSameAs(toolClient);
    }

    [Fact]
    public void WithQylTelemetry_wraps_plain_client_with_full_pipeline()
    {
        var inner = new FakeChatClient { Metadata = new ChatClientMetadata("test-provider", null, "test-model") };

        var result = inner.WithQylTelemetry();

        result.Should().NotBeSameAs(inner);
    }
}
