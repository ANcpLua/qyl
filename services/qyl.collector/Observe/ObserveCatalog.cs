using Qyl.Contracts.Generated;

namespace Qyl.Collector.Observe;

/// <summary>
///     Builds the observability catalog: available domains, their generated attribute manifests,
///     and active subscriptions.
/// </summary>
internal static class ObserveCatalog
{
    // Semconv version that the attribute manifests correspond to.
    // Sourced from the generated DomainContracts source of truth.
    private const string SchemaVersion = DomainContracts.SchemaVersion;

    // ── Static domain definitions ────────────────────────────────────────────

    private static readonly CatalogDomain[] s_domains =
        BuildDomains().ToArray();

    /// <summary>
    ///     Builds the catalog snapshot, merging static domain definitions with live subscription state.
    /// </summary>
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

    /// <summary>Returns the contract hash for a given domain source name, or null if not found.</summary>
    internal static string? GetDomainHash(string sourceName)
        => Array.Find(s_domains, d => string.Equals(d.Source, sourceName, StringComparison.Ordinal))
            ?.ContractHash;

    /// <summary>
    ///     Deterministic 8-hex-char contract hash.
    ///     Input: "{schema_version}:{domain_name}:{sorted_attribute_name:type:required,...}"
    ///     Stable across restarts and deployments for the same semconv pin.
    /// </summary>
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

// ── Response DTOs ────────────────────────────────────────────────────────────

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
