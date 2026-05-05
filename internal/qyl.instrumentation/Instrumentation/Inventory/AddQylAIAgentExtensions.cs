using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

public static class AddQylAIAgentExtensions
{
    public static IServiceCollection AddQylAgentInventory(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IQylAgentInventory, QylAgentInventory>();
        services.TryAddSingleton<QylAgentActivityProcessor>();
        return services;
    }

    public static AIAgent RecordInQylInventory(
        this AIAgent agent,
        IQylAgentInventory? inventory,
        string key,
        string? instructions = null,
        string? description = null,
        string? providerName = null)
    {
        Guard.NotNull(agent);
        if (inventory is null) return agent;

        inventory.Register(new AgentRegistration(
            Key: key,
            Name: agent.Name ?? key,
            Description: description,
            InstructionsHash: QylAgentInventory.HashInstructions(instructions),
            ProviderName: providerName,
            RegisteredAtUtc: default));

        return agent;
    }
}
