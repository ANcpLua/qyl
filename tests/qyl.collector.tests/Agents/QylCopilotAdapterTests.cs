using Qyl.Agents.Adapters;
using Qyl.Contracts.Copilot;
using Xunit;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace Qyl.Collector.Tests.Agents;

public sealed class QylCopilotAdapterTests
{
    [Fact]
    public void BuildConversationMessages_PreservesSystemAndHistoryAsTypedMessages()
    {
        var context = new CopilotContext
        {
            AdditionalContext = "## Context\nInvestigate the latest production error.",
            History =
            [
                new ChatMessage { Role = ChatRole.User, Content = "What happened?" },
                new ChatMessage { Role = ChatRole.Assistant, Content = "I am checking the trace." }
            ]
        };

        var messages = QylCopilotAdapter.BuildConversationMessages(
            "Give me the root cause.",
            context,
            sessionHistory: null);

        Assert.Collection(messages,
            static message =>
            {
                Assert.Equal(AiChatRole.System, message.Role);
                Assert.Equal("## Context\nInvestigate the latest production error.", message.Text);
            },
            static message =>
            {
                Assert.Equal(AiChatRole.User, message.Role);
                Assert.Equal("What happened?", message.Text);
            },
            static message =>
            {
                Assert.Equal(AiChatRole.Assistant, message.Role);
                Assert.Equal("I am checking the trace.", message.Text);
            },
            static message =>
            {
                Assert.Equal(AiChatRole.User, message.Role);
                Assert.Equal("Give me the root cause.", message.Text);
            });
    }
}
