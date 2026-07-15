using Qyl.Collector.Cost;
using Qyl.Collector.Grpc;

namespace Qyl.Collector.Hosting;

internal static class CollectorCoreExtensions
{
    public static CollectorPortOptions AddQylCollectorCore(
        this IServiceCollection services,
        IConfiguration config)
    {
        var ports = CollectorPortOptions.FromConfiguration(config);
        services.AddSingleton(ports);
        services.AddSingleton<CollectorStreamCapacity>();

        var providerCostOptions = ProviderCostSyncOptions.FromConfiguration(config);
        services.AddSingleton(providerCostOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProviderCostSource>(static provider =>
            new OpenAiOrganizationCostsSource(
                new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                provider.GetRequiredService<TimeProvider>(),
                new OpenAiOrganizationCostsOptions(
                    provider.GetRequiredService<ProviderCostSyncOptions>().OpenAiAdminKey,
                    provider.GetRequiredService<ProviderCostSyncOptions>().OpenAiProjectId)));
        services.AddSingleton<IProviderCostSource>(static provider =>
            new AnthropicCostReportSource(
                new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                provider.GetRequiredService<TimeProvider>(),
                new AnthropicCostReportOptions(
                    provider.GetRequiredService<ProviderCostSyncOptions>().AnthropicAdminKey,
                    provider.GetRequiredService<ProviderCostSyncOptions>().AnthropicWorkspaceScope)));
        services.AddSingleton<GenAiEtlAuditService>();
        services.AddHostedService<ProviderCostSyncService>();

        var modelPricingOptions = ModelPricingCatalogOptions.FromConfiguration(config);
        var openRouterOptions = OpenRouterModelPricingCatalogOptions.FromConfiguration(config);
        services.AddSingleton(modelPricingOptions);
        services.AddSingleton(provider =>
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CheckCertificateRevocationList = true
            };
            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = modelPricingOptions.HttpTimeout,
                MaxResponseContentBufferSize = modelPricingOptions.MaximumResponseBytes
            };
            return new OpenRouterModelPricingCatalogSource(
                client,
                provider.GetRequiredService<TimeProvider>(),
                openRouterOptions,
                modelPricingOptions.MaximumResponseBytes);
        });

        services.AddSingleton<ModelPricingCatalogRepository>();
        services.AddSingleton<GenAiEtlCatalogEstimator>();
        services.AddSingleton<ModelPricingCatalogStateService>();
        services.AddSingleton<ModelPricingCatalogRefreshService>();
        services.AddHostedService(static provider =>
            provider.GetRequiredService<ModelPricingCatalogRefreshService>());

        services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default);
        });

        services.AddGrpc(static options =>
        {
            options.ResponseCompressionLevel = CompressionLevel.Optimal;
            options.ResponseCompressionAlgorithm = "gzip";
            // The gRPC half of the API-key boundary; the HTTP half is CollectorApiKeyMiddleware.
            options.Interceptors.Add<OtlpApiKeyInterceptor>();
        });
        services.AddSingleton<OtlpApiKeyInterceptor>();

        return ports;
    }
}
