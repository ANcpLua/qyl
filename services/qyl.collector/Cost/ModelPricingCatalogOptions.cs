namespace Qyl.Collector.Cost;

internal sealed record ModelPricingCatalogOptions
{
    public required TimeSpan SyncInterval { get; init; }

    public required TimeSpan HttpTimeout { get; init; }

    public required long MaximumResponseBytes { get; init; }

    public required TimeSpan MaximumStaleAge { get; init; }

    public required int RetainedSnapshotsPerSource { get; init; }

    public static ModelPricingCatalogOptions FromConfiguration(IConfiguration configuration)
    {
        var intervalMinutes = configuration.GetValue("QYL_MODEL_PRICING_SYNC_INTERVAL_MINUTES", 60);
        if (intervalMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                "QYL_MODEL_PRICING_SYNC_INTERVAL_MINUTES must be between 1 and 1440.");
        }

        var timeoutSeconds = configuration.GetValue("QYL_MODEL_PRICING_HTTP_TIMEOUT_SECONDS", 30);
        if (timeoutSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException(
                "QYL_MODEL_PRICING_HTTP_TIMEOUT_SECONDS must be between 1 and 300.");
        }

        var maximumResponseMib = configuration.GetValue("QYL_MODEL_PRICING_MAX_RESPONSE_MIB", 16);
        if (maximumResponseMib is < 1 or > 64)
        {
            throw new InvalidOperationException(
                "QYL_MODEL_PRICING_MAX_RESPONSE_MIB must be between 1 and 64.");
        }

        var maximumStaleMinutes = configuration.GetValue(
            "QYL_MODEL_PRICING_MAX_STALENESS_MINUTES",
            Math.Max(intervalMinutes * 3, 60));
        if (maximumStaleMinutes < intervalMinutes || maximumStaleMinutes > 43_200)
        {
            throw new InvalidOperationException(
                "QYL_MODEL_PRICING_MAX_STALENESS_MINUTES must be at least the sync interval and at most 43200.");
        }

        var retainedSnapshots = configuration.GetValue(
            "QYL_MODEL_PRICING_RETAINED_SNAPSHOTS",
            32);
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
            RetainedSnapshotsPerSource = retainedSnapshots
        };
    }
}

internal sealed record OpenRouterModelPricingCatalogOptions(
    bool Enabled,
    int Priority,
    Uri Endpoint,
    string? ApiKey)
{
    private static readonly Uri s_defaultEndpoint = new("https://openrouter.ai/api/v1/models");

    public static OpenRouterModelPricingCatalogOptions FromConfiguration(IConfiguration configuration)
    {
        var priority = configuration.GetValue("QYL_OPENROUTER_MODEL_CATALOG_PRIORITY", 100);
        if (priority < 0)
            throw new InvalidOperationException("QYL_OPENROUTER_MODEL_CATALOG_PRIORITY cannot be negative.");

        var endpoint = ReadEndpoint(configuration["QYL_OPENROUTER_MODELS_ENDPOINT"]);
        var apiKey = ReadOptionalSecret(configuration["QYL_OPENROUTER_API_KEY"]);
        if (apiKey is not null &&
            !string.Equals(endpoint.Host, "openrouter.ai", StringComparison.OrdinalIgnoreCase) &&
            !endpoint.Host.EndsWith(".openrouter.ai", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "QYL_OPENROUTER_API_KEY can only be sent to an openrouter.ai HTTPS host.");
        }
        if (apiKey is not null && (endpoint.Query.Length is not 0 || endpoint.Fragment.Length is not 0))
        {
            throw new InvalidOperationException(
                "QYL_OPENROUTER_MODELS_ENDPOINT cannot contain a query or fragment when an API key is configured.");
        }

        return new OpenRouterModelPricingCatalogOptions(
            configuration.GetValue("QYL_OPENROUTER_MODEL_CATALOG_ENABLED", true),
            priority,
            endpoint,
            apiKey);
    }

    private static Uri ReadEndpoint(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return s_defaultEndpoint;
        if (!Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            endpoint.Host.Length is 0 ||
            endpoint.UserInfo.Length is not 0)
        {
            throw new InvalidOperationException(
                "QYL_OPENROUTER_MODELS_ENDPOINT must be an absolute HTTPS URL without user information.");
        }

        return endpoint;
    }

    private static string? ReadOptionalSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 4096 || value.Any(static character => character is < '!' or > '~'))
            throw new InvalidOperationException("QYL_OPENROUTER_API_KEY contains invalid characters.");
        return value;
    }
}
