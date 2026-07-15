using Qyl.Collector.Cost;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class GenAiEtlCatalogEstimatorTests
{
    private static readonly DateTimeOffset s_now =
        new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Estimate_uses_exact_identity_and_per_call_override_evidence()
    {
        var catalog = Version(
            "openrouter",
            "model/exact",
            "model/canonical",
            prompt: 0.02m,
            completion: 0.04m,
            request: 0.1m,
            overrides:
            [
                new ModelPricingOverride(
                    1,
                    "input_tokens",
                    100,
                    [Rate("prompt", "input_tokens", "token", ModelPricingBillingMode.Base, 0.03m)])
            ]);
        var responseRow = Row(
            service: "response-service",
            calls: 3,
            model: "model/exact",
            identityBasis: "response_model");
        var fallbackRow = Row(
            service: "fallback-service",
            calls: 1,
            model: "model/canonical",
            identityBasis: "request_model_fallback");
        var estimates = GenAiEtlCatalogEstimator.Estimate(
            [responseRow, fallbackRow],
            [
                Bucket(responseRow, calls: 1, input: 100, output: 10),
                Bucket(responseRow, calls: 2, input: 120, output: 20),
                Bucket(fallbackRow, calls: 1, input: 10, output: 5)
            ],
            Available(catalog));

        var response = estimates[0];
        Assert.Equal(ModelPricingEstimateStatus.Calculated, response.Status);
        Assert.Equal(3, response.PricedCallCount);
        Assert.Equal(11.5m, response.EstimatedTokenCostUsd);
        Assert.Equal(11.5m / 3, response.EstimatedTokenCostPerCallUsd);
        Assert.Equal("openrouter", response.Provenance?.SourceId);
        Assert.Equal("model/exact", response.Provenance?.ObservedModelId);
        Assert.Equal("model/exact", response.Provenance?.PriceModelId);
        Assert.Equal(
            GenAiEtlObservedModelIdentityBasis.ResponseModel,
            response.Provenance?.ObservedModelIdentityBasis);
        Assert.Equal(ModelPricingMatchKind.ExactModelId, response.Provenance?.ModelMatchKind);
        Assert.Contains(response.Components, static component => component is
        {
            SourceMeter: "request",
            Quantity: 3,
            AmountUsd: 0.3m,
            SourceBillingMode: "usd_per_request",
            RateRelation: ModelPricingRateRelation.AdditiveSurcharge
        });
        Assert.Contains(response.Components, static component => component is
        {
            SourceMeter: "prompt",
            Quantity: 240,
            AmountUsd: 7.2m,
            RateRelation: ModelPricingRateRelation.ReplacesPublishedRate,
            ReplacesUsageDimension: "input_tokens",
            OverrideEvidence.SourceOrder: 1,
            OverrideEvidence.ObservedQuantity: 120
        });
        Assert.Contains(response.Exclusions, static exclusion => exclusion is
        {
            SourceMeter: "prompt",
            Reason: "conditional_adjustment_not_applied",
            OverrideEvidence.ObservedQuantity: 100
        });

        var fallback = estimates[1];
        Assert.Equal(ModelPricingEstimateStatus.Calculated, fallback.Status);
        Assert.Equal("openrouter", fallback.Provenance?.SourceId);
        Assert.Equal(
            GenAiEtlObservedModelIdentityBasis.RequestModelFallback,
            fallback.Provenance?.ObservedModelIdentityBasis);
        Assert.Equal(ModelPricingMatchKind.ExactCanonicalSlug, fallback.Provenance?.ModelMatchKind);
    }

    [Fact]
    public void Estimate_fails_closed_when_one_physical_call_has_unresolvable_pricing()
    {
        var ambiguousCache = Version(
            "openrouter",
            "model/exact",
            canonical: null,
            prompt: 0.01m,
            completion: 0.02m,
            request: 0.1m,
            additionalRates:
            [
                Rate(
                    "input_cache_write",
                    "cache_write_input_tokens",
                    "token",
                    ModelPricingBillingMode.Replacement,
                    0.005m,
                    "input_tokens"),
                Rate(
                    "input_cache_write_1h",
                    "cache_write_1h_input_tokens",
                    "token",
                    ModelPricingBillingMode.Replacement,
                    0.02m,
                    "input_tokens")
            ]);
        var row = Row("service", calls: 2);

        var estimate = Assert.Single(GenAiEtlCatalogEstimator.Estimate(
            [row],
            [
                Bucket(row, calls: 1, input: 10, output: 4, cacheWrite: 0),
                Bucket(row, calls: 1, input: 10, output: 4, cacheWrite: 1)
            ],
            Available(ambiguousCache)));

        Assert.Equal(ModelPricingEstimateStatus.ConditionalPricingUnresolvable, estimate.Status);
        Assert.Equal(0, estimate.PricedCallCount);
        Assert.Null(estimate.EstimatedTokenCostUsd);
        Assert.Null(estimate.Provenance);
        Assert.Empty(estimate.Components);
    }

    [Fact]
    public void Estimate_maps_identity_usage_and_catalog_failures_without_guessing()
    {
        var version = Version(
            "catalog",
            "model/exact",
            canonical: null,
            prompt: 0.01m,
            completion: 0.02m,
            request: 0.1m);
        var available = Available(version);

        var missingIdentity = Row("missing", calls: 1) with
        {
            ModelName = null,
            ModelIdentityBasis = null
        };
        Assert.Equal(
            ModelPricingEstimateStatus.MissingModelIdentity,
            EstimateOne(missingIdentity, [], available).Status);

        var incomplete = Row("incomplete", calls: 2);
        Assert.Equal(
            ModelPricingEstimateStatus.IncompleteUsage,
            EstimateOne(incomplete, [Bucket(incomplete, calls: 1, input: 10, output: 4)], available).Status);

        var invalid = Row("invalid", calls: 1);
        Assert.Equal(
            ModelPricingEstimateStatus.IncompleteUsage,
            EstimateOne(invalid, [Bucket(invalid, calls: 1, input: 10, output: 4, cacheRead: 11)], available)
                .Status);

        var unsupportedOperation = Row("unsupported-operation", calls: 1) with
        {
            OperationName = "invoke_agent"
        };
        Assert.Equal(
            ModelPricingEstimateStatus.UnsupportedPricing,
            EstimateOne(
                unsupportedOperation,
                [Bucket(unsupportedOperation, calls: 1, input: 10, output: 4)],
                available).Status);

        var modelNotFound = Row("not-found", calls: 1) with { ModelName = "model/absent" };
        Assert.Equal(
            ModelPricingEstimateStatus.ModelNotFound,
            EstimateOne(
                modelNotFound,
                [Bucket(modelNotFound, calls: 1, input: 10, output: 4)],
                available).Status);

        var valid = Row("catalog-state", calls: 1);
        var validBucket = Bucket(valid, calls: 1, input: 10, output: 4);
        Assert.Equal(
            ModelPricingEstimateStatus.StaleSource,
            EstimateOne(
                valid,
                [validBucket],
                new ModelPricingCatalogReadResult(ModelPricingCatalogAvailability.Stale, null)).Status);
        Assert.Equal(
            ModelPricingEstimateStatus.SourceUnavailable,
            EstimateOne(
                valid,
                [validBucket],
                new ModelPricingCatalogReadResult(ModelPricingCatalogAvailability.SourceUnavailable, null)).Status);
    }

    [Fact]
    public void Estimate_fails_closed_for_proven_non_token_output_dimensions_and_missing_meters()
    {
        var pricedModalities = Version(
            "catalog",
            "model/exact",
            canonical: null,
            prompt: 0.01m,
            completion: 0.02m,
            request: 0.1m,
            additionalRates:
            [
                Rate("image", "input_images", "image", ModelPricingBillingMode.Surcharge, 0.5m),
                Rate("audio", "source_audio", "source_unit", ModelPricingBillingMode.Unsupported, 0.4m)
            ]);
        var noModalities = Version(
            "no-modalities",
            "model/exact",
            canonical: null,
            prompt: 0.01m,
            completion: 0.02m,
            request: 0.1m);

        var image = Row("image", calls: 1) with { OutputType = "image" };
        var imageEstimate = EstimateOne(
            image,
            [Bucket(image, calls: 1, input: 10, output: 4)],
            Available(pricedModalities));
        Assert.Equal(ModelPricingEstimateStatus.UnsupportedPricing, imageEstimate.Status);
        Assert.Empty(imageEstimate.Components);

        var speech = Row("speech", calls: 1) with { OutputType = "speech" };
        var speechEstimate = EstimateOne(
            speech,
            [Bucket(speech, calls: 1, input: 10, output: 4)],
            Available(pricedModalities));
        Assert.Equal(ModelPricingEstimateStatus.UnsupportedPricing, speechEstimate.Status);
        Assert.Empty(speechEstimate.Components);

        var missingMeter = EstimateOne(
            image,
            [Bucket(image, calls: 1, input: 10, output: 4)],
            Available(noModalities));
        Assert.Equal(ModelPricingEstimateStatus.UnsupportedPricing, missingMeter.Status);
        Assert.Empty(missingMeter.Components);

        var futureModality = Row("future-modality", calls: 1) with { OutputType = "video" };
        Assert.Equal(
            ModelPricingEstimateStatus.UnsupportedPricing,
            EstimateOne(
                futureModality,
                [Bucket(futureModality, calls: 1, input: 10, output: 4)],
                Available(noModalities)).Status);

        var text = Row("text", calls: 1) with { OutputType = "text" };
        var textEstimate = EstimateOne(
            text,
            [Bucket(text, calls: 1, input: 10, output: 4)],
            Available(pricedModalities));
        Assert.Equal(ModelPricingEstimateStatus.Calculated, textEstimate.Status);
        Assert.Contains(textEstimate.Exclusions, static exclusion => exclusion is
        {
            SourceMeter: "image",
            Reason: "outside_token_estimate_scope"
        });
        Assert.Contains(textEstimate.Exclusions, static exclusion => exclusion is
        {
            SourceMeter: "audio",
            Reason: "outside_token_estimate_scope"
        });
    }

    [Fact]
    public void Estimate_treats_absent_embedding_output_tokens_as_zero_while_requiring_input()
    {
        var version = Version(
            "catalog",
            "model/exact",
            canonical: null,
            prompt: 0.01m,
            completion: 0.02m,
            request: 0.1m);
        var row = Row("embedding", calls: 1) with { OperationName = "embeddings" };
        var bucket = Bucket(row, calls: 1, input: 10, output: null);

        var estimate = EstimateOne(row, [bucket], Available(version));

        Assert.Equal(ModelPricingEstimateStatus.Calculated, estimate.Status);
        Assert.Equal(0.2m, estimate.EstimatedTokenCostUsd);
        Assert.Contains(estimate.Components, static component => component is
        {
            SourceMeter: "completion",
            Quantity: 0,
            AmountUsd: 0
        });

        var missingInput = EstimateOne(
            row,
            [Bucket(row, calls: 1, input: null, output: null)],
            Available(version));
        Assert.Equal(ModelPricingEstimateStatus.IncompleteUsage, missingInput.Status);
    }

    private static GenAiEtlCatalogEstimateResult EstimateOne(
        GenAiEtlAuditStorageRow row,
        IReadOnlyList<GenAiEtlAuditUsageBucket> buckets,
        ModelPricingCatalogReadResult catalog) =>
        Assert.Single(GenAiEtlCatalogEstimator.Estimate([row], buckets, catalog));

    private static ModelPricingCatalogReadResult Available(ModelPricingCatalogVersion version) =>
        new(ModelPricingCatalogAvailability.Available, version);

    private static ModelPricingCatalogVersion Version(
        string sourceId,
        string modelId,
        string? canonical,
        decimal prompt,
        decimal completion,
        decimal request,
        IReadOnlyList<ModelPricingRate>? additionalRates = null,
        IReadOnlyList<ModelPricingOverride>? overrides = null)
    {
        var rates = new List<ModelPricingRate>
        {
            Rate("prompt", "input_tokens", "token", ModelPricingBillingMode.Base, prompt),
            Rate("completion", "output_tokens", "token", ModelPricingBillingMode.Base, completion),
            Rate("request", "requests", "request", ModelPricingBillingMode.Surcharge, request)
        };
        if (additionalRates is not null) rates.AddRange(additionalRates);
        var catalog = new ModelPricingCatalogSnapshot(
            sourceId,
            new Uri($"https://{sourceId}.example.test/models"),
            "minimum_available_rate",
            s_now,
            [new ModelPricingCatalogModel(modelId, canonical, "USD", rates, overrides ?? [])]);
        return new ModelPricingCatalogVersion($"snapshot-{sourceId}", s_now, catalog);
    }

    private static ModelPricingRate Rate(
        string sourceMeter,
        string usageDimension,
        string unit,
        ModelPricingBillingMode billingMode,
        decimal amount,
        string? replaces = null) =>
        new(
            sourceMeter,
            usageDimension,
            unit,
            unit switch
            {
                "token" => "usd_per_token",
                "request" => "usd_per_request",
                "image" => "usd_per_image",
                _ => "usd_per_source_unit"
            },
            billingMode,
            replaces,
            amount);

    private static GenAiEtlAuditStorageRow Row(
        string service,
        long calls,
        string model = "model/exact",
        string identityBasis = "response_model") =>
        new()
        {
            ServiceName = service,
            OperationName = "chat",
            OutputType = "text",
            ProviderName = "provider",
            ModelName = model,
            ModelIdentityBasis = identityBasis,
            CallCount = calls,
            InputTokens = 0,
            OutputTokens = 0,
            CacheReadInputTokens = 0,
            CacheCreationInputTokens = 0,
            ReasoningOutputTokens = 0,
            TokenUsageCallCount = calls,
            ErrorCount = 0,
            AverageLatencyMs = 1,
            P95LatencyMs = 1
        };

    private static GenAiEtlAuditUsageBucket Bucket(
        GenAiEtlAuditStorageRow row,
        long calls,
        long? input,
        long? output,
        long? cacheRead = null,
        long? cacheWrite = null,
        long? reasoning = null) =>
        new()
        {
            ServiceName = row.ServiceName,
            OperationName = row.OperationName,
            OutputType = row.OutputType,
            ProviderName = row.ProviderName,
            ModelName = row.ModelName,
            ModelIdentityBasis = row.ModelIdentityBasis,
            InputTokens = input,
            OutputTokens = output,
            CacheReadInputTokens = cacheRead,
            CacheCreationInputTokens = cacheWrite,
            ReasoningOutputTokens = reasoning,
            CallCount = calls
        };
}
