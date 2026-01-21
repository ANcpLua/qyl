using AgentGateway.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentGateway.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Add test configuration that overrides the main app's config
            var testConfig = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(testConfig)) config.AddJsonFile(testConfig, false);
        });
    }
}

public class InfrastructureTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public InfrastructureTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void ChatClientIsRegisteredAsProviderRouter()
    {
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IChatClient>();

        Assert.IsType<ProviderRouterChatClient>(client);
    }

    [Fact]
    public void ProviderRegistryContainsOpenAiAndGitHub()
    {
        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderRegistry>();

        Assert.Contains(registry.All, p => p.Id == "openai");
        Assert.Contains(registry.All, p => p.Id == "github");
        Assert.Contains(registry.All, p => p.Id == "ollama");
    }

    [Fact]
    public async Task HealthEndpointReturnsHealthy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task CatalogEndpointReturnsProviders()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/catalog/providers", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Should use the AOT JSON serializer, so checking for key json properties
        Assert.Contains("openai", content);
        Assert.Contains("github", content);
    }
}
