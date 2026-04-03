using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Qyl.Instrumentation.Instrumentation.Loom;

public static class LoomToolServiceExtensions
{
    public static IServiceCollection AddLoomFactoryTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IReadOnlyList<AIFunction>>(static sp =>
            LoomToolFactoryBridge.CreateAIFunctions(LoomGeneratedRegistry.RuntimeMetadata, sp));

        return services;
    }

    public static IServiceCollection AddInstrumentedLoomFactoryTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IReadOnlyList<AIFunction>>(static sp =>
            LoomToolFactoryBridge.CreateInstrumentedAIFunctions(LoomGeneratedRegistry.RuntimeMetadata, sp));

        return services;
    }

    public static IServiceCollection AddLoomFactoryTools(
        this IServiceCollection services,
        LoomPhase phase)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IReadOnlyList<AIFunction>>(sp =>
            LoomToolFactoryBridge.CreateAIFunctions(
                LoomGeneratedRegistry.RuntimeMetadata.Where(m => m.Phase == phase), sp));

        return services;
    }

    public static IServiceCollection AddLoomFactoryTools(
        this IServiceCollection services,
        string requiredCapability)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(requiredCapability);

        services.TryAddSingleton<IReadOnlyList<AIFunction>>(sp =>
            LoomToolFactoryBridge.CreateAIFunctions(
                LoomGeneratedRegistry.RuntimeMetadata.Where(m =>
                    m.Policy.RequiredCapabilities.Contains(requiredCapability)), sp));

        return services;
    }

    public static IServiceCollection AddLoomToolDeclaringTypes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(toolName);

        var metadata = LoomGeneratedRegistry.RuntimeMetadata
            .FirstOrDefault(m => string.Equals(m.Name, toolName, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No Loom tool found with name '{toolName}'.", nameof(toolName));

        services.AddSingleton(sp => LoomToolFactoryBridge.CreateAIFunction(metadata, sp));
        return services;
    }

    public static IServiceCollection AddLoomTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLoomToolDeclaringTypes();
        services.AddInstrumentedLoomFactoryTools();
        return services;
    }
}
