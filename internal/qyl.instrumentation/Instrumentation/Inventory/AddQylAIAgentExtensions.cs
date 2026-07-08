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
}
