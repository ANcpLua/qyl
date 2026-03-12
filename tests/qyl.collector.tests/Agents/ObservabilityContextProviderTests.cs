using ANcpLua.Roslyn.Utilities.Testing.AgentTesting;
using Qyl.Agents.Agents;
using Qyl.Agents.Context;
using Qyl.Contracts.Copilot;
using Xunit;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace Qyl.Collector.Tests.Agents;

public sealed class ObservabilityContextProviderTests
{
    [Fact]
    public async Task FromChatClient_IncludesIssueContextProviderMessages()
    {
        using var client = FakeChatClient.WithText("done");
        var provider = new ObservabilityContextProvider(new StubIssueContextSource("formatted issue context"));
        var agent = QylAgentBuilder.FromChatClient(client, contextProviders: [provider]);
        var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
        session.StateBag.SetValue(ObservabilityContextProvider.IssueIdKey, "issue-42");

        await foreach (var _ in agent.RunStreamingAsync(
                           "Investigate this issue.",
                           session,
                           cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        var call = Assert.Single(client.Calls);
        Assert.Collection(call.Messages,
            static message =>
            {
                Assert.Equal(AiChatRole.System, message.Role);
                Assert.Equal("## Error Context\nformatted issue context", message.Text);
            },
            static message =>
            {
                Assert.Equal(AiChatRole.User, message.Role);
                Assert.Equal("Investigate this issue.", message.Text);
            });
    }

    private sealed class StubIssueContextSource(string formattedContext) : IIssueContextSource
    {
        public Task<string> GetFormattedContextAsync(
            string issueId,
            string? userContext = null,
            CancellationToken ct = default) =>
            Task.FromResult(formattedContext);
    }
}
