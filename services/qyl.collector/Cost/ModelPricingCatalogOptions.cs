namespace Qyl.Collector.Cost;

internal sealed record ModelPricingCatalogOptions
{
    public required TimeSpan SyncInterval { get; init; }

    public required TimeSpan HttpTimeout { get; init; }

    public required long MaximumResponseBytes { get; init; }

    public required TimeSpan MaximumStaleAge { get; init; }

    public required int RetainedSnapshots { get; init; }

    public static ModelPricingCatalogOptions FromConfiguration(IConfiguration configuration)
    {
        var intervalMinutes = int.Parse(configuration["QYL_MODEL_PRICING_SYNC_INTERVAL_MINUTES"] ?? "60",
            CultureInfo.InvariantCulture);
        if (intervalMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                "QYL_MODEL_PRICING_SYNC_INTERVAL_MINUTES must be between 1 and 1440.");
        }

        var timeoutSeconds = int.Parse(configuration["QYL_MODEL_PRICING_HTTP_TIMEOUT_SECONDS"] ?? "30",
            CultureInfo.InvariantCulture);
        if (timeoutSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException(
                "QYL_MODEL_PRICING_HTTP_TIMEOUT_SECONDS must be between 1 and 300.");
        }

        var maximumResponseMib = int.Parse(configuration["QYL_MODEL_PRICING_MAX_RESPONSE_MIB"] ?? "16",
            CultureInfo.InvariantCulture);
        if (maximumResponseMib is < 1 or > 64)
        {
            throw new InvalidOperationException(
                "QYL_MODEL_PRICING_MAX_RESPONSE_MIB must be between 1 and 64.");
        }

        var maximumStaleMinutes = int.Parse(
            configuration["QYL_MODEL_PRICING_MAX_STALENESS_MINUTES"] ??
            Math.Max(intervalMinutes * 3, 60).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (maximumStaleMinutes < intervalMinutes || maximumStaleMinutes > 43_200)
        {
            throw new InvalidOperationException(
                "QYL_MODEL_PRICING_MAX_STALENESS_MINUTES must be at least the sync interval and at most 43200.");
        }

        var retainedSnapshots = int.Parse(configuration["QYL_MODEL_PRICING_RETAINED_SNAPSHOTS"] ?? "32",
            CultureInfo.InvariantCulture);
        if (retainedSnapshots is < 1 or > 1024)
        {
            throw new InvalidOperationException(
                "QYL_MODEL_PRICING_RETAINED_SNAPSHOTS must be between 1 and 1024.");
        }

        return new ModelPricingCatalogOptions
        {
            SyncInterval = TimeSpan.FromMinutes(intervalMinutes),
            HttpTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            MaximumResponseBytes = maximumResponseMib * 1024L * 1024L,
            MaximumStaleAge = TimeSpan.FromMinutes(maximumStaleMinutes),
            RetainedSnapshots = retainedSnapshots
        };
    }
}

internal sealed record OpenRouterModelPricingCatalogOptions(string? ApiKey)
{
    public static OpenRouterModelPricingCatalogOptions FromConfiguration(IConfiguration configuration)
        => new(ReadOptionalSecret(configuration["QYL_OPENROUTER_API_KEY"]));

    private static string? ReadOptionalSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 4096 || value.Any(static character => character is < '!' or > '~'))
            throw new InvalidOperationException("QYL_OPENROUTER_API_KEY contains invalid characters.");
        return value;
    }
}
