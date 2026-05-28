using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Functional;

[Trait("Category", "Functional")]
public sealed class McpTenantAuthTests : IClassFixture<McpTenantAuthTests.EnabledFactory>
{
    private const string Tenant = "acme";
    private readonly EnabledFactory _factory;

    public McpTenantAuthTests(EnabledFactory factory) => _factory = factory;

    [Fact]
    public async Task ValidToken_AtMatchingTenant_PassesAuthorization()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = await SeedTokenAsync(_factory, Tenant, ct);

        using var client = _factory.CreateClient();
        using var response = await client.SendAsync(McpPost($"/mcp/{Tenant}", token), ct);

        // The MCP protocol response itself is not the subject — only that the request
        // cleared the authorization gate (anything other than a 401).
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidToken_AtWrongTenant_Unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = await SeedTokenAsync(_factory, Tenant, ct);

        using var client = _factory.CreateClient();
        using var response = await client.SendAsync(McpPost("/mcp/other-tenant", token), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NoToken_Unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var response = await client.SendAsync(McpPost($"/mcp/{Tenant}", token: null), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokedToken_Unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = await SeedTokenAsync(_factory, Tenant, ct, revoked: true);

        using var client = _factory.CreateClient();
        using var response = await client.SendAsync(McpPost($"/mcp/{Tenant}", token), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedResourceMetadata_AdvertisesRealmIssuer()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            $"/.well-known/oauth-protected-resource/mcp/{Tenant}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var metadata = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        metadata.GetProperty("authorization_servers")[0].GetString()
            .Should().Be($"https://kc.test/realms/{Tenant}");
    }

    private static async Task<string> SeedTokenAsync(
        WebApplicationFactory<Program> factory, string tenant, CancellationToken ct, bool revoked = false)
    {
        var store = factory.Services.GetRequiredService<IMcpTokenStore>();
        var issued = await store.CreateAsync(
            new McpTokenCreate(
                UserId: "user-1",
                TenantId: tenant,
                Scopes: "openid profile",
                EncryptedRefresh: [],
                RefreshExpiresAt: TimeProvider.System.GetUtcNow().AddHours(1)),
            ct);

        if (revoked)
            await store.RevokeAsync(issued.TokenHash, ct);

        return issued.OpaqueToken;
    }

    // MapMcp registers the Streamable-HTTP message endpoint on POST (not GET in stateless mode),
    // so the authorization gate only fires for POST /mcp/{tenant}.
    private static HttpRequestMessage McpPost(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","method":"ping","id":1}""",
                new MediaTypeHeaderValue("application/json"))
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public sealed class EnabledFactory() : CollectorFunctionalFactory("mcp-tenant-auth")
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["QYL_MCP_TENANT_AUTH_ENABLED"] = "true",
                    ["QYL_KEYCLOAK_BASE_URL"] = "https://kc.test"
                }));
        }
    }
}

[Trait("Category", "Functional")]
public sealed class McpTenantAuthDisabledTests : IClassFixture<McpTenantAuthDisabledTests.DisabledFactory>
{
    private readonly DisabledFactory _factory;

    public McpTenantAuthDisabledTests(DisabledFactory factory) => _factory = factory;

    [Fact]
    public async Task FlagOff_TenantEndpoint_NotGated()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/acme")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","method":"ping","id":1}""",
                new MediaTypeHeaderValue("application/json"))
        };
        using var response = await client.SendAsync(request, ct);

        // Flag off → no authed /mcp/{tenant} endpoint is mapped, so an unauthenticated
        // request is never challenged (it falls through to the SPA fallback, not a 401).
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    public sealed class DisabledFactory() : CollectorFunctionalFactory("mcp-tenant-auth-off");
}
