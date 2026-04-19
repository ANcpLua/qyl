using ANcpLua.Agents.Instrumentation;
using ANcpLua.Agents.Testing.ChatClients;
using AwesomeAssertions;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Xunit;

namespace Qyl.Collector.Tests.Instrumentation;

/// <summary>
///     Verifies that <see cref="GenAiInstrumentation.WithQylTelemetry(IChatClient, string?, bool?)"/>
///     produces a pipeline whose outermost layer is the qyl-decorated
///     <see cref="ToolDecoratingChatClient"/>.
/// </summary>
public sealed class ChatClientToolInstrumentationTests
{
    [Fact]
    public void WithQylTelemetry_wraps_plain_client_with_tool_decorator()
    {
        var inner = new FakeChatClient
        {
            Metadata = new ChatClientMetadata("test-provider", null, "test-model")
        };

        var result = inner.WithQylTelemetry();

        result.Should().NotBeSameAs(inner);
    }
}
