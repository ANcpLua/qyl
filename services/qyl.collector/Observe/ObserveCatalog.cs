using Qyl.Contracts.Generated;

namespace Qyl.Collector.Observe;

internal static class ObserveCatalog
{
    private const string SchemaVersion = DomainContracts.SchemaVersion;


    private static readonly CatalogDomain[] s_domains = [.. BuildDomains()];

    public static CatalogResponse Build(SubscriptionManager subscriptions)
    {
        var activeSubscriptions = subscriptions.GetAll();
        var active = new SubscriptionDto[activeSubscriptions.Count];
        var index = 0;

        foreach (var subscription in activeSubscriptions)
        {
            active[index] = new SubscriptionDto(
                subscription.Id,
                subscription.Filter,
                subscription.Endpoint,
                subscription.CreatedAt,
                subscription.ContractHash,
                subscription.SchemaVersion);
            index++;
        }

        return new CatalogResponse(SchemaVersion, s_domains, active);
    }

    private static IEnumerable<CatalogDomain> BuildDomains()
    {
        foreach (var contract in DomainContracts.All)
        {
            var traceAttributes = contract.TraceAttributes.Length is 0
                ? null
                : BuildCatalogAttributes(contract.TraceAttributes);

            var metricInstruments = contract.MetricInstruments.Length is 0
                ? null
                : BuildCatalogMetricInstruments(contract.MetricInstruments);

            var domain = new CatalogDomain(
                contract.Name,
                contract.Source,
                contract.Signals,
                traceAttributes,
                metricInstruments);

            yield return domain with { ContractHash = ComputeHash(domain) };
        }
    }

    private static CatalogAttribute[] BuildCatalogAttributes(IReadOnlyList<AttributeDef> attributes)
    {
        var result = new CatalogAttribute[attributes.Count];

        for (var i = 0; i < attributes.Count; i++)
        {
            var attribute = attributes[i];
            result[i] = new CatalogAttribute(
                attribute.Name,
                attribute.Type,
                attribute.Required);
        }

        return result;
    }

    private static CatalogMetricInstrument[] BuildCatalogMetricInstruments(
        IReadOnlyList<MetricDef> instruments)
    {
        var result = new CatalogMetricInstrument[instruments.Count];

        for (var i = 0; i < instruments.Count; i++)
        {
            var instrument = instruments[i];
            result[i] = new CatalogMetricInstrument(
                instrument.Name,
                instrument.Instrument,
                instrument.Unit);
        }

        return result;
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
                    .Select(static a => $"{a.Name}:{a.Type}:{(a.Required ? "1" : "0")}")),
            ":",
            domain.MetricInstruments is null
                ? string.Empty
                : string.Join(",", domain.MetricInstruments
                    .OrderBy(static m => m.Name, StringComparer.Ordinal)
                    .Select(static m => $"{m.Name}:{m.Instrument}:{m.Unit}")));

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
