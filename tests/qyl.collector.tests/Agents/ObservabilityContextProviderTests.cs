using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Agents.Context;
using Qyl.Contracts.Copilot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace Qyl.Collector.Tests.Agents;

public sealed class ObservabilityContextProviderTests
{
    // ---------------------------------------------------------------------------
    // ProvideMessagesAsync returns [] when no issueId in state bag
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ProvideMessages_returns_empty_when_no_issueId()
    {
        var source = new StubContextSource("irrelevant");
        var provider = new TestableProvider(source);

        var session = new StubSession();
        var ctx = MakeContext(session);

        IEnumerable<AiChatMessage> messages = await provider.InvokeProvideMessagesAsync(ctx);

        Assert.Empty(messages);
    }

    // ---------------------------------------------------------------------------
    // ProvideMessagesAsync returns [] when issueId present but context is empty
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ProvideMessages_returns_empty_when_context_source_returns_empty()
    {
        var source = new StubContextSource(string.Empty);
        var provider = new TestableProvider(source);

        var session = new StubSession();
        session.StateBag.SetValue<string>(ObservabilityContextProvider.IssueIdKey, "issue-1", null!);
        var ctx = MakeContext(session);

        IEnumerable<AiChatMessage> messages = await provider.InvokeProvideMessagesAsync(ctx);

        Assert.Empty(messages);
    }

    // ---------------------------------------------------------------------------
    // ProvideMessagesAsync returns a system message containing the formatted context
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ProvideMessages_returns_system_message_with_formatted_context()
    {
        const string formatted = "Error: NullReferenceException\nStack: at Foo()";
        var source = new StubContextSource(formatted);
        var provider = new TestableProvider(source);

        var session = new StubSession();
        session.StateBag.SetValue<string>(ObservabilityContextProvider.IssueIdKey, "issue-42", null!);
        var ctx = MakeContext(session);

        IEnumerable<AiChatMessage> messages = await provider.InvokeProvideMessagesAsync(ctx);

        List<AiChatMessage> list = [..messages];
        Assert.Single(list);
        Assert.Equal(AiChatRole.System, list[0].Role);
        Assert.Contains("## Error Context", list[0].Text);
        Assert.Contains(formatted, list[0].Text);
    }

    // ---------------------------------------------------------------------------
    // ProvideMessagesAsync forwards the correct issueId to the context source
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task ProvideMessages_passes_correct_issueId_to_context_source()
    {
        var source = new CapturingContextSource("some-context");
        var provider = new TestableProvider(source);

        var session = new StubSession();
        session.StateBag.SetValue<string>(ObservabilityContextProvider.IssueIdKey, "issue-99", null!);
        var ctx = MakeContext(session);

        await provider.InvokeProvideMessagesAsync(ctx);

        Assert.Equal("issue-99", source.LastIssueId);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static MessageAIContextProvider.InvokingContext MakeContext(AgentSession session) =>
        new(new StubAgent(), session, []);

    /// <summary>
    ///     Exposes the protected <see cref="ObservabilityContextProvider.ProvideMessagesAsync"/>
    ///     for unit testing.
    /// </summary>
    private sealed class TestableProvider(IIssueContextSource source)
        : ObservabilityContextProvider(source)
    {
        public ValueTask<IEnumerable<AiChatMessage>> InvokeProvideMessagesAsync(
            MessageAIContextProvider.InvokingContext context,
            CancellationToken ct = default) =>
            ProvideMessagesAsync(context, ct);
    }

    private sealed class StubContextSource(string formatted) : IIssueContextSource
    {
        public Task<string> GetFormattedContextAsync(
            string issueId, string? userContext = null, CancellationToken ct = default) =>
            Task.FromResult(formatted);
    }

    private sealed class CapturingContextSource(string formatted) : IIssueContextSource
    {
        public string? LastIssueId { get; private set; }

        public Task<string> GetFormattedContextAsync(
            string issueId, string? userContext = null, CancellationToken ct = default)
        {
            LastIssueId = issueId;
            return Task.FromResult(formatted);
        }
    }

    private sealed class StubSession : AgentSession { }

    private sealed class StubAgent : AIAgent
    {
        public StubAgent() { }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<AiChatMessage>? requestMessages,
            AgentSession? session,
            AgentRunOptions? options,
            CancellationToken cancellationToken) =>
            throw new System.NotImplementedException();

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<AiChatMessage>? requestMessages,
            AgentSession? session,
            AgentRunOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield break;
        }

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement state,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<AgentSession>(new StubSession());

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(default(JsonElement));

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<AgentSession>(new StubSession());
    }
}
