using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qyl.Collector.Auth;
using Qyl.Collector.Hosting;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Functional;

/// <summary>
/// Stage E1.e integration coverage — drives the real collector wiring
/// (Kestrel + DI + middleware + endpoints + DuckDB + AES-GCM) via
/// <see cref="WebApplicationFactory{TEntryPoint}"/>, with only the
/// Keycloak HTTP boundary replaced by stubs.
/// </summary>
/// <remarks>
/// <para>
/// Why not Testcontainers.Keycloak: the PRD originally specified booting a
/// real Keycloak in a Docker container for ~20s/run + Docker-on-runner
/// requirements. The trade-off: real JWT signature verification (which is
/// already covered by <c>KeycloakJwksValidator</c> unit tests and the
/// callback unit tests) vs ~200x faster CI feedback on the wiring
/// integration. The wrapped <see cref="NonceCapturingPkceStore"/> lets
/// the stub <see cref="IKeycloakJwksValidator"/> return the nonce that
/// the real <c>PkceStateStore</c> persisted, without raw DB access.
/// </para>
/// </remarks>
public sealed class AuthFlowIntegrationTests : IClassFixture<AuthFlowIntegrationTests.AuthFlowFactory>
{
    private readonly AuthFlowFactory _factory;

    public AuthFlowIntegrationTests(AuthFlowFactory factory) => _factory = factory;

    [Fact]
    public async Task FullFlow_Authorize_Callback_Refresh_Revoke_Then_Refresh_Fails()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // 1) GET /auth/authorize — captures state from the redirect.
        var authResp = await client.GetAsync(
            "/auth/authorize?tenant=demo&redirect_uri=https%3A%2F%2Fapp.test%2Fcb",
            TestContext.Current.CancellationToken);
        authResp.StatusCode.Should().Be(HttpStatusCode.Found);
        var authLocation = authResp.Headers.Location!.ToString();
        authLocation.Should().StartWith("https://kc.stub/authorize");

        var queryParams = QueryHelpers.ParseQuery(authLocation[(authLocation.IndexOf('?') + 1)..]);
        var state = queryParams["state"].ToString();
        state.Should().NotBeNullOrEmpty();

        // 2) Stub Keycloak says: yes, this code is good; here are tokens.
        _factory.Keycloak.NextExchangeTokens = new KeycloakTokenResponse(
            AccessToken: "fake-access",
            IdToken: "stub.id.token",
            RefreshToken: "kc-refresh-v1",
            ExpiresIn: 300,
            RefreshExpiresIn: 3600,
            TokenType: "Bearer",
            Scope: "openid profile email offline_access");

        // 3) Stub Jwks: claims include the actual stored nonce.
        var capturedNonce = _factory.NonceCapture.LastNonce
            ?? throw new InvalidOperationException("NonceCapturingPkceStore did not see a Store call");
        _factory.Jwks.NextClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sub"] = "user-int-1",
            ["nonce"] = capturedNonce,
            ["aud"] = "qyl-collector",
            ["iss"] = "https://kc.stub/realms/qyl",
        };

        // 4) GET /auth/callback — drives token exchange + opaque mint + 302.
        var callbackResp = await client.GetAsync(
            $"/auth/callback?state={Uri.EscapeDataString(state)}&code=fake-auth-code",
            TestContext.Current.CancellationToken);
        callbackResp.StatusCode.Should().Be(HttpStatusCode.Found);

        var callbackLoc = callbackResp.Headers.Location!.ToString();
        callbackLoc.Should().StartWith("https://app.test/cb#",
            "opaque token MUST land in URL fragment, never query");
        var opaque = ExtractFragmentParam(callbackLoc, "token");
        opaque.Should().NotBeNullOrEmpty();

        // 5) POST /auth/refresh with the opaque bearer.
        _factory.Keycloak.NextExchangeTokens = new KeycloakTokenResponse(
            AccessToken: "fake-access-2",
            IdToken: "stub.id.token.2",
            RefreshToken: "kc-refresh-v2",
            ExpiresIn: 300,
            RefreshExpiresIn: 7200,
            TokenType: "Bearer",
            Scope: "openid profile email offline_access");
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        refreshReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opaque);
        var refreshResp = await client.SendAsync(refreshReq, TestContext.Current.CancellationToken);
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refreshResp.Content.ReadFromJsonAsync<RefreshResponse>(
            TestContext.Current.CancellationToken);
        refreshBody!.ExpiresAt.Should().NotBeNullOrWhiteSpace();

        // 6) POST /auth/revoke → 204.
        var revokeReq = new HttpRequestMessage(HttpMethod.Post, "/auth/revoke");
        revokeReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opaque);
        var revokeResp = await client.SendAsync(revokeReq, TestContext.Current.CancellationToken);
        revokeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7) POST /auth/refresh after revoke → 401 (token unknown).
        var refreshAfterRevoke = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        refreshAfterRevoke.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opaque);
        var refreshAfterResp = await client.SendAsync(refreshAfterRevoke, TestContext.Current.CancellationToken);
        refreshAfterResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authorize_RejectsUnknownRedirectUri_400()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var resp = await client.GetAsync(
            "/auth/authorize?tenant=demo&redirect_uri=https%3A%2F%2Fevil.test%2Fsteal",
            TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_WithoutPriorAuthorize_400_InvalidState()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        _factory.Keycloak.NextExchangeTokens = new KeycloakTokenResponse(
            AccessToken: "x", IdToken: "x", RefreshToken: "x",
            ExpiresIn: 1, RefreshExpiresIn: 1, TokenType: "Bearer", Scope: null);

        var resp = await client.GetAsync(
            "/auth/callback?state=never-stored&code=x",
            TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_WithoutBearer_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/auth/refresh", content: null, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string ExtractFragmentParam(string url, string key)
    {
        var fragment = url[(url.IndexOf('#') + 1)..];
        foreach (var pair in fragment.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0 && pair[..eq] == key)
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        throw new InvalidOperationException($"Fragment param '{key}' not present in {url}");
    }

    // ── factory + stubs ─────────────────────────────────────────────────────

    public sealed class AuthFlowFactory : CollectorFunctionalFactory
    {
        public StubKeycloakClient Keycloak { get; } = new();
        public StubJwksValidator Jwks { get; } = new();
        public NonceCapturingPkceStore NonceCapture { get; private set; } = null!;

        private static readonly string TestKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        public AuthFlowFactory() : base("auth-flow") { }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                // Re-Configure runs AFTER AddQylCollectorAuth's empty-config
                // capture; IOptions resolution applies ALL callbacks in order
                // (last one wins per-property), so this populates the values.
                services.Configure<KeycloakOptions>(opts =>
                {
                    opts.Authority = "https://kc.stub/realms/qyl";
                    opts.Audience = "qyl-collector";
                    opts.ClientId = "qyl-collector";
                    opts.AllowedRedirects = ["https://app.test/cb"];
                });

                services.Configure<TokenEncryptionOptions>(opts => opts.Key = TestKey);

                services.RemoveAll<IKeycloakClient>();
                services.AddSingleton<IKeycloakClient>(Keycloak);

                services.RemoveAll<IKeycloakJwksValidator>();
                services.AddSingleton<IKeycloakJwksValidator>(Jwks);

                // Decorate the real PkceStateStore so the test can read the nonce
                // it persisted, without raw DB access.
                services.RemoveAll<IPkceStateStore>();
                services.AddSingleton<IPkceStateStore>(sp =>
                {
                    var inner = new PkceStateStore(
                        sp.GetRequiredService<DuckDbStore>(),
                        sp.GetRequiredService<TimeProvider>());
                    NonceCapture = new NonceCapturingPkceStore(inner);
                    return NonceCapture;
                });
            });
        }
    }

    public sealed class StubKeycloakClient : IKeycloakClient
    {
        public KeycloakTokenResponse? NextExchangeTokens { get; set; }

        public Task<KeycloakDiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken ct) =>
            Task.FromResult(new KeycloakDiscoveryDocument(
                AuthorizationEndpoint: "https://kc.stub/authorize",
                TokenEndpoint: "https://kc.stub/token",
                JwksUri: "https://kc.stub/certs",
                EndSessionEndpoint: "https://kc.stub/logout",
                Issuer: "https://kc.stub/realms/qyl",
                RevocationEndpoint: "https://kc.stub/revoke"));

        public Task<KeycloakTokenResponse?> ExchangeAuthorizationCodeAsync(
            string code, string codeVerifier, string redirectUri, CancellationToken ct) =>
            Task.FromResult(NextExchangeTokens);

        public Task<KeycloakTokenResponse?> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct) =>
            Task.FromResult(NextExchangeTokens);

        public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct) => Task.CompletedTask;

        public void InvalidateDiscoveryDocument() { }
    }

    public sealed class StubJwksValidator : IKeycloakJwksValidator
    {
        public IReadOnlyDictionary<string, string>? NextClaims { get; set; }

        public ValueTask<IReadOnlyDictionary<string, string>?> ValidateAsync(
            string token, CancellationToken ct = default) =>
            ValueTask.FromResult(NextClaims);
    }

    /// <summary>
    /// Decorator over the real <see cref="PkceStateStore"/> that records the
    /// nonce passed into <c>StoreAsync</c> so an integration test's stub
    /// JWKS validator can echo it back as a claim — letting the real
    /// callback's nonce-binding check pass without bypassing it.
    /// </summary>
    public sealed class NonceCapturingPkceStore : IPkceStateStore
    {
        private readonly IPkceStateStore _inner;
        public string? LastNonce { get; private set; }

        public NonceCapturingPkceStore(IPkceStateStore inner) => _inner = inner;

        public Task StoreAsync(string state, string codeVerifier, string tenantId,
            string clientRedirectUri, string nonce, TimeSpan ttl, CancellationToken ct)
        {
            LastNonce = nonce;
            return _inner.StoreAsync(state, codeVerifier, tenantId, clientRedirectUri, nonce, ttl, ct);
        }

        public Task<PkceStateRecord?> ConsumeAsync(string state, CancellationToken ct) =>
            _inner.ConsumeAsync(state, ct);

        public Task<int> CleanupExpiredAsync(CancellationToken ct) =>
            _inner.CleanupExpiredAsync(ct);
    }
}
