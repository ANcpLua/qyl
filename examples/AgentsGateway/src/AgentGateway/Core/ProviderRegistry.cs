using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace AgentGateway.Core;

public sealed record RegisteredProvider(
    string Id,
    string DisplayName,
    ProviderCapabilities Capabilities,
    [property: JsonIgnore] Func<IServiceProvider, IChatClient> Factory,
    [property: JsonIgnore] Func<IServiceProvider, IModelCatalog?> CatalogFactory);

public interface IProviderRegistry
{
    IEnumerable<RegisteredProvider> All { get; }

    void Register(string providerId, string displayName, ProviderCapabilities caps,
        Func<IServiceProvider, IChatClient> factory, Func<IServiceProvider, IModelCatalog?> catalogFactory);

    bool TryGet(string providerId, out RegisteredProvider? provider);
    IChatClient Resolve(string providerId, IServiceProvider serviceProvider);
    IModelCatalog? ResolveCatalog(string providerId, IServiceProvider serviceProvider);
}

public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly Dictionary<string, RegisteredProvider> _providers = new();

    public void Register(string providerId, string displayName, ProviderCapabilities caps,
        Func<IServiceProvider, IChatClient> factory, Func<IServiceProvider, IModelCatalog?> catalogFactory)
    {
        _providers[providerId] = new RegisteredProvider(providerId, displayName, caps, factory, catalogFactory);
    }

    public bool TryGet(string providerId, out RegisteredProvider? provider) => _providers.TryGetValue(providerId, out provider);

    public IEnumerable<RegisteredProvider> All => _providers.Values;

    public IChatClient Resolve(string providerId, IServiceProvider serviceProvider)
    {
        if (_providers.TryGetValue(providerId, out var registered)) return registered.Factory(serviceProvider);
        throw new KeyNotFoundException($"Provider '{providerId}' not registered.");
    }

    public IModelCatalog? ResolveCatalog(string providerId, IServiceProvider serviceProvider)
    {
        if (_providers.TryGetValue(providerId, out var registered)) return registered.CatalogFactory(serviceProvider);
        return null;
    }
}

public static class ProviderDiscovery
{
    public static IServiceCollection AddDiscoveredAdapters(this IServiceCollection services, IConfiguration cfg,
        params Assembly[] scan)
    {
        var registry = new ProviderRegistry();
        services.AddSingleton<IProviderRegistry>(registry);

        var assemblies = scan.Length > 0
            ? scan
            : new[]
            {
                Assembly.GetExecutingAssembly()
            };
        foreach (var assembly in assemblies)
        foreach (var type in assembly.DefinedTypes)
            if (typeof(IChatClient).IsAssignableFrom(type) && type is
                {
                    IsInterface: false,
                    IsAbstract: false
                })
            {
                var meta = type.GetCustomAttribute<ModelProviderAttribute>();
                if (meta != null)
                    registry.Register(
                        meta.ProviderId,
                        meta.DisplayName,
                        meta.Capabilities,
                        sp => (IChatClient)ActivatorUtilities.CreateInstance(sp, type),
                        sp => typeof(IModelCatalog).IsAssignableFrom(type)
                            ? (IModelCatalog)ActivatorUtilities.CreateInstance(sp, type)
                            : null
                    );
            }

        return services;
    }
}
