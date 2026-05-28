using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Auth;

public sealed class PkceStateStoreTests
{
    [Fact]
    public async Task StoreAsync_ThenConsumeAsync_RoundtripsRow()
    {
        await using var store = new DuckDbStore(":memory:");
        var sut = new PkceStateStore(store, TimeProvider.System);

        await sut.StoreAsync(
            state: "state-001",
            codeVerifier: "verifier-payload",
            tenantId: "demo",
            clientRedirectUri: "http://localhost:9000/cb",
            nonce: "nonce-001",
            ttl: TimeSpan.FromMinutes(5),
            ct: TestContext.Current.CancellationToken);

        var consumed = await sut.ConsumeAsync("state-001", TestContext.Current.CancellationToken);

        consumed.Should().NotBeNull();
        consumed!.CodeVerifier.Should().Be("verifier-payload");
        consumed.TenantId.Should().Be("demo");
        consumed.ClientRedirectUri.Should().Be("http://localhost:9000/cb");
        consumed.Nonce.Should().Be("nonce-001");
    }

    [Fact]
    public async Task ConsumeAsync_ReturnsNullOnSecondCall_SingleUseSemantics()
    {
        await using var store = new DuckDbStore(":memory:");
        var sut = new PkceStateStore(store, TimeProvider.System);

        await sut.StoreAsync(
            state: "state-002", codeVerifier: "v", tenantId: "t",
            clientRedirectUri: "http://x", nonce: "n", ttl: TimeSpan.FromMinutes(5),
            ct: TestContext.Current.CancellationToken);

        var first = await sut.ConsumeAsync("state-002", TestContext.Current.CancellationToken);
        var second = await sut.ConsumeAsync("state-002", TestContext.Current.CancellationToken);

        first.Should().NotBeNull();
        second.Should().BeNull("PKCE state must be consumable exactly once");
    }

    [Fact]
    public async Task ConsumeAsync_ReturnsNull_WhenStateExpiredByTtl()
    {
        await using var store = new DuckDbStore(":memory:");
        var time = new TestTimeProvider(new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero));
        var sut = new PkceStateStore(store, time);

        await sut.StoreAsync(
            state: "state-003", codeVerifier: "v", tenantId: "t",
            clientRedirectUri: "http://x", nonce: "n", ttl: TimeSpan.FromMinutes(2),
            ct: TestContext.Current.CancellationToken);

        time.Advance(TimeSpan.FromMinutes(3));

        var consumed = await sut.ConsumeAsync("state-003", TestContext.Current.CancellationToken);

        consumed.Should().BeNull("rows past expires_at must not be consumable");
    }

    [Fact]
    public async Task ConsumeAsync_ReturnsNull_ForMissingState()
    {
        await using var store = new DuckDbStore(":memory:");
        var sut = new PkceStateStore(store, TimeProvider.System);

        var consumed = await sut.ConsumeAsync("never-stored", TestContext.Current.CancellationToken);

        consumed.Should().BeNull();
    }

    [Fact]
    public async Task CleanupExpiredAsync_DeletesPastExpiry_AndKeepsCurrentRows()
    {
        await using var store = new DuckDbStore(":memory:");
        var time = new TestTimeProvider(new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero));
        var sut = new PkceStateStore(store, time);

        await sut.StoreAsync("expired-1", "v", "t", "http://x", "n", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        await sut.StoreAsync("expired-2", "v", "t", "http://x", "n", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        await sut.StoreAsync("current-1", "v", "t", "http://x", "n", TimeSpan.FromMinutes(30), TestContext.Current.CancellationToken);

        time.Advance(TimeSpan.FromMinutes(5));

        var deleted = await sut.CleanupExpiredAsync(TestContext.Current.CancellationToken);

        deleted.Should().Be(2);

        var stillThere = await sut.ConsumeAsync("current-1", TestContext.Current.CancellationToken);
        stillThere.Should().NotBeNull("rows not yet past expires_at must survive cleanup");
    }
}
