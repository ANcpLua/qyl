using System.Net.Http.Headers;

namespace Qyl.Collector.Cost;

internal sealed record OpenAiOrganizationCostsOptions(string? AdminApiKey, string? ProjectId = null);

internal sealed class OpenAiOrganizationCostsSource(
    HttpClient httpClient,
    TimeProvider timeProvider,
    OpenAiOrganizationCostsOptions options) : IProviderCostSource
{
    private const int PageSize = 180;
    private const int MaximumPages = 10_000;

    public string Provider => "openai";

    public Uri SourceEndpoint { get; } = new("https://api.openai.com/v1/organization/costs");

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
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AdminApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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

                OpenAiCostsPage? responsePage;
                try
                {
                    await using var content = await response.Content.ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);
                    responsePage = await JsonSerializer.DeserializeAsync(
                            content,
                            ProviderCostJsonSerializerContext.Default.OpenAiCostsPage,
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
            $"start_time={periodStart.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}",
            $"end_time={periodEnd.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}",
            "bucket_width=1d",
            $"limit={PageSize.ToString(CultureInfo.InvariantCulture)}",
            "group_by=project_id",
            "group_by=line_item"
        };
        if (!string.IsNullOrWhiteSpace(options.ProjectId))
            query.Add($"project_ids={Uri.EscapeDataString(options.ProjectId)}");
        if (page is not null) query.Add($"page={Uri.EscapeDataString(page)}");
        return new Uri($"{SourceEndpoint.AbsoluteUri}?{string.Join('&', query)}");
    }

    private bool TryAppendRecords(
        OpenAiCostsPage? page,
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
                !TryFromUnixSeconds(bucket.StartTime, out var bucketStart) ||
                !TryFromUnixSeconds(bucket.EndTime, out var bucketEnd) ||
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
                if (options.ProjectId is not null &&
                    !string.Equals(result.ProjectId, options.ProjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (result.Amount?.Value is not { } amount || amount < 0 ||
                    !ProviderCostFailureMapper.IsValidCurrencyCode(result.Amount.Currency))
                {
                    return false;
                }

                records.Add(new ProviderCostRecord(
                    Provider,
                    bucketStart,
                    bucketEnd,
                    amount,
                    result.Amount.Currency!,
                    retrievedAt,
                    SourceEndpoint,
                    ProviderCostAttribution.ProviderAggregate,
                    result.ProjectId,
                    result.LineItem));
            }
        }

        if (page.HasMore.Value)
        {
            if (string.IsNullOrWhiteSpace(page.NextPage)) return false;
            nextPage = page.NextPage;
        }

        return true;
    }

    private static bool TryFromUnixSeconds(long value, out DateTimeOffset result)
    {
        try
        {
            result = DateTimeOffset.FromUnixTimeSeconds(value);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = default;
            return false;
        }
    }

    private static bool IsUtcDay(DateTimeOffset start, DateTimeOffset end) =>
        start.Offset == TimeSpan.Zero &&
        end.Offset == TimeSpan.Zero &&
        start.TimeOfDay == TimeSpan.Zero &&
        end.TimeOfDay == TimeSpan.Zero &&
        end - start == TimeSpan.FromDays(1);
}
