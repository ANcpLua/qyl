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
/// Stage E1.d coverage. Real <see cref="McpTokenStore"/> + real AES-GCM
/// encryption — only Keycloak HTTP surface is stubbed. Tests prove the
/// refresh-rotation, upstream-failure-then-local-revoke, and idempotent
/// revoke invariants from qyl-PRD.
/// </summary>
public sealed class RefreshRevokeEndpointTests
{
    private const string TestUserSub = "user-42";
    private const string TestTenant = "demo";
    private const string OldRefreshPayload = "old-keycloak-refresh-jwt";
    private const string NewRefreshPayload = "rotated-keycloak-refresh-jwt";
    private static readonly string TestEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static DefaultHttpContext NewContext(string? bearer = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("collector.test");
        if (bearer is not null)
            context.Request.Headers.Authorization = $"Bearer {bearer}";
        return context;
    }

    private static AesGcmTokenEncryption NewEncryption() =>
        new(Options.Create(new TokenEncryptionOptions { Key = TestEncryptionKey }));

    private static async Task<(string Opaque, string TokenHash)> SeedOpaqueTokenAsync(
        IMcpTokenStore store, ITokenEncryption encryption)
    {
        var issued = await store.CreateAsync(new McpTokenCreate(
            UserId: TestUserSub,
            TenantId: TestTenant,
            Scopes: "openid profile email offline_access",
            EncryptedRefresh: encryption.Encrypt(Encoding.UTF8.GetBytes(OldRefreshPayload)),
            RefreshExpiresAt: TimeProvider.System.GetUtcNow().AddMinutes(30)),
            TestContext.Current.CancellationToken);
        return (issued.OpaqueToken, issued.TokenHash);
    }

    private static KeycloakTokenResponse RotatedTokens() => new(
        AccessToken: "new-access",
        IdToken: "new.id.token",
        RefreshToken: NewRefreshPayload,
        ExpiresIn: 300,
        RefreshExpiresIn: 3600,
        TokenType: "Bearer",
        Scope: "openid profile email offline_access");

    // ── /auth/refresh ───────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_Returns401_WhenNoBearerHeader()
    {
        await using var db = new DuckDbStore(":memory:");
        var result = await AuthEndpoints.RefreshAsync(
            context: NewContext(bearer: null),
            keycloak: new StubKeycloakClient(),
            tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Refresh_Returns401_WhenBearerMalformed()
    {
        await using var db = new DuckDbStore(":memory:");
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic Zm9vOmJhcg==";

        var result = await AuthEndpoints.RefreshAsync(
            context: context,
            keycloak: new StubKeycloakClient(),
            tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Refresh_Returns401_WhenOpaqueTokenUnknown()
    {
        await using var db = new DuckDbStore(":memory:");
        var result = await AuthEndpoints.RefreshAsync(
            context: NewContext(bearer: "garbage-opaque"),
            keycloak: new StubKeycloakClient { RefreshTokens = RotatedTokens() },
            tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Refresh_Returns401_AndRevokesLocally_WhenUpstreamRefreshRejected()
    {
        await using var db = new DuckDbStore(":memory:");
        var tokens = new McpTokenStore(db, TimeProvider.System);
        var encryption = NewEncryption();
        var (opaque, tokenHash) = await SeedOpaqueTokenAsync(tokens, encryption);

        var result = await AuthEndpoints.RefreshAsync(
            context: NewContext(bearer: opaque),
            keycloak: new StubKeycloakClient { RefreshTokens = null },
            tokenStore: tokens,
            encryption: encryption,
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        // Subsequent lookup must return null — local revoke was applied.
        var afterRevoke = await tokens.GetByOpaqueTokenAsync(opaque, TestContext.Current.CancellationToken);
        afterRevoke.Should().BeNull(
            "PRD: 'On Keycloak failure (refresh expired/upstream revoked): IMcpTokenStore.RevokeAsync + 401'");
    }

    [Fact]
    public async Task Refresh_HappyPath_Returns200WithNewExpiresAt()
    {
        await using var db = new DuckDbStore(":memory:");
        var tokens = new McpTokenStore(db, TimeProvider.System);
        var encryption = NewEncryption();
        var (opaque, _) = await SeedOpaqueTokenAsync(tokens, encryption);

        var result = await AuthEndpoints.RefreshAsync(
            context: NewContext(bearer: opaque),
            keycloak: new StubKeycloakClient { RefreshTokens = RotatedTokens() },
            tokenStore: tokens,
            encryption: encryption,
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        var json = result.Should().BeOfType<JsonHttpResult<RefreshResponse>>().Which;
        json.Value!.ExpiresAt.Should().NotBeNullOrWhiteSpace();
        DateTimeOffset.TryParse(json.Value.ExpiresAt, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            .Should().BeTrue("expires_at MUST be ISO 8601 round-trippable");
        parsed.Should().BeAfter(TimeProvider.System.GetUtcNow().AddMinutes(30),
            "RefreshExpiresIn=3600s pushes the new expiry far past the seeded 30 min");
    }

    [Fact]
    public async Task Refresh_HappyPath_RotatesEncryptedRefreshInStore()
    {
        await using var db = new DuckDbStore(":memory:");
        var tokens = new McpTokenStore(db, TimeProvider.System);
        var encryption = NewEncryption();
        var (opaque, _) = await SeedOpaqueTokenAsync(tokens, encryption);

        var before = await tokens.GetByOpaqueTokenAsync(opaque, TestContext.Current.CancellationToken);
        Encoding.UTF8.GetString(encryption.Decrypt(before!.EncryptedRefresh))
            .Should().Be(OldRefreshPayload);

        await AuthEndpoints.RefreshAsync(
            context: NewContext(bearer: opaque),
            keycloak: new StubKeycloakClient { RefreshTokens = RotatedTokens() },
            tokenStore: tokens,
            encryption: encryption,
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        var after = await tokens.GetByOpaqueTokenAsync(opaque, TestContext.Current.CancellationToken);
        after.Should().NotBeNull();
        Encoding.UTF8.GetString(encryption.Decrypt(after.EncryptedRefresh))
            .Should().Be(NewRefreshPayload, "the rotated upstream refresh must replace the stored envelope");
    }

    [Fact]
    public async Task Refresh_NeverReturnsKeycloakTokensInBody()
    {
        await using var db = new DuckDbStore(":memory:");
        var tokens = new McpTokenStore(db, TimeProvider.System);
        var encryption = NewEncryption();
        var (opaque, _) = await SeedOpaqueTokenAsync(tokens, encryption);

        var result = await AuthEndpoints.RefreshAsync(
            context: NewContext(bearer: opaque),
            keycloak: new StubKeycloakClient { RefreshTokens = RotatedTokens() },
            tokenStore: tokens,
            encryption: encryption,
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        // Type assertion: the body MUST be RefreshResponse (expires_at only),
        // NOT JsonHttpResult<KeycloakTokenResponse> (would leak refresh_token).
        result.Should().BeOfType<JsonHttpResult<RefreshResponse>>(
            "PRD: 'Never returns the underlying Keycloak token'");
    }

    // ── /auth/revoke ────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_Returns401_WhenNoBearerHeader()
    {
        await using var db = new DuckDbStore(":memory:");
        var result = await AuthEndpoints.RevokeAsync(
            context: NewContext(bearer: null),
            keycloak: new StubKeycloakClient(),
            tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Revoke_HappyPath_Returns204_AndRevokesLocally_AndCallsUpstream()
    {
        await using var db = new DuckDbStore(":memory:");
        var tokens = new McpTokenStore(db, TimeProvider.System);
        var encryption = NewEncryption();
        var (opaque, _) = await SeedOpaqueTokenAsync(tokens, encryption);
        var keycloak = new StubKeycloakClient();

        var result = await AuthEndpoints.RevokeAsync(
            context: NewContext(bearer: opaque),
            keycloak: keycloak,
            tokenStore: tokens,
            encryption: encryption,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<NoContent>();
        keycloak.RevokeCallCount.Should().Be(1, "upstream revoke MUST be attempted");
        keycloak.LastRevokedRefresh.Should().Be(OldRefreshPayload,
            "the decrypted refresh value (not the envelope) MUST be forwarded to Keycloak");

        var afterRevoke = await tokens.GetByOpaqueTokenAsync(opaque, TestContext.Current.CancellationToken);
        afterRevoke.Should().BeNull("local revoke MUST set revoked_at, so GetByOpaqueToken returns null");
    }

    [Fact]
    public async Task Revoke_Returns204_WhenTokenUnknown_NoDisclosure()
    {
        await using var db = new DuckDbStore(":memory:");
        var keycloak = new StubKeycloakClient();

        var result = await AuthEndpoints.RevokeAsync(
            context: NewContext(bearer: "never-issued-opaque"),
            keycloak: keycloak,
            tokenStore: new McpTokenStore(db, TimeProvider.System),
            encryption: NewEncryption(),
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        result.Should().BeOfType<NoContent>(
            "RFC 7009 §2.2: revocation must not disclose whether the token exists");
        keycloak.RevokeCallCount.Should().Be(0,
            "no upstream call when local lookup misses — nothing to revoke upstream");
    }

    [Fact]
    public async Task Revoke_RemainsIdempotent_WhenCalledTwice()
    {
        await using var db = new DuckDbStore(":memory:");
        var tokens = new McpTokenStore(db, TimeProvider.System);
        var encryption = NewEncryption();
        var (opaque, _) = await SeedOpaqueTokenAsync(tokens, encryption);

        var first = await AuthEndpoints.RevokeAsync(
            context: NewContext(bearer: opaque),
            keycloak: new StubKeycloakClient(),
            tokenStore: tokens,
            encryption: encryption,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);
        var second = await AuthEndpoints.RevokeAsync(
            context: NewContext(bearer: opaque),
            keycloak: new StubKeycloakClient(),
            tokenStore: tokens,
            encryption: encryption,
            loggerFactory: NullLoggerFactory.Instance,
            ct: TestContext.Current.CancellationToken);

        first.Should().BeOfType<NoContent>();
        second.Should().BeOfType<NoContent>(
            "idempotency: revoking an already-revoked token MUST still return 204");
    }

    // ── test double ────────────────────────────────────────────────────────

    private sealed class StubKeycloakClient : IKeycloakClient
    {
        public KeycloakTokenResponse? RefreshTokens { get; init; }

        public int RevokeCallCount { get; private set; }
        public string? LastRevokedRefresh { get; private set; }

        public Task<KeycloakDiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken ct) =>
            Task.FromException<KeycloakDiscoveryDocument>(
                new InvalidOperationException("/auth/refresh and /auth/revoke must not call discovery directly"));

        public Task<KeycloakTokenResponse?> ExchangeAuthorizationCodeAsync(
            string code, string codeVerifier, string redirectUri, CancellationToken ct) =>
            Task.FromResult<KeycloakTokenResponse?>(null);

        public Task<KeycloakTokenResponse?> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct) =>
            Task.FromResult(RefreshTokens);

        public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct)
        {
            RevokeCallCount++;
            LastRevokedRefresh = refreshToken;
            return Task.CompletedTask;
        }

        public void InvalidateDiscoveryDocument() { }
    }
}
