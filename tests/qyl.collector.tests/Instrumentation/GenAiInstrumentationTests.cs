using System.Diagnostics;
using AwesomeAssertions;
using Xunit;
using Microsoft.Extensions.AI;
using qyl.contracts.Attributes;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Instrumentation;

/// <summary>Tests for <see cref="GenAiInstrumentation" />.</summary>
public sealed class GenAiInstrumentationTests
{
    private static ActivityListener CreateListener(List<Activity> captured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == ActivitySources.GenAi,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = captured.Add
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task ExecuteAsync_creates_span_with_provider_operation_model_tags()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        await GenAiInstrumentation.ExecuteAsync(
            "openai", "chat", "gpt-4o",
            static () => Task.FromResult("result"));

        var activity = captured.Should().ContainSingle().Which;
        activity.GetTagItem(GenAiAttributes.ProviderName).Should().Be("openai");
        activity.GetTagItem(GenAiAttributes.OperationName).Should().Be("chat");
        activity.GetTagItem(GenAiAttributes.RequestModel).Should().Be("gpt-4o");
    }

    [Fact]
    public async Task ExecuteAsync_records_token_usage_when_extractUsage_provided()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        await GenAiInstrumentation.ExecuteAsync(
            "anthropic", "chat", "claude-3-5",
            static () => Task.FromResult(new TokenUsage(50, 25)),
            static r => r);

        var activity = captured.Should().ContainSingle().Which;
        activity.GetTagItem(GenAiAttributes.UsageInputTokens).Should().Be(50);
        activity.GetTagItem(GenAiAttributes.UsageOutputTokens).Should().Be(25);
    }

    [Fact]
    public async Task ExecuteAsync_records_error_on_exception()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var act = async () => await GenAiInstrumentation.ExecuteAsync<string>(
            "openai", "chat", "gpt-4o",
            static () => throw new InvalidOperationException("fail"));
        await act.Should().ThrowAsync<InvalidOperationException>();

        var activity = captured.Should().ContainSingle().Which;
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem(GenAiAttributes.ErrorType).Should().Be("InvalidOperationException");
    }

    [Fact]
    public void WithQylTelemetry_wraps_OpenTelemetryChatClient_in_ToolInstrumenting()
    {
        var inner = new CapturingInnerClient();
        var otel = new OpenTelemetryChatClient(inner, sourceName: "test");

        var result = otel.WithQylTelemetry();

        // OTel client gets wrapped with tool instrumentation on top
        result.Should().BeOfType<ToolInstrumentingChatClient>();
    }

    [Fact]
    public void WithQylTelemetry_does_not_double_wrap_ToolInstrumentingChatClient()
    {
        var inner = new CapturingInnerClient();
        var toolClient = new ToolInstrumentingChatClient(inner);

        var result = toolClient.WithQylTelemetry();

        result.Should().BeSameAs(toolClient);
    }

    [Fact]
    public void WithQylTelemetry_wraps_plain_client_with_full_pipeline()
    {
        var inner = new CapturingInnerClient();

        var result = inner.WithQylTelemetry();

        // Pipeline: inner → OpenTelemetry → ToolInstrumenting
        result.Should().NotBeSameAs(inner);
    }
}

file sealed class CapturingInnerClient : IChatClient
{
    public ChatClientMetadata Metadata => new("test-provider", null, "test-model");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? Metadata : null;

    public void Dispose() { }
}
