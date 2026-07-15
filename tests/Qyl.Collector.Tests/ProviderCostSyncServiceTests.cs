using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Collector.Cost;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class ProviderCostSyncServiceTests
{
    [Fact]
    public void Configuration_distinguishes_unscoped_named_and_default_anthropic_workspaces()
    {
        var unscoped = ProviderCostSyncOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection().Build());
        var defaultWorkspace = ProviderCostSyncOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QYL_ANTHROPIC_WORKSPACE_ID"] = "default"
            }).Build());
        var namedWorkspace = ProviderCostSyncOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QYL_ANTHROPIC_WORKSPACE_ID"] = "wrk_qyl"
            }).Build());

        Assert.Equal(ProviderCostScope.Organization, unscoped.AnthropicWorkspaceScope);
        Assert.Equal(ProviderCostScope.DefaultWorkspace, defaultWorkspace.AnthropicWorkspaceScope);
        Assert.Equal(
            ProviderCostScope.ForIdentifier("wrk_qyl"),
            namedWorkspace.AnthropicWorkspaceScope);
    }

    [Fact]
    public async Task Unexpected_source_failure_is_persisted_safely_and_does_not_stop_other_providers()
    {
        await using var store = new DuckDbStore(":memory:");
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var periodStart = new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddDays(1);
        var anthropicEndpoint = new Uri("https://api.anthropic.com/v1/organizations/cost_report");
        var sources = new IProviderCostSource[]
        {
            new ThrowingSource(),
            new SuccessfulSource(
                "anthropic",
                anthropicEndpoint,
                ProviderCostFetchResult.Success(
                    periodStart,
                    periodEnd,
                    [
                        new ProviderCostRecord(
                            "anthropic",
                            periodStart,
                            periodEnd,
                            1.25m,
                            "USD",
                            now,
                            anthropicEndpoint,
                            ProviderCostAttribution.ProviderReportedModel,
                            ModelName: "claude-test")
                    ],
                    [new ProviderCostPeriod(periodStart, periodEnd)]))
        };
        var options = new ProviderCostSyncOptions
        {
            ProjectId = "default",
            SyncInterval = TimeSpan.FromMinutes(15),
            LookbackDays = 1
        };
        using var service = new ProviderCostSyncService(
            sources,
            options,
            store,
            new FixedTimeProvider(now),
            NullLogger<ProviderCostSyncService>.Instance);

        await service.SyncAllAsync(TestContext.Current.CancellationToken);

        var syncRows = await store.GetProviderCostSyncAsync(
            "default",
            TestContext.Current.CancellationToken);
        var openAi = Assert.Single(syncRows, static row => row.Provider == "openai");
        Assert.Equal("sync_failed", openAi.Status);
        Assert.Equal("invalid_response", openAi.FailureCategory);
        Assert.Null(openAi.LastSuccessAt);
        var anthropic = Assert.Single(syncRows, static row => row.Provider == "anthropic");
        Assert.Equal("current", anthropic.Status);
        Assert.Equal(periodStart, anthropic.PeriodStart);
        Assert.Equal(periodEnd, anthropic.PeriodEnd);
        Assert.Equal(1.25m, Assert.Single(await store.GetProviderCostBucketsAsync(
            "default",
            periodStart,
            periodEnd,
            TestContext.Current.CancellationToken)).Amount);
    }

    [Fact]
    public async Task Failed_sync_after_scope_change_does_not_retain_old_scope_success_coverage()
    {
        await using var store = new DuckDbStore(":memory:");
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var periodStart = new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddDays(1);
        var oldScopeKey = ProviderCostScope.ForIdentifier("proj-old").CreateStableKey("openai");
        await store.ReplaceProviderCostBucketsAsync(
            "default",
            "openai",
            periodStart,
            periodEnd,
            [],
            new ProviderCostSyncRow
            {
                ProjectId = "default",
                Provider = "openai",
                SourceEndpoint = "https://api.openai.com/v1/organization/costs",
                ProviderScopeKey = oldScopeKey,
                SourceKind = "actual_billed_cost",
                Attribution = "provider_period",
                Status = "current",
                LastAttemptAt = periodEnd,
                LastSuccessAt = periodEnd,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            },
            TestContext.Current.CancellationToken);
        var options = new ProviderCostSyncOptions
        {
            ProjectId = "default",
            SyncInterval = TimeSpan.FromMinutes(15),
            LookbackDays = 1,
            OpenAiProjectId = "proj-new"
        };
        using var service = new ProviderCostSyncService(
            [new ThrowingSource()],
            options,
            store,
            new FixedTimeProvider(now),
            NullLogger<ProviderCostSyncService>.Instance);

        await service.SyncAllAsync(TestContext.Current.CancellationToken);

        var sync = Assert.Single(await store.GetProviderCostSyncAsync(
            "default",
            TestContext.Current.CancellationToken));
        Assert.Equal(
            ProviderCostScope.ForIdentifier("proj-new").CreateStableKey("openai"),
            sync.ProviderScopeKey);
        Assert.Equal("sync_failed", sync.Status);
        Assert.Null(sync.LastSuccessAt);
        Assert.Null(sync.PeriodStart);
        Assert.Null(sync.PeriodEnd);
    }

    private sealed class ThrowingSource : IProviderCostSource
    {
        public string Provider => "openai";

        public Uri SourceEndpoint { get; } = new("https://api.openai.com/v1/organization/costs");

        public Task<ProviderCostFetchResult> FetchAsync(
            DateTimeOffset periodStart,
            DateTimeOffset periodEnd,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider parser bug");
    }

    private sealed class SuccessfulSource(
        string provider,
        Uri endpoint,
        ProviderCostFetchResult result) : IProviderCostSource
    {
        public string Provider { get; } = provider;

        public Uri SourceEndpoint { get; } = endpoint;

        public Task<ProviderCostFetchResult> FetchAsync(
            DateTimeOffset periodStart,
            DateTimeOffset periodEnd,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
