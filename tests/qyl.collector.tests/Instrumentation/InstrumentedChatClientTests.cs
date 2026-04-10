using System.Diagnostics;
using System.Net;
using AwesomeAssertions;
using Xunit;
using Microsoft.Extensions.AI;
using qyl.contracts.Attributes;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Instrumentation;

/// <summary>
///     Tests for <see cref="InstrumentedChatClient" />.
/// </summary>
public sealed class InstrumentedChatClientTests
{
    // -- helpers ---------------------------------------------------------------

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

    private static InstrumentedChatClient BuildClient(
        IChatClient inner,
        string? agentName = null,
        TimeProvider? timeProvider = null)
        => new(inner, agentName, timeProvider);

    // -- basic span attributes -------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_creates_activity_with_gen_ai_tags()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = new CapturingChatClient(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")]));
        var client = BuildClient(inner);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            new ChatOptions { ModelId = "gpt-4o" },
            TestContext.Current.CancellationToken);

        captured.Should().ContainSingle();
        var activity = captured[0];

        activity.GetTagItem(GenAiAttributes.OperationName).Should().Be(GenAiAttributes.Operations.Chat);
        activity.GetTagItem(GenAiAttributes.RequestModel).Should().Be("gpt-4o");
        activity.GetTagItem(GenAiAttributes.OutputType).Should().Be(GenAiAttributes.OutputTypes.Text);
    }

    [Fact]
    public async Task GetResponseAsync_falls_back_to_unknown_when_no_model_or_provider()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = new CapturingChatClient(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")]));
        var client = BuildClient(inner);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.Current.CancellationToken);

        captured.Should().ContainSingle();
        var activity = captured[0];

        activity.GetTagItem(GenAiAttributes.RequestModel).Should().Be("unknown");
        activity.GetTagItem(GenAiAttributes.ProviderName).Should().Be("unknown");
    }

    // -- response model and finish reason -------------------------------------

    [Fact]
    public async Task GetResponseAsync_records_response_model_and_finish_reason()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")])
        {
            ModelId = "gpt-4o-2024-11",
            FinishReason = ChatFinishReason.Stop
        };

        var inner = new CapturingChatClient(response);
        var client = BuildClient(inner);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")],
            cancellationToken: TestContext.Current.CancellationToken);

        var activity = captured.Should().ContainSingle().Which;

        activity.GetTagItem(GenAiAttributes.ResponseModel).Should().Be("gpt-4o-2024-11");

        var finishReasons = activity.GetTagItem(GenAiAttributes.ResponseFinishReasons) as string[];
        finishReasons.Should().NotBeNull();
        finishReasons![0].Should().Be("stop");
    }

    // -- token usage ----------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_records_token_usage_when_present()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "text")])
        {
            Usage = new UsageDetails
            {
                InputTokenCount = 100,
                OutputTokenCount = 42
            }
        };

        var inner = new CapturingChatClient(response);
        var client = BuildClient(inner);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "prompt")],
            cancellationToken: TestContext.Current.CancellationToken);

        var activity = captured.Should().ContainSingle().Which;

        activity.GetTagItem(GenAiAttributes.UsageInputTokens).Should().Be(100);
        activity.GetTagItem(GenAiAttributes.UsageOutputTokens).Should().Be(42);
    }

    [Fact]
    public async Task GetResponseAsync_does_not_add_usage_tags_when_usage_is_null()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "text")]);
        // Usage remains null

        var inner = new CapturingChatClient(response);
        var client = BuildClient(inner);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "prompt")],
            cancellationToken: TestContext.Current.CancellationToken);

        var activity = captured.Should().ContainSingle().Which;

        activity.GetTagItem(GenAiAttributes.UsageInputTokens).Should().BeNull();
        activity.GetTagItem(GenAiAttributes.UsageOutputTokens).Should().BeNull();
    }

    // -- error handling -------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_sets_error_status_on_HttpRequestException()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = new ThrowingChatClient(
            new HttpRequestException("connection refused", null, HttpStatusCode.ServiceUnavailable));
        var client = BuildClient(inner);

        var act = async () => await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "call")],
            cancellationToken: TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<HttpRequestException>();

        var activity = captured.Should().ContainSingle().Which;

        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem(GenAiAttributes.ErrorType).Should().Be("503");
    }

    // -- agent name -----------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_records_agent_name_when_provided()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = new CapturingChatClient(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")]));
        var client = BuildClient(inner, agentName: "MyAgent");

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            cancellationToken: TestContext.Current.CancellationToken);

        var activity = captured.Should().ContainSingle().Which;

        activity.GetTagItem("gen_ai.agent.name").Should().Be("MyAgent");
    }

    [Fact]
    public async Task GetResponseAsync_does_not_add_agent_name_tag_when_not_provided()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = new CapturingChatClient(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hi")]));
        var client = BuildClient(inner); // no agentName

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            cancellationToken: TestContext.Current.CancellationToken);

        var activity = captured.Should().ContainSingle().Which;

        activity.GetTagItem("gen_ai.agent.name").Should().BeNull();
    }
}

// -- test doubles ---------------------------------------------------------------

/// <summary>Minimal IChatClient that returns a preset response.</summary>
file sealed class CapturingChatClient(ChatResponse response) : IChatClient
{
    public ChatOptions? LastOptions { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        LastOptions = options;
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

/// <summary>Minimal IChatClient that always throws.</summary>
file sealed class ThrowingChatClient(Exception exception) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw exception;

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
