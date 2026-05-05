
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Qyl.Hosting;

public static class QylAgentExtensions
{
    public static AIAgent AsQylAgent(
        this IChatClient client,
        string name,
        string description,
        string instructions,
        Action<AIAgentBuilder>? telemetry = null,
        IServiceProvider? services = null)
    {
        Guard.NotNull(client);
        Guard.NotNullOrEmpty(name);
        Guard.NotNull(description);
        Guard.NotNull(instructions);

        ChatClientAgentOptions options = new()
        {
            Name = name,
            Description = description,
            ChatOptions = new ChatOptions { Instructions = instructions }
        };

        var builder = client.AsAIAgent(options).AsBuilder();
        telemetry?.Invoke(builder);
        return services is null ? builder.Build() : builder.Build(services);
    }
}
