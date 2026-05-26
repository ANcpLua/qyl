using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qyl.Collector.Auth;
using Qyl.Collector.Hosting;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Auth;

/// <summary>
/// Stage E1.c coverage. Uses real <see cref="PkceStateStore"/>, real
/// <see cref="McpTokenStore"/>, and real <see cref="AesGcmTokenEncryption"/>
/// so the tests exercise the same SQL + crypto code that ships in production;
/// only the Keycloak HTTP surface (<see cref="IKeycloakClient"/>) and the
/// JWKS validator (<see cref="IKeycloakJwksValidator"/>) are stubbed.
/// </summary>
public sealed class CallbackEndpointTests
{
    private const string TestClientId = "qyl-collector";
    private const string TestClientRedirect = "https://app.test/cb";
    private const string TestTenant = "demo";
    private const string TestUserSub = "user-42";
    private const string TestNonce = "nonce-xyz-128bit";
    private const string TestCode = "kc-auth-code";
    private const string TestRefreshToken = "kc-refresh-jwt-payload";
    private static readonly string TestEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static KeycloakOptions ConfiguredOptions() => new()
    {
        Authority = "https://kc.test/realms/qyl",
        Audience = TestClientId,
        ClientId = TestClientId,
        PkceStateTtl = TimeSpan.FromMinutes(10),
    };

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("collector.test");
        return context;
    }

    private static AesGcmTokenEncryption NewEncryption() =>
        new(Options.Create(new TokenEncryptionOptions { Key = TestEncryptionKey }));

    private static KeycloakTokenResponse SuccessfulTokens(string idToken = "fake.id.token") =>
        new(
            AccessToken: "fake-access",
            IdToken: idToken,
            RefreshToken: TestRefreshToken,
            ExpiresIn: 300,
            RefreshExpiresIn: 1800,
            TokenType: "Bearer",
            Scope: "openid profile email offline_access");

    private static Dictionary<string, string> ValidClaims(string nonce = TestNonce, string sub = TestUserSub) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["sub"] = sub,
            ["nonce"] = nonce,
            ["aud"] = TestClientId,
            ["iss"] = "https://kc.test/realms/qyl",
        };

    private static async Task<string> StorePkceRowAsync(IPkceStateStore store, string state)
    {
        const string verifier = "verifier-for-callback-tests-43-or-more-chars-aaaaa";
        await store.StoreAsync(
            state: state,
            codeVerifier: verifier,
            tenantId: TestTenant,
            clientRedirectUri: TestClientRedirect,
            nonce: TestNonce,
            ttl: TimeSpan.FromMinutes(10),
            ct: TestContext.Current.CancellationToken);
        return verifier;
    }

    // ── error / validation paths ────────────────────────────────────────────

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenAuthorizationError()
    {
        await using var db = new DuckDbStore(":memory:");
        var pkce = new PkceStateStore(db, TimeProvider.System);
        var tokens = new McpTokenStore(db, TimeProvider.System);

        var result = await AuthEndpoints.CallbackAsync(
            state: "ignored", code: null,
            error: "access_denied", errorDescription: "user denied",
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            jwksValidator: new StubJwksValidator(),
            pkceStore: pkce, tokenStore: tokens,
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<BadRequest<ErrorResponse>>()
            .Which.Value!.Error.Should().Be("access_denied");
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenStateMissing()
    {
        await using var db = new DuckDbStore(":memory:");
        var result = await AuthEndpoints.CallbackAsync(
            state: null, code: "abc", error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            jwksValidator: new StubJwksValidator(),
            pkceStore: new PkceStateStore(db, TimeProvider.System),
            tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<BadRequest<ErrorResponse>>()
            .Which.Value!.Error.Should().Be("invalid_request");
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenCodeMissing()
    {
        await using var db = new DuckDbStore(":memory:");
        var result = await AuthEndpoints.CallbackAsync(
            state: "abc", code: null, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            jwksValidator: new StubJwksValidator(),
            pkceStore: new PkceStateStore(db, TimeProvider.System),
            tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<BadRequest<ErrorResponse>>()
            .Which.Value!.Error.Should().Be("invalid_request");
    }

    [Fact]
    public async Task Callback_Returns503_WhenKeycloakUnconfigured()
    {
        await using var db = new DuckDbStore(":memory:");
        var result = await AuthEndpoints.CallbackAsync(
            state: "abc", code: "xyz", error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient(),
            jwksValidator: new StubJwksValidator(),
            pkceStore: new PkceStateStore(db, TimeProvider.System),
            tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(new KeycloakOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenPkceStateNotFound()
    {
        await using var db = new DuckDbStore(":memory:");
        var result = await AuthEndpoints.CallbackAsync(
            state: "never-stored", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = ValidClaims() },
            pkceStore: new PkceStateStore(db, TimeProvider.System),
            tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<BadRequest<ErrorResponse>>()
            .Which.Value!.Error.Should().Be("invalid_state");
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenPkceStateExpired()
    {
        await using var db = new DuckDbStore(":memory:");
        var time = new TestTimeProvider(new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero));
        var pkce = new PkceStateStore(db, time);
        await pkce.StoreAsync("expiring", "v", TestTenant, TestClientRedirect, TestNonce,
            TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);

        time.Advance(TimeSpan.FromMinutes(3));

        var result = await AuthEndpoints.CallbackAsync(
            state: "expiring", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = ValidClaims() },
            pkceStore: pkce, tokenStore: new McpTokenStore(db, time),
            encryption: NewEncryption(),
            timeProvider: time,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<BadRequest<ErrorResponse>>()
            .Which.Value!.Error.Should().Be("invalid_state");
    }

    [Fact]
    public async Task Callback_ReturnsUnauthorized_WhenTokenExchangeRejected()
    {
        await using var db = new DuckDbStore(":memory:");
        var pkce = new PkceStateStore(db, TimeProvider.System);
        await StorePkceRowAsync(pkce, "tx-rejected");

        var result = await AuthEndpoints.CallbackAsync(
            state: "tx-rejected", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = null },
            jwksValidator: new StubJwksValidator { Claims = ValidClaims() },
            pkceStore: pkce, tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Callback_ReturnsUnauthorized_WhenJwksValidatorRejects()
    {
        await using var db = new DuckDbStore(":memory:");
        var pkce = new PkceStateStore(db, TimeProvider.System);
        await StorePkceRowAsync(pkce, "jwt-bad");

        var result = await AuthEndpoints.CallbackAsync(
            state: "jwt-bad", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = null },
            pkceStore: pkce, tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Callback_ReturnsUnauthorized_WhenNonceMismatch()
    {
        await using var db = new DuckDbStore(":memory:");
        var pkce = new PkceStateStore(db, TimeProvider.System);
        await StorePkceRowAsync(pkce, "nonce-bad");

        var result = await AuthEndpoints.CallbackAsync(
            state: "nonce-bad", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = ValidClaims(nonce: "ATTACKER-REPLAYED-NONCE") },
            pkceStore: pkce, tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Callback_ReturnsUnauthorized_WhenSubClaimMissing()
    {
        await using var db = new DuckDbStore(":memory:");
        var pkce = new PkceStateStore(db, TimeProvider.System);
        await StorePkceRowAsync(pkce, "sub-missing");

        var claimsNoSub = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nonce"] = TestNonce,
            ["aud"] = TestClientId,
            ["iss"] = "https://kc.test/realms/qyl",
        };

        var result = await AuthEndpoints.CallbackAsync(
            state: "sub-missing", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = claimsNoSub },
            pkceStore: pkce, tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    // ── happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Callback_HappyPath_RedirectsToClientWithTokenInFragment()
    {
        await using var db = new DuckDbStore(":memory:");
        var pkce = new PkceStateStore(db, TimeProvider.System);
        var tokens = new McpTokenStore(db, TimeProvider.System);
        await StorePkceRowAsync(pkce, "ok-1");

        var result = await AuthEndpoints.CallbackAsync(
            state: "ok-1", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = ValidClaims() },
            pkceStore: pkce, tokenStore: tokens,
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        var redirect = result.Should().BeOfType<RedirectHttpResult>().Which;
        var fragmentStart = redirect.Url.IndexOf('#');
        fragmentStart.Should().BeGreaterThan(0,
            "opaque token MUST be delivered in URL fragment, never query (proxy log leak prevention)");
        redirect.Url[..fragmentStart].Should().Be(TestClientRedirect);

        var fragment = redirect.Url[(fragmentStart + 1)..];
        fragment.Should().Contain("token=").And.Contain("&expires_at=");
    }

    [Fact]
    public async Task Callback_HappyPath_OpaqueTokenLookupSucceeds()
    {
        await using var db = new DuckDbStore(":memory:");
        var pkce = new PkceStateStore(db, TimeProvider.System);
        var tokens = new McpTokenStore(db, TimeProvider.System);
        await StorePkceRowAsync(pkce, "ok-2");

        var result = await AuthEndpoints.CallbackAsync(
            state: "ok-2", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = ValidClaims() },
            pkceStore: pkce, tokenStore: tokens,
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        var redirect = result.Should().BeOfType<RedirectHttpResult>().Which;
        var opaque = ExtractFragmentParam(redirect.Url, "token");

        var stored = await tokens.GetByOpaqueTokenAsync(opaque, TestContext.Current.CancellationToken);
        stored.Should().NotBeNull("the minted opaque token must round-trip through IMcpTokenStore");
        stored.UserId.Should().Be(TestUserSub);
        stored.TenantId.Should().Be(TestTenant);
        stored.Scopes.Should().Be("openid profile email offline_access");
    }

    [Fact]
    public async Task Callback_HappyPath_RefreshTokenIsEncryptedAtRest_DecryptsBack()
    {
        await using var db = new DuckDbStore(":memory:");
        var pkce = new PkceStateStore(db, TimeProvider.System);
        var tokens = new McpTokenStore(db, TimeProvider.System);
        var encryption = NewEncryption();
        await StorePkceRowAsync(pkce, "ok-3");

        var result = await AuthEndpoints.CallbackAsync(
            state: "ok-3", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = ValidClaims() },
            pkceStore: pkce, tokenStore: tokens, encryption: encryption,
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        var opaque = ExtractFragmentParam(((RedirectHttpResult)result).Url, "token");
        var stored = await tokens.GetByOpaqueTokenAsync(opaque, TestContext.Current.CancellationToken);
        stored.Should().NotBeNull();

        var decrypted = Encoding.UTF8.GetString(encryption.Decrypt(stored.EncryptedRefresh));
        decrypted.Should().Be(TestRefreshToken,
            "the stored envelope MUST decrypt back to the original Keycloak refresh_token");
    }

    [Fact]
    public async Task Callback_HappyPath_ConsumesPkceRow_SecondCallbackFails()
    {
        await using var db = new DuckDbStore(":memory:");
        var pkce = new PkceStateStore(db, TimeProvider.System);
        var tokens = new McpTokenStore(db, TimeProvider.System);
        await StorePkceRowAsync(pkce, "ok-4");

        // First call: happy path
        var first = await AuthEndpoints.CallbackAsync(
            state: "ok-4", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = ValidClaims() },
            pkceStore: pkce, tokenStore: tokens,
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);
        first.Should().BeOfType<RedirectHttpResult>();

        // Second call: same state, must fail because PKCE row was consumed
        var second = await AuthEndpoints.CallbackAsync(
            state: "ok-4", code: TestCode, error: null, errorDescription: null,
            context: NewContext(),
            keycloak: new StubKeycloakClient { Tokens = SuccessfulTokens() },
            jwksValidator: new StubJwksValidator { Claims = ValidClaims() },
            pkceStore: pkce, tokenStore: tokens,
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            optionsAccessor: Options.Create(ConfiguredOptions()),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);
        second.Should().BeOfType<BadRequest<ErrorResponse>>()
            .Which.Value!.Error.Should().Be("invalid_state",
                "PKCE state row is single-use; replay must be rejected");
    }

    // ── helpers ─────────────────────────────────────────────────────────────

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

    // ── test doubles ────────────────────────────────────────────────────────

    private sealed class StubKeycloakClient : IKeycloakClient
    {
        public KeycloakTokenResponse? Tokens { get; init; }

        public Task<KeycloakDiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken ct) =>
            Task.FromException<KeycloakDiscoveryDocument>(
                new InvalidOperationException("CallbackAsync must not call discovery directly"));

        public Task<KeycloakTokenResponse?> ExchangeAuthorizationCodeAsync(
            string code, string codeVerifier, string redirectUri, CancellationToken ct) =>
            Task.FromResult(Tokens);

        public Task<KeycloakTokenResponse?> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct) =>
            Task.FromResult<KeycloakTokenResponse?>(null);

        public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct) => Task.CompletedTask;

        public void InvalidateDiscoveryDocument() { }
    }

    private sealed class StubJwksValidator : IKeycloakJwksValidator
    {
        public IReadOnlyDictionary<string, string>? Claims { get; init; }

        public ValueTask<IReadOnlyDictionary<string, string>?> ValidateAsync(
            string token, CancellationToken ct = default) =>
            ValueTask.FromResult(Claims);
    }
}
