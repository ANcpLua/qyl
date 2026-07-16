using System.Net;
using System.Text;

namespace Qyl.Collector.Cost;

internal interface IProviderCostSource
{
    string Provider { get; }

    Uri SourceEndpoint { get; }

    Task<ProviderCostFetchResult> FetchAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default);
}

internal enum ProviderCostAttribution
{
    ProviderAggregate,
    ProviderReportedModel
}

internal enum ProviderCostScopeKind
{
    Organization,
    Identifier,
    DefaultWorkspace
}

internal readonly record struct ProviderCostScope(ProviderCostScopeKind Kind, string? Identifier)
{
    public static ProviderCostScope Organization => new(ProviderCostScopeKind.Organization, null);

    public static ProviderCostScope ForIdentifier(string identifier) =>
        new(ProviderCostScopeKind.Identifier, identifier);

    public static ProviderCostScope DefaultWorkspace =>
        new(ProviderCostScopeKind.DefaultWorkspace, null);

    public bool Matches(string? returnedIdentifier) => Kind switch
    {
        ProviderCostScopeKind.Organization => true,
        ProviderCostScopeKind.DefaultWorkspace => returnedIdentifier is null,
        ProviderCostScopeKind.Identifier => string.Equals(
            returnedIdentifier,
            Identifier,
            StringComparison.Ordinal),
        _ => false
    };

    public string CreateStableKey(string provider)
    {
        var value = $"{provider}\n{Kind}\n{Identifier ?? string.Empty}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}

internal enum ProviderCostFailureCategory
{
    MissingCredential,
    InvalidCredential,
    InvalidPeriod,
    Authentication,
    Authorization,
    RateLimited,
    ProviderUnavailable,
    Timeout,
    Transport,
    InvalidResponse,
    UnexpectedResponseStatus
}

internal sealed record ProviderCostRecord(
    string Provider,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset RetrievedAt,
    Uri SourceEndpoint,
    ProviderCostAttribution Attribution,
    string? ModelName = null);

internal sealed record ProviderCostFailure(ProviderCostFailureCategory Category);

internal sealed record ProviderCostPeriod(
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd);

internal sealed record ProviderCostFetchResult(
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    IReadOnlyList<ProviderCostRecord> Records,
    IReadOnlyList<ProviderCostPeriod> CoveredPeriods,
    ProviderCostFailure? Failure)
{
    public bool IsSuccess => Failure is null;

    public static ProviderCostFetchResult Success(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        IReadOnlyList<ProviderCostRecord> records,
        IReadOnlyList<ProviderCostPeriod> coveredPeriods) =>
        new(periodStart, periodEnd, records, coveredPeriods, null);

    public static ProviderCostFetchResult Failed(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        ProviderCostFailureCategory category) =>
        new(periodStart, periodEnd, [], [], new ProviderCostFailure(category));
}

internal static class ProviderCostCoverage
{
    public static ProviderCostPeriod? GetContiguousRange(IReadOnlyList<ProviderCostPeriod> periods)
    {
        if (periods.Count is 0) return null;

        var ordered = periods
            .Where(static period => period.PeriodEnd > period.PeriodStart)
            .OrderBy(static period => period.PeriodStart)
            .ThenBy(static period => period.PeriodEnd)
            .ToArray();
        if (ordered.Length != periods.Count) return null;

        var start = ordered[0].PeriodStart;
        var end = ordered[0].PeriodEnd;
        foreach (var period in ordered.AsSpan(1))
        {
            if (period.PeriodStart > end) return null;
            if (period.PeriodEnd > end) end = period.PeriodEnd;
        }

        return new ProviderCostPeriod(start, end);
    }
}

internal static class ProviderCostFailureMapper
{
    public static ProviderCostFailureCategory FromStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => ProviderCostFailureCategory.Authentication,
        HttpStatusCode.Forbidden => ProviderCostFailureCategory.Authorization,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ProviderCostFailureCategory.Timeout,
        HttpStatusCode.TooManyRequests => ProviderCostFailureCategory.RateLimited,
        >= HttpStatusCode.InternalServerError => ProviderCostFailureCategory.ProviderUnavailable,
        _ => ProviderCostFailureCategory.UnexpectedResponseStatus
    };

    public static bool IsCredentialUsable(string? credential) =>
        !string.IsNullOrWhiteSpace(credential) &&
        credential.All(static character => character is >= '!' and <= '~');

    public static bool IsValidCurrencyCode(string? currencyCode) =>
        currencyCode is { Length: 3 } &&
        currencyCode.All(static character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    public static bool IsValidPeriod(DateTimeOffset periodStart, DateTimeOffset periodEnd) =>
        periodEnd > periodStart;
}
