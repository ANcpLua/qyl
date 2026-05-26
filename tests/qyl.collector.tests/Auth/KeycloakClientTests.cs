using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qyl.Collector.Auth;

namespace Qyl.Collector.Tests.Auth;

public sealed class KeycloakClientTests
{
    private const string Authority = "https://kc.example/realms/qyl";

    private const string DiscoveryJson = """
                                         {
                                             "issuer": "https://kc.example/realms/qyl",
                                             "authorization_endpoint": "https://kc.example/realms/qyl/protocol/openid-connect/auth",
                                             "token_endpoint": "https://kc.example/realms/qyl/protocol/openid-connect/token",
                                             "jwks_uri": "https://kc.example/realms/qyl/protocol/openid-connect/certs",
                                             "end_session_endpoint": "https://kc.example/realms/qyl/protocol/openid-connect/logout",
                                             "revocation_endpoint": "https://kc.example/realms/qyl/protocol/openid-connect/revoke"
                                         }
                                         """;

    [Fact]
    public async Task GetDiscoveryDocumentAsync_ParsesAllFields()
    {
        var handler = new CountingDiscoveryHandler(DiscoveryJson);
        var sut = CreateClient(handler, TimeProvider.System);

        var doc = await sut.GetDiscoveryDocumentAsync(TestContext.Current.CancellationToken);

        doc.Issuer.Should().Be("https://kc.example/realms/qyl");
        doc.AuthorizationEndpoint.Should().Be("https://kc.example/realms/qyl/protocol/openid-connect/auth");
        doc.TokenEndpoint.Should().Be("https://kc.example/realms/qyl/protocol/openid-connect/token");
        doc.JwksUri.Should().Be("https://kc.example/realms/qyl/protocol/openid-connect/certs");
        doc.EndSessionEndpoint.Should().Be("https://kc.example/realms/qyl/protocol/openid-connect/logout");
        doc.RevocationEndpoint.Should().Be("https://kc.example/realms/qyl/protocol/openid-connect/revoke");

        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDiscoveryDocumentAsync_HitsWellKnownPath()
    {
        var handler = new CountingDiscoveryHandler(DiscoveryJson);
        var sut = CreateClient(handler, TimeProvider.System);

        await sut.GetDiscoveryDocumentAsync(TestContext.Current.CancellationToken);

        handler.LastRequestUri!.AbsoluteUri.Should().Be($"{Authority}/.well-known/openid-configuration");
    }

    [Fact]
    public async Task GetDiscoveryDocumentAsync_ReturnsCachedWithinTtl_WithoutRefetching()
    {
        var handler = new CountingDiscoveryHandler(DiscoveryJson);
        var time = new TestTimeProvider(new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero));
        var sut = CreateClient(handler, time);

        var first = await sut.GetDiscoveryDocumentAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(30));
        var second = await sut.GetDiscoveryDocumentAsync(TestContext.Current.CancellationToken);

        handler.RequestCount.Should().Be(1, "second call within TTL must hit cache");
        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task GetDiscoveryDocumentAsync_RefetchesAfterCacheDurationExpires()
    {
        var handler = new CountingDiscoveryHandler(DiscoveryJson);
        var time = new TestTimeProvider(new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero));
        var sut = CreateClient(handler, time);

        await sut.GetDiscoveryDocumentAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await sut.GetDiscoveryDocumentAsync(TestContext.Current.CancellationToken);

        handler.RequestCount.Should().Be(2, "call past TTL must refetch from Keycloak");
    }

    [Fact]
    public async Task InvalidateDiscoveryDocument_ForcesRefetchOnNextCall()
    {
        var handler = new CountingDiscoveryHandler(DiscoveryJson);
        var sut = CreateClient(handler, TimeProvider.System);

        await sut.GetDiscoveryDocumentAsync(TestContext.Current.CancellationToken);
        sut.InvalidateDiscoveryDocument();
        await sut.GetDiscoveryDocumentAsync(TestContext.Current.CancellationToken);

        handler.RequestCount.Should().Be(2, "invalidation must drop the cache");
    }

    [Fact]
    public void Constructor_ThrowsWhenAuthorityIsEmpty()
    {
        var options = Options.Create(new KeycloakOptions { Authority = "" });
        var act = () => new KeycloakClient(
            new HttpClient(),
            options,
            TimeProvider.System,
            NullLogger<KeycloakClient>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*QYL_KEYCLOAK_AUTHORITY*");
    }

    private static KeycloakClient CreateClient(HttpMessageHandler handler, TimeProvider timeProvider)
    {
        var options = Options.Create(new KeycloakOptions { Authority = Authority });
        return new KeycloakClient(
            new HttpClient(handler),
            options,
            timeProvider,
            NullLogger<KeycloakClient>.Instance);
    }

    private sealed class CountingDiscoveryHandler : HttpMessageHandler
    {
        private readonly string _body;

        public CountingDiscoveryHandler(string body) => _body = body;

        public int RequestCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
