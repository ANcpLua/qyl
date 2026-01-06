using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using AgentGateway.Core;
using Xunit;

namespace AgentGateway.Tests;

public class InfrastructureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InfrastructureTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ChatClient_Is_Registered_As_ProviderRouter()
    {
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IChatClient>();

        Assert.IsType<ProviderRouterChatClient>(client);
    }

    [Fact]
    public void ProviderRegistry_Contains_OpenAI_And_GitHub()
    {
        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderRegistry>();

        Assert.Contains(registry.All, p => p.Id == "openai");
        Assert.Contains(registry.All, p => p.Id == "github");
        Assert.Contains(registry.All, p => p.Id == "ollama");
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Healthy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task Catalog_Endpoint_Returns_Providers()
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