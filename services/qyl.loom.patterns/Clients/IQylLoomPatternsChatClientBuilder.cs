
namespace Qyl.Loom.Patterns.Clients;

public interface IQylLoomPatternsChatClientBuilder : IDisposable
{
    IChatClient BuildChatClient(string stage);
}
