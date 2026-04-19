using Qyl.Collector.Storage;
using Qyl.Contracts.Loom;

namespace Qyl.Collector.Tests.Autofix;

public sealed class LoomSettingsEndpointsTests : IAsyncDisposable
{
    private readonly DuckDbStore _store = new(":memory:");

    public ValueTask DisposeAsync() => _store.DisposeAsync();

    [Fact]
    public async Task Get_NoSavedSettings_ReturnsDefaults()
    {
        var ct = TestContext.Current.CancellationToken;

        var settings = await _store.GetLoomSettingsAsync("default", ct);

        settings.Id.Should().Be("default");
        settings.DefaultCodingAgent.Should().Be("Loom");
        settings.AutomationTuning.Should().Be("medium");
    }

    [Fact]
    public async Task Upsert_ThenGet_RoundTripsData()
    {
        var ct = TestContext.Current.CancellationToken;
        var input = new LoomSettingsRecord
        {
            Id = "default",
            DefaultCodingAgent = "cursor",
            AutomationTuning = "aggressive"
        };

        await _store.UpsertLoomSettingsAsync(input, ct);
        var result = await _store.GetLoomSettingsAsync("default", ct);

        result.DefaultCodingAgent.Should().Be("cursor");
        result.AutomationTuning.Should().Be("aggressive");
    }

    [Fact]
    public async Task Upsert_NormalizesProviderSlug()
    {
        var ct = TestContext.Current.CancellationToken;
        var input = new LoomSettingsRecord
        {
            Id = "default",
            DefaultCodingAgent = "GITHUB_COPILOT"
        };

        await _store.UpsertLoomSettingsAsync(input, ct);
        var result = await _store.GetLoomSettingsAsync("default", ct);

        result.DefaultCodingAgent.Should().Be("github_copilot");
    }

    [Fact]
    public async Task Upsert_Twice_OverwritesPreviousValues()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.UpsertLoomSettingsAsync(
            new LoomSettingsRecord { Id = "default", DefaultCodingAgent = "cursor", AutomationTuning = "low" }, ct);
        await _store.UpsertLoomSettingsAsync(
            new LoomSettingsRecord { Id = "default", DefaultCodingAgent = "claude_code", AutomationTuning = "high" }, ct);

        var result = await _store.GetLoomSettingsAsync("default", ct);

        result.DefaultCodingAgent.Should().Be("claude_code");
        result.AutomationTuning.Should().Be("high");
    }
}
