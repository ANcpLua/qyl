using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

/// <summary>
///     Registration helpers for the qyl agent inventory. Two surfaces:
///     <list type="bullet">
///         <item>
///             <see cref="AddQylAgentInventory" /> — DI singleton wiring; idempotent.
///         </item>
///         <item>
///             <see cref="RecordInQylInventory" /> — extension on
///             <see cref="AIAgent" /> that records the agent into the inventory at
///             construction time. Returns the agent for chaining off the qyl
///             three-builder pattern.
///         </item>
///     </list>
/// </summary>
public static class AddQylAIAgentExtensions
{
    /// <summary>Register the singleton <see cref="IQylAgentInventory" /> implementation.</summary>
    public static IServiceCollection AddQylAgentInventory(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IQylAgentInventory, QylAgentInventory>();
        services.TryAddSingleton<QylAgentActivityProcessor>();
        return services;
    }

    /// <summary>
    ///     Record an agent into the inventory and return it for further chaining. Designed to
    ///     slot in immediately after <c>.AsBuilder().UseQylAgentTelemetry().Build()</c> in the
    ///     qyl three-builder pattern.
    /// </summary>
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
