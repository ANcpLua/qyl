namespace Qyl.Collector.Cost;

internal enum ModelPricingEstimateStatus
{
    Calculated,
    SourceUnavailable,
    StaleSource,
    MissingModelIdentity,
    ModelNotFound,
    AmbiguousModel,
    IncompleteUsage,
    ConditionalPricingUnresolvable,
    UnsupportedPricing
}

internal enum ModelPricingMatchKind
{
    ExactModelId,
    ExactCanonicalSlug
}

internal enum ModelPricingRateRelation
{
    BaseRate,
    AdditiveSurcharge,
    ReplacesInclusiveBaseRate,
    ReplacesPublishedRate
}

internal sealed record ModelPricingUsage(
    IReadOnlyDictionary<string, decimal?> Quantities,
    IReadOnlySet<string> RequiredUnsupportedDimensions)
{
    public static ModelPricingUsage ForGenAiCall(
        long? inputTokens,
        long? outputTokens,
        long? cacheReadInputTokens = null,
        long? cacheWriteInputTokens = null,
        long? reasoningOutputTokens = null,
        IReadOnlyDictionary<string, decimal?>? additionalQuantities = null,
        IReadOnlySet<string>? requiredUnsupportedDimensions = null) =>
        ForGenAiAggregate(
            1,
            inputTokens,
            outputTokens,
            cacheReadInputTokens,
            cacheWriteInputTokens,
            reasoningOutputTokens,
            additionalQuantities,
            requiredUnsupportedDimensions);

    public static ModelPricingUsage ForGenAiAggregate(
        long callCount,
        long? inputTokens,
        long? outputTokens,
        long? cacheReadInputTokens = null,
        long? cacheWriteInputTokens = null,
        long? reasoningOutputTokens = null,
        IReadOnlyDictionary<string, decimal?>? additionalQuantities = null,
        IReadOnlySet<string>? requiredUnsupportedDimensions = null)
    {
        var quantities = new Dictionary<string, decimal?>(StringComparer.Ordinal)
        {
            ["requests"] = callCount,
            ["input_tokens"] = inputTokens,
            ["output_tokens"] = outputTokens
        };
        if (cacheReadInputTokens.HasValue)
            quantities["cache_read_input_tokens"] = cacheReadInputTokens.Value;
        if (cacheWriteInputTokens.HasValue)
            quantities["cache_write_input_tokens"] = cacheWriteInputTokens.Value;
        if (reasoningOutputTokens.HasValue)
            quantities["reasoning_output_tokens"] = reasoningOutputTokens.Value;
        if (additionalQuantities is not null)
        {
            foreach (var pair in additionalQuantities)
                quantities[pair.Key] = pair.Value;
        }

        return new ModelPricingUsage(
            quantities,
            requiredUnsupportedDimensions ?? new HashSet<string>(StringComparer.Ordinal));
    }
}

internal sealed record ModelPricingEstimateComponent(
    string SourceMeter,
    string UsageDimension,
    string Unit,
    string SourceBillingMode,
    ModelPricingBillingMode BillingMode,
    ModelPricingRateRelation RateRelation,
    string? ReplacesUsageDimension,
    decimal Quantity,
    decimal UsdPerUnit,
    decimal AmountUsd,
    ModelPricingOverrideEvidence? OverrideEvidence);

internal sealed record ModelPricingOverrideEvidence(
    int SourceOrder,
    string ConditionUsageDimension,
    decimal ExclusiveMinimumQuantity,
    decimal ObservedQuantity);

internal sealed record ModelPricingEstimateExclusion(
    string SourceMeter,
    string Reason,
    string UsageDimension,
    string Unit,
    string SourceBillingMode,
    ModelPricingBillingMode? BillingMode,
    ModelPricingRateRelation RateRelation,
    string? ReplacesUsageDimension,
    decimal UsdPerUnit,
    ModelPricingOverrideEvidence? OverrideEvidence);

internal sealed record ModelPricingEstimateResult(
    ModelPricingEstimateStatus Status,
    decimal? TokenCostUsd,
    string? CurrencyCode,
    string? MatchedModelId,
    ModelPricingMatchKind? MatchKind,
    IReadOnlyList<ModelPricingEstimateComponent> Components,
    IReadOnlyList<ModelPricingEstimateExclusion> Exclusions);

internal static class ModelPricingCalculator
{
    internal static ModelPricingEstimateResult Calculate(
        ModelPricingCatalogVersion version,
        string observedModel,
        ModelPricingUsage usage,
        long aggregateCallCount)
    {
        var resolution = ResolveModel(version.Catalog.Models, observedModel);
        if (resolution.Status is not ModelPricingEstimateStatus.Calculated)
            return Failure(resolution.Status);

        var model = resolution.Model!;
        var components = new List<ModelPricingEstimateComponent>();
        var exclusions = new List<ModelPricingEstimateExclusion>();
        var effectiveRates = model.Rates.ToDictionary(static rate => rate.SourceMeter, StringComparer.Ordinal);
        var overrideEvidence = model.Rates.ToDictionary(
            static rate => rate.SourceMeter,
            static _ => (ModelPricingOverrideEvidence?)null,
            StringComparer.Ordinal);
        foreach (var priceOverride in model.Overrides.OrderBy(static value => value.Priority))
        {
            if (!TryGetQuantity(usage, priceOverride.ConditionUsageDimension, out var conditionQuantity))
            {
                return Failure(
                    ModelPricingEstimateStatus.ConditionalPricingUnresolvable,
                    model.ModelId,
                    resolution.MatchKind,
                    exclusions);
            }

            if (aggregateCallCount > 1 &&
                conditionQuantity > priceOverride.ExclusiveMinimumQuantity)
            {
                return Failure(
                    ModelPricingEstimateStatus.ConditionalPricingUnresolvable,
                    model.ModelId,
                    resolution.MatchKind,
                    exclusions);
            }

            var evidence = new ModelPricingOverrideEvidence(
                priceOverride.Priority,
                priceOverride.ConditionUsageDimension,
                priceOverride.ExclusiveMinimumQuantity,
                conditionQuantity);
            if (conditionQuantity <= priceOverride.ExclusiveMinimumQuantity)
            {
                exclusions.AddRange(priceOverride.Rates.Select(rate => Exclude(
                    rate,
                    "conditional_adjustment_not_applied",
                    evidence)));
                continue;
            }

            foreach (var rate in priceOverride.Rates)
            {
                if (effectiveRates.TryGetValue(rate.SourceMeter, out var superseded) &&
                    overrideEvidence.TryGetValue(rate.SourceMeter, out var supersededEvidence) &&
                    supersededEvidence is not null)
                {
                    exclusions.Add(Exclude(
                        superseded,
                        "superseded_by_later_override",
                        supersededEvidence));
                }

                effectiveRates[rate.SourceMeter] = rate;
                overrideEvidence[rate.SourceMeter] = evidence;
            }
        }

        if (!effectiveRates.Values.Any(static rate => rate.BillingMode is ModelPricingBillingMode.Base) ||
            effectiveRates.Values.Any(static rate =>
                (rate.BillingMode is ModelPricingBillingMode.Base or ModelPricingBillingMode.Replacement) &&
                !string.Equals(rate.Unit, "token", StringComparison.Ordinal)))
        {
            return Failure(
                ModelPricingEstimateStatus.UnsupportedPricing,
                model.ModelId,
                resolution.MatchKind);
        }

        var cacheWriteRates = effectiveRates.Values
            .Where(static rate =>
                rate.BillingMode is ModelPricingBillingMode.Replacement &&
                rate.SourceMeter.StartsWith("input_cache_write", StringComparison.Ordinal))
            .ToArray();
        if (cacheWriteRates.Select(static rate => rate.UsdPerUnit).Distinct().Take(2).Count() > 1 &&
            TryGetQuantity(usage, "cache_write_input_tokens", out var cacheWriteQuantity) &&
            cacheWriteQuantity > 0 &&
            !TryGetQuantity(usage, "cache_write_1h_input_tokens", out _))
        {
            return Failure(
                ModelPricingEstimateStatus.ConditionalPricingUnresolvable,
                model.ModelId,
                resolution.MatchKind,
                exclusions);
        }

        decimal total = 0;
        try
        {
            foreach (var unsupported in effectiveRates.Values
                         .Where(static rate => rate.BillingMode is ModelPricingBillingMode.Unsupported))
            {
                if (usage.RequiredUnsupportedDimensions.Contains(unsupported.UsageDimension) ||
                    TryGetQuantity(usage, unsupported.UsageDimension, out var quantity) && quantity > 0)
                {
                    exclusions.Add(Exclude(
                        unsupported,
                        "unsupported_usage_dimension",
                        overrideEvidence.GetValueOrDefault(unsupported.SourceMeter)));
                    return Failure(
                        ModelPricingEstimateStatus.UnsupportedPricing,
                        model.ModelId,
                        resolution.MatchKind,
                        exclusions);
                }

                exclusions.Add(Exclude(
                    unsupported,
                    "outside_token_estimate_scope",
                    overrideEvidence.GetValueOrDefault(unsupported.SourceMeter)));
            }

            foreach (var baseRate in effectiveRates.Values
                         .Where(static rate => rate.BillingMode is ModelPricingBillingMode.Base))
            {
                if (!TryGetQuantity(usage, baseRate.UsageDimension, out var baseQuantity))
                {
                    if (baseRate.UsdPerUnit is 0)
                    {
                        exclusions.Add(Exclude(
                            baseRate,
                            "usage_not_observed",
                            overrideEvidence.GetValueOrDefault(baseRate.SourceMeter)));
                        baseQuantity = 0;
                    }
                    else
                    {
                        return Failure(
                            ModelPricingEstimateStatus.IncompleteUsage,
                            model.ModelId,
                            resolution.MatchKind,
                            exclusions);
                    }
                }

                decimal replacedQuantity = 0;
                foreach (var replacement in effectiveRates.Values.Where(rate =>
                             rate.BillingMode is ModelPricingBillingMode.Replacement &&
                             string.Equals(
                                 rate.ReplacesUsageDimension,
                                 baseRate.UsageDimension,
                                 StringComparison.Ordinal)))
                {
                    if (!TryGetQuantity(usage, replacement.UsageDimension, out var quantity))
                    {
                        exclusions.Add(Exclude(
                            replacement,
                            "usage_not_observed",
                            overrideEvidence.GetValueOrDefault(replacement.SourceMeter)));
                        continue;
                    }

                    replacedQuantity += quantity;
                    AddComponent(
                        components,
                        replacement,
                        quantity,
                        overrideEvidence.GetValueOrDefault(replacement.SourceMeter),
                        ref total);
                }

                if (replacedQuantity > baseQuantity)
                {
                    return Failure(
                        ModelPricingEstimateStatus.IncompleteUsage,
                        model.ModelId,
                        resolution.MatchKind,
                        exclusions);
                }

                AddComponent(
                    components,
                    baseRate,
                    baseQuantity - replacedQuantity,
                    overrideEvidence.GetValueOrDefault(baseRate.SourceMeter),
                    ref total);
            }

            if (effectiveRates.Values.Any(rate =>
                    rate.BillingMode is ModelPricingBillingMode.Replacement &&
                    !effectiveRates.Values.Any(baseRate =>
                        baseRate.BillingMode is ModelPricingBillingMode.Base &&
                        string.Equals(
                            baseRate.UsageDimension,
                            rate.ReplacesUsageDimension,
                            StringComparison.Ordinal))))
            {
                return Failure(
                    ModelPricingEstimateStatus.UnsupportedPricing,
                    model.ModelId,
                    resolution.MatchKind,
                    exclusions);
            }

            foreach (var surcharge in effectiveRates.Values
                         .Where(static rate => rate.BillingMode is ModelPricingBillingMode.Surcharge))
            {
                if (surcharge.UsdPerUnit is 0)
                {
                    continue;
                }

                if (string.Equals(surcharge.Unit, "request", StringComparison.Ordinal) &&
                    TryGetQuantity(usage, surcharge.UsageDimension, out var requestQuantity))
                {
                    AddComponent(
                        components,
                        surcharge,
                        requestQuantity,
                        overrideEvidence.GetValueOrDefault(surcharge.SourceMeter),
                        ref total);
                    continue;
                }

                if (string.Equals(surcharge.Unit, "request", StringComparison.Ordinal))
                {
                    return Failure(
                        ModelPricingEstimateStatus.IncompleteUsage,
                        model.ModelId,
                        resolution.MatchKind,
                        exclusions);
                }

                if (TryGetQuantity(usage, surcharge.UsageDimension, out var quantity) && quantity > 0)
                {
                    exclusions.Add(Exclude(
                        surcharge,
                        "unsupported_usage_dimension",
                        overrideEvidence.GetValueOrDefault(surcharge.SourceMeter)));
                    return Failure(
                        ModelPricingEstimateStatus.UnsupportedPricing,
                        model.ModelId,
                        resolution.MatchKind,
                        exclusions);
                }

                if (usage.RequiredUnsupportedDimensions.Contains(surcharge.UsageDimension))
                {
                    exclusions.Add(Exclude(
                        surcharge,
                        "unsupported_usage_dimension",
                        overrideEvidence.GetValueOrDefault(surcharge.SourceMeter)));
                    return Failure(
                        ModelPricingEstimateStatus.UnsupportedPricing,
                        model.ModelId,
                        resolution.MatchKind,
                        exclusions);
                }

                exclusions.Add(Exclude(
                    surcharge,
                    "outside_token_estimate_scope",
                    overrideEvidence.GetValueOrDefault(surcharge.SourceMeter)));
            }
        }
        catch (OverflowException)
        {
            return Failure(
                ModelPricingEstimateStatus.UnsupportedPricing,
                model.ModelId,
                resolution.MatchKind,
                exclusions);
        }

        return new ModelPricingEstimateResult(
            ModelPricingEstimateStatus.Calculated,
            total,
            model.CurrencyCode,
            model.ModelId,
            resolution.MatchKind,
            components,
            exclusions);
    }

    private static void AddComponent(
        List<ModelPricingEstimateComponent> components,
        ModelPricingRate rate,
        decimal quantity,
        ModelPricingOverrideEvidence? overrideEvidence,
        ref decimal total)
    {
        var amount = checked(quantity * rate.UsdPerUnit);
        total = checked(total + amount);
        var relation = ResolveRateRelation(rate, overrideEvidence);
        components.Add(new ModelPricingEstimateComponent(
            rate.SourceMeter,
            rate.UsageDimension,
            rate.Unit,
            rate.SourceBillingMode,
            rate.BillingMode,
            relation,
            relation is ModelPricingRateRelation.ReplacesPublishedRate
                ? rate.UsageDimension
                : rate.ReplacesUsageDimension,
            quantity,
            rate.UsdPerUnit,
            amount,
            overrideEvidence));
    }

    private static ModelPricingEstimateExclusion Exclude(
        ModelPricingRate rate,
        string reason,
        ModelPricingOverrideEvidence? overrideEvidence)
    {
        var conditional = reason is "conditional_adjustment_not_applied" or "superseded_by_later_override";
        var relation = conditional
            ? ModelPricingRateRelation.ReplacesPublishedRate
            : ResolveRateRelation(rate, overrideEvidence);
        return new(
            rate.SourceMeter,
            reason,
            rate.UsageDimension,
            rate.Unit,
            rate.SourceBillingMode,
            rate.BillingMode is ModelPricingBillingMode.Unsupported ? null : rate.BillingMode,
            relation,
            conditional || relation is ModelPricingRateRelation.ReplacesPublishedRate
                ? rate.UsageDimension
                : rate.ReplacesUsageDimension,
            rate.UsdPerUnit,
            overrideEvidence);
    }

    private static ModelPricingRateRelation ResolveRateRelation(
        ModelPricingRate rate,
        ModelPricingOverrideEvidence? overrideEvidence) => rate.BillingMode switch
        {
            ModelPricingBillingMode.Replacement => ModelPricingRateRelation.ReplacesInclusiveBaseRate,
            _ when overrideEvidence is not null => ModelPricingRateRelation.ReplacesPublishedRate,
            ModelPricingBillingMode.Surcharge => ModelPricingRateRelation.AdditiveSurcharge,
            _ => ModelPricingRateRelation.BaseRate
        };

    private static bool TryGetQuantity(
        ModelPricingUsage usage,
        string dimension,
        out decimal quantity)
    {
        quantity = 0;
        return usage.Quantities.TryGetValue(dimension, out var value) &&
               value is >= 0 &&
               (quantity = value.Value) >= 0;
    }

    private static ModelResolution ResolveModel(
        IReadOnlyList<ModelPricingCatalogModel> models,
        string observedModel)
    {
        var direct = FindMatches(models, observedModel);
        if (direct.Count > 1)
            return new ModelResolution(ModelPricingEstimateStatus.AmbiguousModel, null, null);
        return direct.Count is 1
            ? direct[0]
            : new ModelResolution(ModelPricingEstimateStatus.ModelNotFound, null, null);
    }

    private static List<ModelResolution> FindMatches(
        IReadOnlyList<ModelPricingCatalogModel> models,
        string candidate)
    {
        var byModel = new Dictionary<string, ModelResolution>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            if (string.Equals(model.ModelId, candidate, StringComparison.Ordinal))
            {
                byModel[model.ModelId] = new ModelResolution(
                    ModelPricingEstimateStatus.Calculated,
                    model,
                    ModelPricingMatchKind.ExactModelId);
            }

            if (string.Equals(model.CanonicalModelId, candidate, StringComparison.Ordinal) &&
                !byModel.ContainsKey(model.ModelId))
            {
                byModel[model.ModelId] = new ModelResolution(
                    ModelPricingEstimateStatus.Calculated,
                    model,
                    ModelPricingMatchKind.ExactCanonicalSlug);
            }
        }

        return byModel.Values.ToList();
    }

    private static ModelPricingEstimateResult Failure(
        ModelPricingEstimateStatus status,
        string? matchedModelId = null,
        ModelPricingMatchKind? matchKind = null,
        IReadOnlyList<ModelPricingEstimateExclusion>? exclusions = null) =>
        new(
            status,
            null,
            null,
            matchedModelId,
            matchKind,
            [],
            exclusions ?? []);

    private sealed record ModelResolution(
        ModelPricingEstimateStatus Status,
        ModelPricingCatalogModel? Model,
        ModelPricingMatchKind? MatchKind);
}
