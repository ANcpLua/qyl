
using Microsoft.Extensions.AI;

namespace Qyl.Loom.Clients;

internal sealed class QylLoomChatClientBuilder(IChatClient? llm = null) : IQylLoomChatClientBuilder
{
    public IChatClient? BuildChatClient() => llm;
}
