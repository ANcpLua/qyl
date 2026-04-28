using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ANcpLua.Roslyn.Utilities;
namespace Qyl.Instrumentation.Instrumentation.Loom;

public static class LoomToolServiceExtensions
{
    public static IServiceCollection AddLoomFactoryTools(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IReadOnlyList<AIFunction>>(static sp =>
            LoomToolFactoryBridge.CreateAIFunctions(LoomGeneratedRegistry.RuntimeMetadata, sp));

        return services;
    }

    public static IServiceCollection AddInstrumentedLoomFactoryTools(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IReadOnlyList<AIFunction>>(static sp =>
            LoomToolFactoryBridge.CreateInstrumentedAIFunctions(LoomGeneratedRegistry.RuntimeMetadata, sp));

        return services;
    }

    public static IServiceCollection AddLoomFactoryTools(
        this IServiceCollection services,
        LoomPhase phase)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<IReadOnlyList<AIFunction>>(sp =>
            LoomToolFactoryBridge.CreateAIFunctions(
                LoomGeneratedRegistry.RuntimeMetadata.Where(m => m.Phase == phase), sp));

        return services;
    }

    public static IServiceCollection AddLoomFactoryTools(
        this IServiceCollection services,
        string requiredCapability)
    {
        Guard.NotNull(services);
        Guard.NotNull(requiredCapability);

        services.TryAddSingleton<IReadOnlyList<AIFunction>>(sp =>
            LoomToolFactoryBridge.CreateAIFunctions(
                LoomGeneratedRegistry.RuntimeMetadata.Where(m =>
                    m.Policy.RequiredCapabilities.Contains(requiredCapability)), sp));

        return services;
    }

    public static IServiceCollection AddLoomToolDeclaringTypes(this IServiceCollection services)
    {
        Guard.NotNull(services);

        var registeredTypes = new HashSet<Type>();
        foreach (var metadata in LoomGeneratedRegistry.RuntimeMetadata)
        {
            if (registeredTypes.Add(metadata.DeclaringType))
                services.TryAddTransient(metadata.DeclaringType);
        }

        return services;
    }

    public static IServiceCollection AddLoomFactoryTool(
        this IServiceCollection services,
        string toolName)
    {
        Guard.NotNull(services);
        Guard.NotNull(toolName);

        var metadata = LoomGeneratedRegistry.RuntimeMetadata
                           .FirstOrDefault(m => string.Equals(m.Name, toolName, StringComparison.Ordinal))
                       ?? throw new ArgumentException($"No Loom tool found with name '{toolName}'.", nameof(toolName));

        services.AddSingleton(sp => LoomToolFactoryBridge.CreateAIFunction(metadata, sp));
        return services;
    }

    public static IServiceCollection AddLoomTools(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.AddLoomToolDeclaringTypes();
        services.AddInstrumentedLoomFactoryTools();
        return services;
    }
}
