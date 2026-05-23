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
        var inner = NewFake();

        inner.WithQylTelemetry().Should().NotBeSameAs(inner);
    }

    [Fact]
    public void WithQylTelemetry_WrapsExistingOpenTelemetryClient_InToolDecorator() =>
        new OpenTelemetryChatClient(NewFake(), sourceName: "test")
            .WithQylTelemetry()
            .Should().BeOfType<ToolDecoratingChatClient>();

    [Fact]
    public void WithQylTelemetry_ReturnsSameInstance_WhenAlreadyToolDecorated()
    {
        var decorated = new ToolDecoratingChatClient(NewFake(), GenAiInstrumentation.WrapTool);

        decorated.WithQylTelemetry().Should().BeSameAs(decorated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithQylTelemetry_FlipsSensitiveDataFlag_OnExistingOpenTelemetryClient(bool enable)
    {
        var otel = new OpenTelemetryChatClient(NewFake(), sourceName: "test") { EnableSensitiveData = !enable };

        otel.WithQylTelemetry(enableSensitiveData: enable);

        otel.EnableSensitiveData.Should().Be(enable);
    }
}
