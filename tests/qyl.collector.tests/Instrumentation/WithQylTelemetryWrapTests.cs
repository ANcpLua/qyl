using ANcpLua.Agents.Instrumentation;
using ANcpLua.Agents.Testing.ChatClients;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Instrumentation;

public sealed class WithQylTelemetryWrapTests
{
    private static FakeChatClient NewFake() =>
        new() { Metadata = new ChatClientMetadata("openai", null, "gpt-4o-mini") };

    [Fact]
    public void WithQylTelemetry_WrapsPlainClient()
    {
        using var inner = NewFake();
        using var client = inner.WithQylTelemetry();

        client.Should().NotBeSameAs(inner);
    }

    [Fact]
    public void WithQylTelemetry_WrapsExistingOpenTelemetryClient_InToolDecorator()
    {
        using var inner = NewFake();
        using var otel = new OpenTelemetryChatClient(inner, sourceName: "test");
        using var client = otel.WithQylTelemetry();

        client.Should().BeOfType<ToolDecoratingChatClient>();
    }

    [Fact]
    public void WithQylTelemetry_ReturnsSameInstance_WhenAlreadyToolDecorated()
    {
        using var inner = NewFake();
        using var decorated = new ToolDecoratingChatClient(inner, GenAiInstrumentation.WrapTool);

        decorated.WithQylTelemetry().Should().BeSameAs(decorated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithQylTelemetry_FlipsSensitiveDataFlag_OnExistingOpenTelemetryClient(bool enable)
    {
        using var inner = NewFake();
        using var otel = new OpenTelemetryChatClient(inner, sourceName: "test") { EnableSensitiveData = !enable };

        using var client = otel.WithQylTelemetry(enableSensitiveData: enable);

        otel.EnableSensitiveData.Should().Be(enable);
    }
}
