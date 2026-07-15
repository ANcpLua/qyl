using System.Net;

namespace Qyl.Collector.Cost;

internal interface IModelPricingCatalogSource
{
    string SourceId { get; }

    int Priority { get; }

    string ConfigurationFingerprint { get; }

    Uri SourceEndpoint { get; }

    Task<ModelPricingCatalogFetchResult> FetchAsync(CancellationToken cancellationToken = default);
}

internal sealed class ModelPricingCatalogSourceRegistry
{
    public ModelPricingCatalogSourceRegistry(IEnumerable<IModelPricingCatalogSource> sources)
    {
        Sources = sources
            .OrderBy(static source => source.Priority)
            .ThenBy(static source => source.SourceId, StringComparer.Ordinal)
            .ToArray();
        var duplicate = Sources
            .GroupBy(static source => source.SourceId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Model-pricing source '{duplicate.Key}' is registered more than once.");

        foreach (var source in Sources)
        {
            if (!ModelPricingCatalogValidation.IsIdentifier(source.SourceId, 64))
                throw new InvalidOperationException($"Model-pricing source id '{source.SourceId}' is invalid.");
            if (source.Priority < 0 || !ModelPricingCatalogValidation.IsIdentifier(
                    source.ConfigurationFingerprint,
                    128))
            {
                throw new InvalidOperationException(
                    $"Model-pricing source '{source.SourceId}' has invalid ordering or configuration identity.");
            }
        }
    }

    public IReadOnlyList<IModelPricingCatalogSource> Sources { get; }
}

internal enum ModelPricingCatalogFailureCategory
{
    Authentication,
    Authorization,
    RateLimited,
    ProviderUnavailable,
    Timeout,
    Transport,
    InvalidResponse,
    UnexpectedResponseStatus
}

internal enum ModelPricingBillingMode
{
    Base,
    Replacement,
    Surcharge,
    Unsupported
}

internal sealed record ModelPricingRate(
    string SourceMeter,
    string UsageDimension,
    string Unit,
    string SourceBillingMode,
    ModelPricingBillingMode BillingMode,
    string? ReplacesUsageDimension,
    decimal UsdPerUnit);

internal sealed record ModelPricingOverride(
    int Priority,
    string ConditionUsageDimension,
    decimal ExclusiveMinimumQuantity,
    IReadOnlyList<ModelPricingRate> Rates);

internal sealed record ModelPricingCatalogModel(
    string ModelId,
    string? CanonicalModelId,
    string CurrencyCode,
    IReadOnlyList<ModelPricingRate> Rates,
    IReadOnlyList<ModelPricingOverride> Overrides);

internal sealed record ModelPricingCatalogSnapshot(
    string SourceId,
    Uri SourceEndpoint,
    string PriceSemantics,
    DateTimeOffset RetrievedAt,
    IReadOnlyList<ModelPricingCatalogModel> Models);

internal sealed record ModelPricingCatalogFailure(
    ModelPricingCatalogFailureCategory Category,
    HttpStatusCode? StatusCode = null);

internal sealed record ModelPricingCatalogFetchResult(
    ModelPricingCatalogSnapshot? Snapshot,
    ModelPricingCatalogFailure? Failure)
{
    public bool IsSuccess => Snapshot is not null && Failure is null;

    public static ModelPricingCatalogFetchResult Success(ModelPricingCatalogSnapshot snapshot) =>
        new(snapshot, null);

    public static ModelPricingCatalogFetchResult Failed(
        ModelPricingCatalogFailureCategory category,
        HttpStatusCode? statusCode = null) =>
        new(null, new ModelPricingCatalogFailure(category, statusCode));
}

internal static class ModelPricingCatalogValidation
{
    public static bool IsValid(ModelPricingCatalogSnapshot snapshot)
    {
        if (!IsIdentifier(snapshot.SourceId, 64) ||
            snapshot.PriceSemantics is not ("minimum_available_rate" or "published_rate") ||
            !snapshot.SourceEndpoint.IsAbsoluteUri ||
            snapshot.Models.Count is 0 or > 100_000)
        {
            return false;
        }

        var modelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in snapshot.Models)
        {
            if (!IsText(model.ModelId, 512) || !modelIds.Add(model.ModelId) ||
                model.CanonicalModelId is { } canonical && !IsText(canonical, 512) ||
                model.CurrencyCode is not { Length: 3 } currency ||
                !currency.All(static character => character is >= 'A' and <= 'Z') ||
                !AreRatesValid(model.Rates))
            {
                return false;
            }

            if (model.Overrides.Count > 1_024) return false;
            var priorities = new HashSet<int>();
            var effectiveRates = model.Rates.ToDictionary(static rate => rate.SourceMeter, StringComparer.Ordinal);
            foreach (var priceOverride in model.Overrides)
            {
                if (priceOverride.Priority <= 0 || !priorities.Add(priceOverride.Priority) ||
                    !IsIdentifier(priceOverride.ConditionUsageDimension, 128) ||
                    priceOverride.ExclusiveMinimumQuantity < 0 ||
                    !AreRatesValid(priceOverride.Rates))
                {
                    return false;
                }

                foreach (var rate in priceOverride.Rates)
                    effectiveRates[rate.SourceMeter] = rate;
                if (!AreRatesValid(effectiveRates.Values.ToArray())) return false;
            }
        }

        return true;
    }

    public static bool IsIdentifier(string? value, int maximumLength) =>
        value is { Length: > 0 } && value.Length <= maximumLength &&
        value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.');

    public static bool IsText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength &&
        value.IndexOfAny(['\r', '\n', '\0']) < 0;

    private static bool AreRatesValid(IReadOnlyList<ModelPricingRate> rates)
    {
        var meters = new HashSet<string>(StringComparer.Ordinal);
        var baseDimensions = new HashSet<string>(StringComparer.Ordinal);
        return rates.Count is > 0 and <= 1_024 && rates.All(rate =>
            IsIdentifier(rate.SourceMeter, 128) && meters.Add(rate.SourceMeter) &&
            IsIdentifier(rate.UsageDimension, 128) &&
            IsIdentifier(rate.Unit, 32) &&
            IsIdentifier(rate.SourceBillingMode, 64) &&
            rate.UsdPerUnit >= 0 &&
            (rate.BillingMode is not ModelPricingBillingMode.Base ||
             baseDimensions.Add(rate.UsageDimension)) &&
            (rate.BillingMode is ModelPricingBillingMode.Replacement
                ? IsIdentifier(rate.ReplacesUsageDimension, 128)
                : rate.ReplacesUsageDimension is null));
    }
}

internal static class ModelPricingCatalogSnapshotIdentity
{
    public static string Compute(
        ModelPricingCatalogSnapshot snapshot,
        string configurationFingerprint)
    {
        var builder = new System.Text.StringBuilder();
        Append(builder, snapshot.SourceId);
        Append(builder, configurationFingerprint);
        Append(builder, snapshot.PriceSemantics);
        foreach (var model in snapshot.Models.OrderBy(static model => model.ModelId, StringComparer.Ordinal))
        {
            Append(builder, model.ModelId);
            Append(builder, model.CanonicalModelId ?? string.Empty);
            Append(builder, model.CurrencyCode);
            AppendRates(builder, model.Rates);
            foreach (var priceOverride in model.Overrides.OrderBy(static value => value.Priority))
            {
                Append(builder, priceOverride.Priority.ToString(CultureInfo.InvariantCulture));
                Append(builder, priceOverride.ConditionUsageDimension);
                Append(builder, priceOverride.ExclusiveMinimumQuantity.ToString(
                    "G29",
                    CultureInfo.InvariantCulture));
                AppendRates(builder, priceOverride.Rates);
            }
        }

        return Convert.ToHexString(SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendRates(System.Text.StringBuilder builder, IReadOnlyList<ModelPricingRate> rates)
    {
        foreach (var rate in rates.OrderBy(static rate => rate.SourceMeter, StringComparer.Ordinal))
        {
            Append(builder, rate.SourceMeter);
            Append(builder, rate.UsageDimension);
            Append(builder, rate.Unit);
            Append(builder, rate.SourceBillingMode);
            Append(builder, rate.BillingMode.ToString());
            Append(builder, rate.ReplacesUsageDimension ?? string.Empty);
            Append(builder, rate.UsdPerUnit.ToString("G29", CultureInfo.InvariantCulture));
        }
    }

    private static void Append(System.Text.StringBuilder builder, string value) =>
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append(';');
}

internal static class ModelPricingCatalogFailureMapper
{
    public static ModelPricingCatalogFailureCategory FromStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => ModelPricingCatalogFailureCategory.Authentication,
        HttpStatusCode.Forbidden => ModelPricingCatalogFailureCategory.Authorization,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
            ModelPricingCatalogFailureCategory.Timeout,
        HttpStatusCode.TooManyRequests => ModelPricingCatalogFailureCategory.RateLimited,
        >= HttpStatusCode.InternalServerError => ModelPricingCatalogFailureCategory.ProviderUnavailable,
        _ => ModelPricingCatalogFailureCategory.UnexpectedResponseStatus
    };
}
