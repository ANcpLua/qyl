using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qyl.Collector.Auth;
using Qyl.Collector.Hosting;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Auth;

public sealed class AuthorizeEndpointTests
{
    private const string TestAuthorizationEndpoint = "https://kc.test/realms/qyl/protocol/openid-connect/auth";
    private const string TestClientId = "qyl-collector";

    private static KeycloakOptions ConfiguredOptions(params string[] allowedRedirects) => new()
    {
        Authority = "https://kc.test/realms/qyl",
        Audience = TestClientId,
        ClientId = TestClientId,
        AllowedRedirects = allowedRedirects,
        PkceStateTtl = TimeSpan.FromMinutes(10),
    };

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("collector.test");
        return context;
    }

    [Fact]
    public async Task Authorize_ReturnsBadRequest_WhenTenantMissing()
    {
        var result = await AuthEndpoints.AuthorizeAsync(
            tenant: null, redirect_uri: "https://app.test/cb",
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            pkceStore: new StubPkceStateStore(),
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<BadRequest<ErrorResponse>>()
            .Which.Value!.Error.Should().Be("invalid_request");
    }

    [Fact]
    public async Task Authorize_ReturnsBadRequest_WhenRedirectUriMissing()
    {
        var result = await AuthEndpoints.AuthorizeAsync(
            tenant: "demo", redirect_uri: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            pkceStore: new StubPkceStateStore(),
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<BadRequest<ErrorResponse>>()
            .Which.Value!.Error.Should().Be("invalid_request");
    }

    [Fact]
    public async Task Authorize_Returns503_WhenKeycloakUnconfigured()
    {
        // Authority empty → IsEnabled = false
        var result = await AuthEndpoints.AuthorizeAsync(
            tenant: "demo", redirect_uri: "https://app.test/cb",
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            pkceStore: new StubPkceStateStore(),
            optionsAccessor: Options.Create(new KeycloakOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Authorize_RejectsRedirectUriNotInAllowlist()
    {
        var result = await AuthEndpoints.AuthorizeAsync(
            tenant: "demo", redirect_uri: "https://evil.test/steal",
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            pkceStore: new StubPkceStateStore(),
            optionsAccessor: Options.Create(ConfiguredOptions("https://app.test/cb")),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<BadRequest<ErrorResponse>>()
            .Which.Value!.Error.Should().Be("redirect_uri_not_allowed");
    }

    [Fact]
    public async Task Authorize_HappyPath_Returns302WithRequiredPkceParams()
    {
        var pkce = new StubPkceStateStore();
        var result = await AuthEndpoints.AuthorizeAsync(
            tenant: "demo",
            redirect_uri: "https://app.test/cb",
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            pkceStore: pkce,
            optionsAccessor: Options.Create(ConfiguredOptions("https://app.test/cb")),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        var redirect = result.Should().BeOfType<RedirectHttpResult>().Which;
        redirect.Url.Should().StartWith(TestAuthorizationEndpoint);

        var queryString = redirect.Url[(redirect.Url.IndexOf('?') + 1)..];
        var qs = QueryHelpers.ParseQuery(queryString);

        qs["response_type"].ToString().Should().Be("code");
        qs["client_id"].ToString().Should().Be(TestClientId);
        qs["redirect_uri"].ToString().Should().Be("https://collector.test/auth/callback",
            "/auth/callback is the collector's own callback, NOT the client redirect — distinction matters for spec compliance");
        qs["scope"].ToString().Should().Be("openid profile email offline_access");
        qs["code_challenge_method"].ToString().Should().Be("S256");
        qs.Should().ContainKeys("state", "code_challenge", "nonce");

        qs["state"].ToString().Length.Should().BeGreaterThanOrEqualTo(32,
            "state is 32-byte base64url = >=43 chars");
        qs["code_challenge"].ToString().Length.Should().Be(43,
            "S256 challenge is always 32-byte SHA256 → base64url = 43 chars no padding");
        qs["nonce"].ToString().Length.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public async Task Authorize_StoresPkceRow_BeforeRedirect_NoRace()
    {
        var pkce = new StubPkceStateStore();
        var result = await AuthEndpoints.AuthorizeAsync(
            tenant: "demo",
            redirect_uri: "https://app.test/cb",
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            pkceStore: pkce,
            optionsAccessor: Options.Create(ConfiguredOptions("https://app.test/cb")),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        pkce.Stored.Should().HaveCount(1,
            "PRD invariant: PKCE state row must be persisted BEFORE the redirect is returned");
        var stored = pkce.Stored[0];
        stored.TenantId.Should().Be("demo");
        stored.ClientRedirectUri.Should().Be("https://app.test/cb");
        stored.Ttl.Should().Be(TimeSpan.FromMinutes(10));
        stored.CodeVerifier.Length.Should().BeInRange(43, 128,
            "RFC 7636 §4.1: code_verifier MUST be 43-128 URL-safe chars");

        // The state stored should match the state returned in the redirect URL.
        var redirect = result.Should().BeOfType<RedirectHttpResult>().Which;
        var qs = QueryHelpers.ParseQuery(redirect.Url[(redirect.Url.IndexOf('?') + 1)..]);
        qs["state"].ToString().Should().Be(stored.State);

        // The code_challenge in the redirect is the SHA256(code_verifier) — verify the binding.
        var expectedChallenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(stored.CodeVerifier)))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        qs["code_challenge"].ToString().Should().Be(expectedChallenge,
            "code_challenge must be S256 of the stored code_verifier");
    }

    [Fact]
    public async Task Authorize_AllowsAnyRedirect_WhenAllowlistEmpty()
    {
        // Dev mode: empty allowlist means pass-through.
        var result = await AuthEndpoints.AuthorizeAsync(
            tenant: "demo", redirect_uri: "http://localhost:9000/cb",
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            pkceStore: new StubPkceStateStore(),
            optionsAccessor: Options.Create(ConfiguredOptions()),  // no allowedRedirects
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<RedirectHttpResult>();
    }

    // ── Test doubles ────────────────────────────────────────────────────────

    private sealed class StubKeycloakClient : IKeycloakClient
    {
        public Task<KeycloakDiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken ct) =>
            Task.FromResult(new KeycloakDiscoveryDocument(
                AuthorizationEndpoint: TestAuthorizationEndpoint,
                TokenEndpoint: "https://kc.test/realms/qyl/protocol/openid-connect/token",
                JwksUri: "https://kc.test/realms/qyl/protocol/openid-connect/certs",
                EndSessionEndpoint: "https://kc.test/realms/qyl/protocol/openid-connect/logout",
                Issuer: "https://kc.test/realms/qyl",
                RevocationEndpoint: "https://kc.test/realms/qyl/protocol/openid-connect/revoke"));

        public Task<KeycloakTokenResponse?> ExchangeAuthorizationCodeAsync(
            string code, string codeVerifier, string redirectUri, CancellationToken ct) =>
            Task.FromResult<KeycloakTokenResponse?>(null);

        public Task<KeycloakTokenResponse?> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct) =>
            Task.FromResult<KeycloakTokenResponse?>(null);

        public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct) => Task.CompletedTask;

        public void InvalidateDiscoveryDocument() { }
    }

    private sealed record StoredPkce(string State, string CodeVerifier, string TenantId, string ClientRedirectUri, string Nonce, TimeSpan Ttl);

    private sealed class StubPkceStateStore : IPkceStateStore
    {
        public List<StoredPkce> Stored { get; } = [];

        public Task StoreAsync(
            string state, string codeVerifier, string tenantId,
            string clientRedirectUri, string nonce, TimeSpan ttl, CancellationToken ct)
        {
            Stored.Add(new StoredPkce(state, codeVerifier, tenantId, clientRedirectUri, nonce, ttl));
            return Task.CompletedTask;
        }

        public Task<PkceStateRecord?> ConsumeAsync(string state, CancellationToken ct) =>
            Task.FromResult<PkceStateRecord?>(null);

        public Task<int> CleanupExpiredAsync(CancellationToken ct) => Task.FromResult(0);
    }
}
