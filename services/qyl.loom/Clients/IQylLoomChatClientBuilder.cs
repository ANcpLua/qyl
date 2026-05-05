
using Microsoft.Extensions.AI;

namespace Qyl.Loom.Clients;

internal interface IQylLoomChatClientBuilder
{
    IChatClient? BuildChatClient();
}
