using System.Net.Http.Headers;

namespace Qyl.Collector.Cost;

internal sealed class OpenRouterModelPricingCatalogSource(
    HttpClient httpClient,
    TimeProvider timeProvider,
    OpenRouterModelPricingCatalogOptions options,
    long maximumResponseBytes) : IDisposable
{
    internal const string CatalogSourceId = "openrouter";
    internal const int ContractPriority = 100;
    internal static readonly Uri CatalogEndpoint =
        new("https://openrouter.ai/api/v1/models?output_modalities=all");

    public string SourceId { get; } = CatalogSourceId;

    public string ConfigurationFingerprint { get; } = CreateConfigurationFingerprint(options);

    public Uri SourceEndpoint { get; } = CatalogEndpoint;

    public void Dispose() => httpClient.Dispose();

    public async Task<ModelPricingCatalogFetchResult> FetchAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SourceEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (options.ApiKey is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ModelPricingCatalogFetchResult.Failed(ModelPricingCatalogFailureCategory.Timeout);
        }
        catch (HttpRequestException)
        {
            return ModelPricingCatalogFetchResult.Failed(ModelPricingCatalogFailureCategory.Transport);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return ModelPricingCatalogFetchResult.Failed(
                    ModelPricingCatalogFailureMapper.FromStatusCode(response.StatusCode));
            }

            try
            {
                if (maximumResponseBytes <= 0 ||
                    response.Content.Headers.ContentLength > maximumResponseBytes)
                {
                    return ModelPricingCatalogFetchResult.Failed(
                        ModelPricingCatalogFailureCategory.InvalidResponse);
                }

                using var document = await ReadBoundedJsonAsync(
                        response.Content,
                        maximumResponseBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (document is null)
                {
                    return ModelPricingCatalogFetchResult.Failed(
                        ModelPricingCatalogFailureCategory.InvalidResponse);
                }

                var retrievedAt = timeProvider.GetUtcNow();
                return TryParseModels(document.RootElement, out var models)
                    ? ModelPricingCatalogFetchResult.Success(new ModelPricingCatalogSnapshot(
                        SourceId,
                        SourceEndpoint,
                        "minimum_available_rate",
                        retrievedAt,
                        models))
                    : ModelPricingCatalogFetchResult.Failed(ModelPricingCatalogFailureCategory.InvalidResponse);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ModelPricingCatalogFetchResult.Failed(ModelPricingCatalogFailureCategory.Timeout);
            }
            catch (HttpRequestException)
            {
                return ModelPricingCatalogFetchResult.Failed(ModelPricingCatalogFailureCategory.Transport);
            }
            catch (IOException)
            {
                return ModelPricingCatalogFetchResult.Failed(ModelPricingCatalogFailureCategory.Transport);
            }
            catch (JsonException)
            {
                return ModelPricingCatalogFetchResult.Failed(ModelPricingCatalogFailureCategory.InvalidResponse);
            }
        }
    }

    private static bool TryParseModels(
        JsonElement root,
        out IReadOnlyList<ModelPricingCatalogModel> models)
    {
        models = [];
        if (root.ValueKind is not JsonValueKind.Object ||
            !root.TryGetProperty("data", out var data) ||
            data.ValueKind is not JsonValueKind.Array)
        {
            return false;
        }

        var parsed = new List<ModelPricingCatalogModel>();
        var modelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in data.EnumerateArray())
        {
            if (!TryParseModel(element, out var model, out var hasDynamicPrice)) return false;
            if (hasDynamicPrice) continue;
            if (model is null || !modelIds.Add(model.ModelId)) return false;
            parsed.Add(model);
        }

        if (parsed.Count is 0) return false;
        models = parsed;
        return true;
    }

    private static async Task<JsonDocument?> ReadBoundedJsonAsync(
        HttpContent content,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var buffer = new MemoryStream();
        var rented = ArrayPool<byte>.Shared.Rent(81_920);
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(rented, cancellationToken).ConfigureAwait(false);
                if (read is 0) break;
                if (buffer.Length + read > maximumBytes) return null;
                await buffer.WriteAsync(rented.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            return JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length)));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static bool TryParseModel(
        JsonElement element,
        out ModelPricingCatalogModel? model,
        out bool hasDynamicPrice)
    {
        model = null;
        hasDynamicPrice = false;
        if (element.ValueKind is not JsonValueKind.Object ||
            !TryReadRequiredText(element, "id", out var modelId) ||
            !TryReadOptionalText(element, "canonical_slug", out var canonicalModelId) ||
            !element.TryGetProperty("pricing", out var pricing) ||
            pricing.ValueKind is not JsonValueKind.Object ||
            !TryReadRates(
                pricing.EnumerateObject().Where(static property =>
                    !property.NameEquals("overrides") && !property.NameEquals("discount")),
                out var rates,
                out hasDynamicPrice))
        {
            return false;
        }

        if (hasDynamicPrice) return true;
        if (!rates.Any(static rate => rate.SourceMeter == "prompt") ||
            !rates.Any(static rate => rate.SourceMeter == "completion") ||
            !TryReadOverrides(pricing, out var priceOverrides, out hasDynamicPrice))
        {
            return false;
        }

        if (hasDynamicPrice) return true;
        model = new ModelPricingCatalogModel(
            modelId!,
            canonicalModelId,
            "USD",
            rates,
            priceOverrides);
        return true;
    }

    private static bool TryReadRates(
        IEnumerable<JsonProperty> properties,
        out IReadOnlyList<ModelPricingRate> rates,
        out bool hasDynamicPrice)
    {
        var parsed = new List<ModelPricingRate>();
        var meters = new HashSet<string>(StringComparer.Ordinal);
        hasDynamicPrice = false;
        foreach (var property in properties)
        {
            if (!ModelPricingCatalogValidation.IsIdentifier(property.Name, 128) ||
                !meters.Add(property.Name) ||
                property.Value.ValueKind is not JsonValueKind.String ||
                !decimal.TryParse(
                    property.Value.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var amount))
            {
                rates = [];
                return false;
            }

            if (amount < 0)
            {
                if (property.NameEquals("prompt") || property.NameEquals("completion"))
                    hasDynamicPrice = true;
                continue;
            }

            NormalizeRate(property.Name, amount, out var normalizedRate);
            parsed.Add(normalizedRate);
        }

        rates = parsed;
        return true;
    }

    private static bool TryReadOverrides(
        JsonElement pricing,
        out IReadOnlyList<ModelPricingOverride> priceOverrides,
        out bool hasDynamicPrice)
    {
        priceOverrides = [];
        hasDynamicPrice = false;
        if (!pricing.TryGetProperty("overrides", out var overrides) ||
            overrides.ValueKind is JsonValueKind.Null)
        {
            return true;
        }

        if (overrides.ValueKind is not JsonValueKind.Array) return false;
        var parsed = new List<ModelPricingOverride>();
        var priority = 0;
        foreach (var element in overrides.EnumerateArray())
        {
            priority++;
            if (element.ValueKind is JsonValueKind.Object &&
                (element.TryGetProperty("utc_start", out _) || element.TryGetProperty("utc_end", out _)))
            {
                // Aggregated audit usage cannot select a UTC-conditioned rate without
                // inventing the per-call routing evidence, so this model stays unpriced.
                hasDynamicPrice = true;
                return true;
            }

            if (element.ValueKind is not JsonValueKind.Object ||
                !element.TryGetProperty("min_prompt_tokens", out var minimum) ||
                !minimum.TryGetInt64(out var minimumPromptTokens) ||
                minimumPromptTokens < 0)
            {
                return false;
            }

            if (!TryReadRates(
                    element.EnumerateObject()
                        .Where(static property =>
                            !property.NameEquals("min_prompt_tokens") && !property.NameEquals("discount")),
                    out var rates,
                    out hasDynamicPrice) ||
                rates.Count is 0)
            {
                return false;
            }

            if (hasDynamicPrice) return true;
            parsed.Add(new ModelPricingOverride(
                priority,
                "input_tokens",
                minimumPromptTokens,
                rates));
        }

        priceOverrides = parsed;
        return true;
    }

    private static bool TryReadRequiredText(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind is JsonValueKind.String &&
               ModelPricingCatalogValidation.IsText(value = property.GetString(), 512);
    }

    private static void NormalizeRate(
        string sourceMeter,
        decimal amount,
        out ModelPricingRate rate)
    {
        var normalized = sourceMeter switch
        {
            "prompt" => ("input_tokens", "token", "usd_per_token", ModelPricingBillingMode.Base, (string?)null),
            "completion" => ("output_tokens", "token", "usd_per_token", ModelPricingBillingMode.Base, (string?)null),
            "input_cache_read" => (
                "cache_read_input_tokens", "token", "usd_per_token", ModelPricingBillingMode.Replacement, "input_tokens"),
            "input_cache_write" => (
                "cache_write_input_tokens", "token", "usd_per_token", ModelPricingBillingMode.Replacement, "input_tokens"),
            "input_cache_write_1h" => (
                "cache_write_1h_input_tokens", "token", "usd_per_token", ModelPricingBillingMode.Replacement, "input_tokens"),
            "internal_reasoning" => (
                "reasoning_output_tokens", "token", "usd_per_token", ModelPricingBillingMode.Replacement, "output_tokens"),
            "request" => ("requests", "request", "usd_per_request", ModelPricingBillingMode.Surcharge, (string?)null),
            "image" => ("input_images", "image", "usd_per_image", ModelPricingBillingMode.Surcharge, (string?)null),
            "web_search" => ("web_searches", "search", "usd_per_search", ModelPricingBillingMode.Surcharge, (string?)null),
            _ => (
                $"source_{sourceMeter}",
                "source_unit",
                "usd_per_source_unit",
                ModelPricingBillingMode.Unsupported,
                (string?)null)
        };

        rate = new ModelPricingRate(
            sourceMeter,
            normalized.Item1,
            normalized.Item2,
            normalized.Item3,
            normalized.Item4,
            normalized.Item5,
            amount);
    }

    private static bool TryReadOptionalText(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null)
        {
            return true;
        }

        return property.ValueKind is JsonValueKind.String &&
               ModelPricingCatalogValidation.IsText(value = property.GetString(), 512);
    }

    private static string CreateConfigurationFingerprint(OpenRouterModelPricingCatalogOptions sourceOptions)
    {
        var credentialIdentity = sourceOptions.ApiKey is null
            ? "anonymous"
            : Convert.ToHexString(SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(sourceOptions.ApiKey)));
        var material = $"openrouter-models-v1/normalized-pricing-v2\n{CatalogEndpoint.AbsoluteUri}\n{credentialIdentity}";
        return Convert.ToHexString(SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant();
    }
}
