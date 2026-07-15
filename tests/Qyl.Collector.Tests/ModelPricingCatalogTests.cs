using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Collector.Cost;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class ModelPricingCatalogTests
{
    private static readonly DateTimeOffset s_now =
        new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OpenRouter_adapter_normalizes_dynamic_rates_preserves_ordered_overrides_and_skips_router_prices()
    {
        const string payload = """
                               {
                                 "data": [
                                   {
                                     "id": "openai/gpt-test",
                                     "canonical_slug": "openai/gpt-test-20260714",
                                     "pricing": {
                                       "prompt": "0.01",
                                       "completion": "0.02",
                                       "audio": "0.5",
                                       "overrides": [
                                         { "min_prompt_tokens": 100, "prompt": "0.03" },
                                         { "min_prompt_tokens": 50, "completion": "0.04" }
                                       ]
                                     }
                                   },
                                   {
                                     "id": "openrouter/auto",
                                     "canonical_slug": "openrouter/auto",
                                     "pricing": { "prompt": "-1", "completion": "-1" }
                                   }
                                 ]
                               }
                               """;
        using var handler = new RecordingHandler(payload);
        using var client = new HttpClient(handler);
        using var source = new OpenRouterModelPricingCatalogSource(
            client,
            new FixedTimeProvider(s_now),
            new OpenRouterModelPricingCatalogOptions(
                true,
                20,
                new Uri("https://openrouter.ai/api/v1/models"),
                "catalog-key"),
            1024 * 1024);

        var result = await source.FetchAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var snapshot = Assert.IsType<ModelPricingCatalogSnapshot>(result.Snapshot);
        Assert.Equal("minimum_available_rate", snapshot.PriceSemantics);
        Assert.Equal(s_now, snapshot.RetrievedAt);
        var model = Assert.Single(snapshot.Models);
        Assert.Equal("openai/gpt-test", model.ModelId);
        Assert.Equal("openai/gpt-test-20260714", model.CanonicalModelId);
        Assert.Equal(ModelPricingBillingMode.Base,
            Assert.Single(model.Rates, static rate => rate.SourceMeter == "prompt").BillingMode);
        Assert.Equal(ModelPricingBillingMode.Unsupported,
            Assert.Single(model.Rates, static rate => rate.SourceMeter == "audio").BillingMode);
        Assert.Equal([100m, 50m], model.Overrides.Select(static value => value.ExclusiveMinimumQuantity));
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("catalog-key", handler.AuthorizationParameter);
        Assert.Equal(20, source.Priority);
        Assert.Equal(64, source.ConfigurationFingerprint.Length);
    }

    [Fact]
    public void Calculator_uses_exact_identity_ordered_strict_overrides_and_replacement_token_math()
    {
        var version = CreateVersion();
        var usage = ModelPricingUsage.ForGenAiCall(
            inputTokens: 120,
            outputTokens: 50,
            cacheReadInputTokens: 20,
            cacheWriteInputTokens: 10,
            reasoningOutputTokens: 5);

        var result = ModelPricingCalculator.Calculate(
            version,
            "openai/gpt-test",
            usage,
            aggregateCallCount: 1);

        Assert.Equal(ModelPricingEstimateStatus.Calculated, result.Status);
        Assert.Equal(4.39m, result.TokenCostUsd);
        Assert.Equal(ModelPricingMatchKind.ExactModelId, result.MatchKind);
        Assert.Equal("snapshot-test", result.SnapshotId);
        Assert.Equal("minimum_available_rate", result.PriceSemantics);
        Assert.Contains(result.Exclusions,
            static exclusion => exclusion is
                { SourceMeter: "audio", Reason: "outside_token_estimate_scope" });
        Assert.Contains(result.Exclusions,
            static exclusion => exclusion is
            {
                SourceMeter: "prompt",
                Reason: "superseded_by_later_override",
                OverrideEvidence.SourceOrder: 1
            });
        Assert.Contains(result.Components,
            static component => component is
            {
                SourceMeter: "prompt",
                SourceBillingMode: "usd_per_token",
                RateRelation: ModelPricingRateRelation.ReplacesPublishedRate,
                OverrideEvidence.SourceOrder: 3,
                OverrideEvidence.ObservedQuantity: 120
            });
        Assert.Contains(result.Components,
            static component => component is
            {
                SourceMeter: "request",
                Quantity: 1,
                RateRelation: ModelPricingRateRelation.AdditiveSurcharge
            });

        var exactBoundary = ModelPricingCalculator.Calculate(
            version,
            "openai/gpt-test",
            ModelPricingUsage.ForGenAiCall(100, 10, 0, 0, 0),
            aggregateCallCount: 1);
        // 100 does not cross the first rule's exclusive threshold; the later 50-token rule still applies.
        Assert.Equal(1.5m, exactBoundary.TokenCostUsd);
        Assert.Contains(exactBoundary.Exclusions,
            static exclusion => exclusion is
            {
                SourceMeter: "prompt",
                Reason: "conditional_adjustment_not_applied",
                OverrideEvidence.SourceOrder: 1,
                OverrideEvidence.ObservedQuantity: 100
            });

        var canonical = ModelPricingCalculator.Calculate(
            version,
            "openai/gpt-test-20260714",
            ModelPricingUsage.ForGenAiCall(10, 10, 0, 0, 0),
            aggregateCallCount: 1);
        Assert.Equal(ModelPricingMatchKind.ExactCanonicalSlug, canonical.MatchKind);

        var noSynthesizedProviderPrefix = ModelPricingCalculator.Calculate(
            version,
            "gpt-test",
            ModelPricingUsage.ForGenAiCall(10, 10),
            aggregateCallCount: 1);
        Assert.Equal(ModelPricingEstimateStatus.ModelNotFound, noSynthesizedProviderPrefix.Status);

        var zeroCompletionModel = version.Catalog.Models[0] with
        {
            Overrides = [],
            Rates = version.Catalog.Models[0].Rates
                .Select(static rate => rate.SourceMeter == "completion" ? rate with { UsdPerUnit = 0 } : rate)
                .ToArray()
        };
        var zeroCompletion = ModelPricingCalculator.Calculate(
            version with { Catalog = version.Catalog with { Models = [zeroCompletionModel] } },
            zeroCompletionModel.ModelId,
            ModelPricingUsage.ForGenAiCall(10, null),
            aggregateCallCount: 1);
        Assert.Equal(ModelPricingEstimateStatus.Calculated, zeroCompletion.Status);
        Assert.Equal(0.2m, zeroCompletion.TokenCostUsd);
        Assert.Contains(zeroCompletion.Exclusions,
            static exclusion => exclusion is { SourceMeter: "completion", Reason: "usage_not_observed" });
    }

    [Fact]
    public void Calculator_keeps_inclusive_base_rates_when_optional_adjustments_are_not_observed()
    {
        var result = ModelPricingCalculator.Calculate(
            CreateVersion(),
            "openai/gpt-test",
            ModelPricingUsage.ForGenAiCall(120, 50),
            aggregateCallCount: 1);

        Assert.Equal(ModelPricingEstimateStatus.Calculated, result.Status);
        Assert.Equal(5.1m, result.TokenCostUsd);
        Assert.Contains(result.Exclusions,
            static exclusion => exclusion is { SourceMeter: "input_cache_read", Reason: "usage_not_observed" });
        Assert.Contains(result.Exclusions,
            static exclusion => exclusion is { SourceMeter: "internal_reasoning", Reason: "usage_not_observed" });
    }

    [Fact]
    public void Calculator_fails_closed_for_ambiguous_identity_aggregate_overrides_and_invalid_subsets()
    {
        var version = CreateVersion();
        var aggregate = ModelPricingCalculator.Calculate(
            version,
            "openai/gpt-test",
            ModelPricingUsage.ForGenAiAggregate(2, 60, 20),
            aggregateCallCount: 2);
        Assert.Equal(ModelPricingEstimateStatus.ConditionalPricingUnresolvable, aggregate.Status);

        var invalidSubset = ModelPricingCalculator.Calculate(
            version,
            "openai/gpt-test",
            ModelPricingUsage.ForGenAiCall(10, 10, 11, 0, 0),
            aggregateCallCount: 1);
        Assert.Equal(ModelPricingEstimateStatus.IncompleteUsage, invalidSubset.Status);

        var first = version.Catalog.Models[0];
        var ambiguousVersion = version with
        {
            Catalog = version.Catalog with
            {
                Models =
                [
                    first,
                    first with
                    {
                        ModelId = "another/model",
                        CanonicalModelId = first.ModelId
                    }
                ]
            }
        };
        var ambiguous = ModelPricingCalculator.Calculate(
            ambiguousVersion,
            first.ModelId,
            ModelPricingUsage.ForGenAiCall(10, 10),
            aggregateCallCount: 1);
        Assert.Equal(ModelPricingEstimateStatus.AmbiguousModel, ambiguous.Status);

        var ambiguousCacheWriteModel = first with
        {
            Overrides = [],
            Rates =
            [
                .. first.Rates,
                Rate("input_cache_write_1h", "cache_write_1h_input_tokens", "token",
                    ModelPricingBillingMode.Replacement, "input_tokens", 0.02m)
            ]
        };
        var ambiguousCacheWrite = ModelPricingCalculator.Calculate(
            version with { Catalog = version.Catalog with { Models = [ambiguousCacheWriteModel] } },
            ambiguousCacheWriteModel.ModelId,
            ModelPricingUsage.ForGenAiCall(10, 4, cacheWriteInputTokens: 1),
            aggregateCallCount: 1);
        Assert.Equal(
            ModelPricingEstimateStatus.ConditionalPricingUnresolvable,
            ambiguousCacheWrite.Status);
    }

    [Fact]
    public async Task Refresh_activates_content_once_retains_same_scope_and_invalidates_changed_configuration()
    {
        await using var store = new DuckDbStore(":memory:");
        var source = new MutableSource(CreateVersion().Catalog, new string('a', 64));
        var options = new ModelPricingCatalogOptions
        {
            SyncInterval = TimeSpan.FromHours(1),
            HttpTimeout = TimeSpan.FromSeconds(30),
            MaximumResponseBytes = 16 * 1024 * 1024,
            MaximumStaleAge = TimeSpan.FromHours(3),
            RetainedSnapshotsPerSource = 32
        };
        using var service = new ModelPricingCatalogRefreshService(
            new ModelPricingCatalogSourceRegistry([source]),
            options,
            store,
            new FixedTimeProvider(s_now),
            NullLogger<ModelPricingCatalogRefreshService>.Instance);
        var changes = 0;
        service.SnapshotChanged += (_, _) => changes++;

        await service.RefreshAllAsync(TestContext.Current.CancellationToken);
        await service.RefreshAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, changes);
        var state = Assert.Single(await store.GetModelPricingCatalogSourcesAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("current", state.Source.Status);
        Assert.NotNull(state.ActiveSnapshot);
        Assert.Equal(state.Source.ActiveSnapshotId, state.ActiveSnapshot.SnapshotId);
        Assert.Equal(1, state.ActiveSnapshot.ModelCount);

        source.Result = ModelPricingCatalogFetchResult.Failed(
            ModelPricingCatalogFailureCategory.ProviderUnavailable);
        await service.RefreshAllAsync(TestContext.Current.CancellationToken);
        Assert.NotNull((await store.GetModelPricingCatalogAsync(
            "openrouter",
            TestContext.Current.CancellationToken))?.Snapshot);

        source.ConfigurationFingerprint = new string('b', 64);
        await service.RefreshAllAsync(TestContext.Current.CancellationToken);

        Assert.Null(await store.GetModelPricingCatalogAsync(
            "openrouter",
            TestContext.Current.CancellationToken));
        state = Assert.Single(await store.GetModelPricingCatalogSourcesAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("sync_failed", state.Source.Status);
        Assert.Null(state.Source.ActiveSnapshotId);
        Assert.Null(state.ActiveSnapshot);
    }

    [Fact]
    public async Task Source_state_derives_staleness_from_the_configured_freshness_window()
    {
        await using var store = new DuckDbStore(":memory:");
        var source = new MutableSource(CreateVersion().Catalog, new string('a', 64));
        var registry = new ModelPricingCatalogSourceRegistry([source]);
        var options = new ModelPricingCatalogOptions
        {
            SyncInterval = TimeSpan.FromHours(1),
            HttpTimeout = TimeSpan.FromSeconds(30),
            MaximumResponseBytes = 16 * 1024 * 1024,
            MaximumStaleAge = TimeSpan.FromHours(3),
            RetainedSnapshotsPerSource = 32
        };
        using var refresh = new ModelPricingCatalogRefreshService(
            registry,
            options,
            store,
            new FixedTimeProvider(s_now),
            NullLogger<ModelPricingCatalogRefreshService>.Instance);
        await refresh.RefreshAllAsync(TestContext.Current.CancellationToken);

        var staleState = new ModelPricingCatalogStateService(
            registry,
            options,
            store,
            new FixedTimeProvider(s_now.AddHours(4)));
        Assert.Equal(
            "stale",
            Assert.Single(await staleState.GetSourcesAsync(TestContext.Current.CancellationToken)).Status);

        source.Result = ModelPricingCatalogFetchResult.Failed(
            ModelPricingCatalogFailureCategory.ProviderUnavailable);
        await refresh.RefreshAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            "sync_failed",
            Assert.Single(await staleState.GetSourcesAsync(TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public void Configuration_binds_credentials_to_the_official_host_and_source_registry_orders_by_priority()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["QYL_OPENROUTER_MODELS_ENDPOINT"] = "https://prices.example.test/models",
            ["QYL_OPENROUTER_API_KEY"] = "secret"
        }).Build();

        Assert.Throws<InvalidOperationException>(() =>
            OpenRouterModelPricingCatalogOptions.FromConfiguration(configuration));

        var later = new MutableSource(
            CreateVersion().Catalog with { SourceId = "later" },
            new string('c', 64),
            priority: 200);
        var earlier = new MutableSource(
            CreateVersion().Catalog with { SourceId = "earlier" },
            new string('d', 64),
            priority: 10);
        var registry = new ModelPricingCatalogSourceRegistry([later, earlier]);
        Assert.Equal([10, 200], registry.Sources.Select(static source => source.Priority));
    }

    [Fact]
    public async Task OpenRouter_adapter_rejects_chunked_responses_that_cross_the_explicit_size_cap()
    {
        using var content = new UnknownLengthContent(new byte[1024]);
        Assert.Null(content.Headers.ContentLength);
        using var handler = new ContentHandler(content);
        using var client = new HttpClient(handler);
        using var source = new OpenRouterModelPricingCatalogSource(
            client,
            new FixedTimeProvider(s_now),
            new OpenRouterModelPricingCatalogOptions(
                true,
                20,
                new Uri("https://openrouter.ai/api/v1/models"),
                null),
            maximumResponseBytes: 64);

        var result = await source.FetchAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ModelPricingCatalogFailureCategory.InvalidResponse, result.Failure?.Category);
    }

    private static ModelPricingCatalogVersion CreateVersion()
    {
        var model = new ModelPricingCatalogModel(
            "openai/gpt-test",
            "openai/gpt-test-20260714",
            "USD",
            [
                Rate("prompt", "input_tokens", "token", ModelPricingBillingMode.Base, null, 0.01m),
                Rate("completion", "output_tokens", "token", ModelPricingBillingMode.Base, null, 0.02m),
                Rate("input_cache_read", "cache_read_input_tokens", "token",
                    ModelPricingBillingMode.Replacement, "input_tokens", 0.002m),
                Rate("input_cache_write", "cache_write_input_tokens", "token",
                    ModelPricingBillingMode.Replacement, "input_tokens", 0.005m),
                Rate("internal_reasoning", "reasoning_output_tokens", "token",
                    ModelPricingBillingMode.Replacement, "output_tokens", 0.03m),
                Rate("request", "requests", "request", ModelPricingBillingMode.Surcharge, null, 0.1m),
                Rate("audio", "source_audio", "source_unit", ModelPricingBillingMode.Unsupported, null, 0.5m)
            ],
            [
                new ModelPricingOverride(1, "input_tokens", 100,
                    [Rate("prompt", "input_tokens", "token", ModelPricingBillingMode.Base, null, 0.02m)]),
                new ModelPricingOverride(2, "input_tokens", 50,
                    [Rate("completion", "output_tokens", "token", ModelPricingBillingMode.Base, null, 0.04m)]),
                new ModelPricingOverride(3, "input_tokens", 110,
                    [Rate("prompt", "input_tokens", "token", ModelPricingBillingMode.Base, null, 0.025m)])
            ]);
        var catalog = new ModelPricingCatalogSnapshot(
            "openrouter",
            new Uri("https://openrouter.ai/api/v1/models"),
            "minimum_available_rate",
            s_now,
            [model]);
        return new ModelPricingCatalogVersion("snapshot-test", s_now, catalog);
    }

    private static ModelPricingRate Rate(
        string sourceMeter,
        string dimension,
        string unit,
        ModelPricingBillingMode mode,
        string? replaces,
        decimal amount) =>
        new(
            sourceMeter,
            dimension,
            unit,
            unit switch
            {
                "token" => "usd_per_token",
                "request" => "usd_per_request",
                _ => "usd_per_source_unit"
            },
            mode,
            replaces,
            amount);

    private sealed class MutableSource : IModelPricingCatalogSource
    {
        public MutableSource(
            ModelPricingCatalogSnapshot snapshot,
            string configurationFingerprint,
            int priority = 100)
        {
            SourceEndpoint = snapshot.SourceEndpoint;
            ConfigurationFingerprint = configurationFingerprint;
            Priority = priority;
            Result = ModelPricingCatalogFetchResult.Success(snapshot);
        }

        public string SourceId => Result.Snapshot?.SourceId ?? "openrouter";
        public int Priority { get; }
        public string ConfigurationFingerprint { get; set; }
        public Uri SourceEndpoint { get; }
        public ModelPricingCatalogFetchResult Result { get; set; }

        public Task<ModelPricingCatalogFetchResult> FetchAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingHandler(string payload) : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ContentHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
