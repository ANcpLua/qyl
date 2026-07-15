namespace Qyl.Collector.Cost;

internal sealed record AnthropicCostReportOptions(
    string? AdminApiKey,
    ProviderCostScope WorkspaceScope = default);

internal sealed class AnthropicCostReportSource(
    HttpClient httpClient,
    TimeProvider timeProvider,
    AnthropicCostReportOptions options) : IProviderCostSource
{
    private const int PageSize = 31;
    private const int MaximumPages = 10_000;
    private const string ApiVersion = "2023-06-01";

    public string Provider => "anthropic";

    public Uri SourceEndpoint { get; } = new("https://api.anthropic.com/v1/organizations/cost_report");

    public async Task<ProviderCostFetchResult> FetchAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        if (!ProviderCostFailureMapper.IsValidPeriod(periodStart, periodEnd))
        {
            return ProviderCostFetchResult.Failed(
                periodStart,
                periodEnd,
                ProviderCostFailureCategory.InvalidPeriod);
        }

        if (!ProviderCostFailureMapper.IsCredentialUsable(options.AdminApiKey))
        {
            var category = string.IsNullOrWhiteSpace(options.AdminApiKey)
                ? ProviderCostFailureCategory.MissingCredential
                : ProviderCostFailureCategory.InvalidCredential;
            return ProviderCostFetchResult.Failed(periodStart, periodEnd, category);
        }

        var retrievedAt = timeProvider.GetUtcNow();
        var records = new List<ProviderCostRecord>();
        var coveredPeriods = new List<ProviderCostPeriod>();
        var seenPages = new HashSet<string>(StringComparer.Ordinal);
        string? page = null;

        for (var pageNumber = 0; pageNumber < MaximumPages; pageNumber++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(periodStart, periodEnd, page));
            request.Headers.TryAddWithoutValidation("x-api-key", options.AdminApiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
            request.Headers.Accept.ParseAdd("application/json");

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
                return ProviderCostFetchResult.Failed(
                    periodStart,
                    periodEnd,
                    ProviderCostFailureCategory.Timeout);
            }
            catch (HttpRequestException)
            {
                return ProviderCostFetchResult.Failed(
                    periodStart,
                    periodEnd,
                    ProviderCostFailureCategory.Transport);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return ProviderCostFetchResult.Failed(
                        periodStart,
                        periodEnd,
                        ProviderCostFailureMapper.FromStatusCode(response.StatusCode),
                        response.StatusCode);
                }

                AnthropicCostReportPage? responsePage;
                try
                {
                    await using var content = await response.Content.ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);
                    responsePage = await JsonSerializer.DeserializeAsync(
                            content,
                            ProviderCostJsonSerializerContext.Default.AnthropicCostReportPage,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return ProviderCostFetchResult.Failed(
                        periodStart,
                        periodEnd,
                        ProviderCostFailureCategory.Timeout);
                }
                catch (HttpRequestException)
                {
                    return ProviderCostFetchResult.Failed(
                        periodStart,
                        periodEnd,
                        ProviderCostFailureCategory.Transport);
                }
                catch (IOException)
                {
                    return ProviderCostFetchResult.Failed(
                        periodStart,
                        periodEnd,
                        ProviderCostFailureCategory.Transport);
                }
                catch (JsonException)
                {
                    return ProviderCostFetchResult.Failed(
                        periodStart,
                        periodEnd,
                        ProviderCostFailureCategory.InvalidResponse);
                }

                if (!TryAppendRecords(
                        responsePage,
                        periodStart,
                        periodEnd,
                        retrievedAt,
                        records,
                        coveredPeriods,
                        out var nextPage))
                {
                    return ProviderCostFetchResult.Failed(
                        periodStart,
                        periodEnd,
                        ProviderCostFailureCategory.InvalidResponse);
                }

                if (nextPage is null)
                {
                    return ProviderCostFetchResult.Success(periodStart, periodEnd, records, coveredPeriods);
                }

                if (!seenPages.Add(nextPage))
                {
                    return ProviderCostFetchResult.Failed(
                        periodStart,
                        periodEnd,
                        ProviderCostFailureCategory.InvalidResponse);
                }

                page = nextPage;
            }
        }

        return ProviderCostFetchResult.Failed(
            periodStart,
            periodEnd,
            ProviderCostFailureCategory.InvalidResponse);
    }

    private Uri BuildRequestUri(DateTimeOffset periodStart, DateTimeOffset periodEnd, string? page)
    {
        var query = new List<string>
        {
            $"starting_at={Uri.EscapeDataString(FormatTimestamp(periodStart))}",
            $"ending_at={Uri.EscapeDataString(FormatTimestamp(periodEnd))}",
            "bucket_width=1d",
            $"limit={PageSize.ToString(CultureInfo.InvariantCulture)}",
            "group_by%5B%5D=workspace_id",
            "group_by%5B%5D=description"
        };
        if (page is not null) query.Add($"page={Uri.EscapeDataString(page)}");
        return new Uri($"{SourceEndpoint.AbsoluteUri}?{string.Join('&', query)}");
    }

    private bool TryAppendRecords(
        AnthropicCostReportPage? page,
        DateTimeOffset requestedStart,
        DateTimeOffset requestedEnd,
        DateTimeOffset retrievedAt,
        List<ProviderCostRecord> records,
        List<ProviderCostPeriod> coveredPeriods,
        out string? nextPage)
    {
        nextPage = null;
        if (page?.Data is null || page.HasMore is null) return false;

        foreach (var bucket in page.Data)
        {
            if (bucket.Results is null ||
                !TryParseTimestamp(bucket.StartingAt, out var bucketStart) ||
                !TryParseTimestamp(bucket.EndingAt, out var bucketEnd) ||
                bucketEnd <= bucketStart ||
                bucketStart < requestedStart ||
                bucketEnd > requestedEnd ||
                !IsUtcDay(bucketStart, bucketEnd))
            {
                return false;
            }

            coveredPeriods.Add(new ProviderCostPeriod(bucketStart, bucketEnd));

            foreach (var result in bucket.Results)
            {
                if (!decimal.TryParse(result.Amount, NumberStyles.Number, CultureInfo.InvariantCulture,
                        out var amountInLowestUnits) ||
                    amountInLowestUnits < 0 ||
                    !ProviderCostFailureMapper.IsValidCurrencyCode(result.Currency) ||
                    result.Model is { } returnedModel &&
                    (string.IsNullOrWhiteSpace(returnedModel) || returnedModel.Length > 256))
                {
                    return false;
                }

                if (!options.WorkspaceScope.Matches(result.WorkspaceId))
                {
                    continue;
                }

                var model = result.Model?.Trim();
                records.Add(new ProviderCostRecord(
                    Provider,
                    bucketStart,
                    bucketEnd,
                    amountInLowestUnits / 100m,
                    result.Currency!,
                    retrievedAt,
                    SourceEndpoint,
                    model is null
                        ? ProviderCostAttribution.ProviderAggregate
                        : ProviderCostAttribution.ProviderReportedModel,
                    ProviderProjectId: result.WorkspaceId,
                    LineItem: result.Description,
                    ModelName: model));
            }
        }

        if (page.HasMore.Value)
        {
            if (string.IsNullOrWhiteSpace(page.NextPage)) return false;
            nextPage = page.NextPage;
        }

        return true;
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static bool TryParseTimestamp(string? value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out result);

    private static bool IsUtcDay(DateTimeOffset start, DateTimeOffset end) =>
        start.Offset == TimeSpan.Zero &&
        end.Offset == TimeSpan.Zero &&
        start.TimeOfDay == TimeSpan.Zero &&
        end.TimeOfDay == TimeSpan.Zero &&
        end - start == TimeSpan.FromDays(1);
}
