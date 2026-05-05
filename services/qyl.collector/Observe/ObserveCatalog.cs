using Qyl.Contracts.Generated;

namespace Qyl.Collector.Observe;

internal static class ObserveCatalog
{
    private const string SchemaVersion = DomainContracts.SchemaVersion;


    private static readonly CatalogDomain[] s_domains =
        BuildDomains().ToArray();

    public static CatalogResponse Build(SubscriptionManager subscriptions)
    {
        var active = subscriptions.GetAll()
            .Select(static s => new SubscriptionDto(
                s.Id, s.Filter, s.Endpoint, s.CreatedAt,
                s.ContractHash, s.SchemaVersion))
            .ToArray();

        return new CatalogResponse(SchemaVersion, s_domains, active);
    }

    private static IEnumerable<CatalogDomain> BuildDomains()
    {
        foreach (var contract in DomainContracts.All)
        {
            var domain = new CatalogDomain(
                contract.Name,
                contract.Source,
                contract.Signals,
                contract.TraceAttributes.Length is 0
                    ? null
                    : contract.TraceAttributes
                        .Select(static attribute => new CatalogAttribute(
                            attribute.Name,
                            attribute.Type,
                            attribute.Required))
                        .ToArray(),
                contract.MetricInstruments.Length is 0
                    ? null
                    : contract.MetricInstruments
                        .Select(static metric => new CatalogMetricInstrument(
                            metric.Name,
                            metric.Instrument,
                            metric.Unit))
                        .ToArray());

            yield return domain with { ContractHash = ComputeHash(domain) };
        }
    }

    internal static string? GetDomainHash(string sourceName)
        => Array.Find(s_domains, d => string.Equals(d.Source, sourceName, StringComparison.Ordinal))
            ?.ContractHash;

    private static string ComputeHash(CatalogDomain domain)
    {
        var input = string.Concat(
            SchemaVersion, ":",
            domain.Name, ":",
            domain.TraceAttributes is null
                ? string.Empty
                : string.Join(",", domain.TraceAttributes
                    .OrderBy(static a => a.Name, StringComparer.Ordinal)
                    .Select(static a => $"{a.Name}:{a.Type}:{(a.Required ? "1" : "0")}")));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }
}


internal sealed record CatalogResponse(
    [property: JsonPropertyName("schema_version")]
    string SchemaVersion,
    [property: JsonPropertyName("domains")]
    IReadOnlyList<CatalogDomain> Domains,
    [property: JsonPropertyName("active_subscriptions")]
    IReadOnlyList<SubscriptionDto> ActiveSubscriptions);

internal sealed record CatalogDomain(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("signals")]
    string[] Signals,
    [property: JsonPropertyName("trace_attributes")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CatalogAttribute>? TraceAttributes = null,
    [property: JsonPropertyName("metric_instruments")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CatalogMetricInstrument>? MetricInstruments = null,
    [property: JsonPropertyName("contract_hash")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContractHash = null);

internal sealed record CatalogAttribute(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("required")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool Required = false);

internal sealed record CatalogMetricInstrument(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("instrument")]
    string Instrument,
    [property: JsonPropertyName("unit")] string Unit);
