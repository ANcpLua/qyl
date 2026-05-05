using ANcpLua.Agents.Instrumentation;
using ANcpLua.Agents.Testing.ChatClients;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Instrumentation;

public sealed class ChatClientToolInstrumentationTests
{
    [Fact]
    public void WithQylTelemetry_wraps_plain_client_with_tool_decorator()
    {
        var inner = new FakeChatClient { Metadata = new ChatClientMetadata("test-provider", null, "test-model") };

        var result = inner.WithQylTelemetry();

        result.Should().NotBeSameAs(inner);
    }
}
