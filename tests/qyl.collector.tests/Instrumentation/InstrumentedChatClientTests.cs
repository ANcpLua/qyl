using System.Net;
using ANcpLua.Agents.Testing.ChatClients;
using Microsoft.Extensions.AI;
using qyl.contracts.Attributes;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Xunit;

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

    // ActivityListener captures every span on the qyl.genai source. Each test below
    // tags its request with a unique ModelId and filters captured activities by it
    // so parallel sibling tests can't pollute the assertion.
    private static Activity ForModel(List<Activity> captured, string modelId) =>
        captured.Should().ContainSingle(a => (string?)a.GetTagItem(GenAiAttributes.RequestModel) == modelId).Which;

    // -- basic span attributes -------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_creates_activity_with_gen_ai_tags()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = new FakeChatClient().WithResponse("hi");
        var client = BuildClient(inner);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            new ChatOptions { ModelId = "icct-basic-attrs" },
            TestContext.Current.CancellationToken);

        var activity = ForModel(captured, "icct-basic-attrs");
        activity.GetTagItem(GenAiAttributes.OperationName).Should().Be(GenAiAttributes.Operations.Chat);
        activity.GetTagItem(GenAiAttributes.OutputType).Should().Be(GenAiAttributes.OutputTypes.Text);
    }

    // -- response model and finish reason -------------------------------------

    [Fact]
    public async Task GetResponseAsync_records_response_model_and_finish_reason()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = new FakeChatClient().WithResponse(
            "done",
            finishReason: ChatFinishReason.Stop,
            modelId: "gpt-4o-2024-11");
        var client = BuildClient(inner);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")],
            new ChatOptions { ModelId = "icct-finish-reason" },
            TestContext.Current.CancellationToken);

        var activity = ForModel(captured, "icct-finish-reason");
        activity.GetTagItem(GenAiAttributes.ResponseModel).Should().Be("gpt-4o-2024-11");

        var finishReasons = activity.GetTagItem(GenAiAttributes.ResponseFinishReasons) as string[];
        finishReasons.Should().NotBeNull();
        finishReasons[0].Should().Be("stop");
    }

    // -- token usage ----------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_does_not_add_usage_tags_when_usage_is_null()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = new FakeChatClient().WithResponse("text");
        var client = BuildClient(inner);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "prompt")],
            new ChatOptions { ModelId = "icct-no-usage" },
            TestContext.Current.CancellationToken);

        var activity = ForModel(captured, "icct-no-usage");
        activity.GetTagItem(GenAiAttributes.UsageInputTokens).Should().BeNull();
        activity.GetTagItem(GenAiAttributes.UsageOutputTokens).Should().BeNull();
    }

    // -- error handling -------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_sets_error_status_on_HttpRequestException()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = FakeChatClient.WithException(
            new HttpRequestException("connection refused", null, HttpStatusCode.ServiceUnavailable));
        var client = BuildClient(inner);

        var act = async () => await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "call")],
            new ChatOptions { ModelId = "icct-http-error" },
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<HttpRequestException>();

        var activity = ForModel(captured, "icct-http-error");
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem(GenAiAttributes.ErrorType).Should().Be("503");
    }

    // -- agent name -----------------------------------------------------------

    [Fact]
    public async Task GetResponseAsync_does_not_add_agent_name_tag_when_not_provided()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = new FakeChatClient().WithResponse("hi");
        var client = BuildClient(inner); // no agentName

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            new ChatOptions { ModelId = "icct-no-agent" },
            TestContext.Current.CancellationToken);

        var activity = ForModel(captured, "icct-no-agent");
        activity.GetTagItem("gen_ai.agent.name").Should().BeNull();
    }
}

// FakeChatClient from ANcpLua.Agents.Testing.ChatClients
// supersedes the file-scoped CapturingChatClient + ThrowingChatClient that lived here.
