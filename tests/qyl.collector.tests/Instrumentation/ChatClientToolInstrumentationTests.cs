using AwesomeAssertions;
using Xunit;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Instrumentation;

public sealed class ChatClientToolInstrumentationTests
{
    [Fact]
    public async Task BuilderInstrumentation_AutoWrapsTools()
    {
        using var inner = new CapturingChatClient();
        using var client = new ChatClientBuilder(inner)
            .UseQylInstrumentation()
            .Build();

        var options = CreateOptions();
        await client.GetResponseAsync(CreateMessages(), options, TestContext.Current.CancellationToken);

        inner.LastOptions.Should().NotBeNull();
        var capturedTool = inner.LastOptions!.Tools!.Should().ContainSingle().Which;
        capturedTool.Should().BeOfType<InstrumentedAIFunction>();
    }

    [Fact]
    public async Task DirectClientInstrumentation_AutoWrapsTools()
    {
        using var inner = new CapturingChatClient();
        using var client = inner.UseQylInstrumentation(agentName: "loom");

        var options = CreateOptions();
        await client.GetResponseAsync(CreateMessages(), options, TestContext.Current.CancellationToken);

        inner.LastOptions.Should().NotBeNull();
        var capturedTool = inner.LastOptions!.Tools!.Should().ContainSingle().Which;
        capturedTool.Should().BeOfType<InstrumentedAIFunction>();
    }

    [Fact]
    public async Task OpenTelemetryBridge_AutoWrapsTools()
    {
        using var inner = new CapturingChatClient();
        using var client = inner.WithQylTelemetry();

        var options = CreateOptions();
        await client.GetResponseAsync(CreateMessages(), options, TestContext.Current.CancellationToken);

        inner.LastOptions.Should().NotBeNull();
        var capturedTool = inner.LastOptions!.Tools!.Should().ContainSingle().Which;
        capturedTool.Should().BeOfType<InstrumentedAIFunction>();
    }

    private static List<ChatMessage> CreateMessages() =>
    [
        new(ChatRole.User, "ping")
    ];

    private static ChatOptions CreateOptions() =>
        new()
        {
            Tools = [AIFunctionFactory.Create(static () => "pong")]
        };

    private sealed class CapturingChatClient : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public void Dispose()
        {
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Stream();

            static async IAsyncEnumerable<ChatResponseUpdate> Stream()
            {
                await Task.CompletedTask;
                yield break;
            }
        }
    }
}
