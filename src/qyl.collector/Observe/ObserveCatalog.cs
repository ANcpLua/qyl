using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace qyl.collector.Observe;

/// <summary>
/// Builds the observability catalog: available domains, their attribute manifests,
/// and active subscriptions. Initially hardcoded from the four known ActivitySources.
/// </summary>
internal static class ObserveCatalog
{
    // Semconv version that the attribute manifests correspond to.
    // Sourced from qyl.protocol.Attributes.GenAiAttributes.SchemaUrl.
    private const string SchemaVersion = "semconv-1.40.0";

    /// <summary>
    /// Builds the catalog snapshot, merging static domain definitions with live subscription state.
    /// </summary>
    public static CatalogResponse Build(SubscriptionManager subscriptions)
    {
        var active = subscriptions.GetAll()
            .Select(static s => new SubscriptionDto(
                s.Id, s.Filter, s.Endpoint, s.CreatedAt,
                s.ContractHash, s.SchemaVersion))
            .ToArray();

        return new CatalogResponse(SchemaVersion, Domains, active);
    }

    // ── Static domain definitions ────────────────────────────────────────────

    private static readonly CatalogDomain[] Domains =
        BuildDomains().ToArray();

    private static IEnumerable<CatalogDomain> BuildDomains()
    {
        var raw = new CatalogDomain[]
        {
            new("gen_ai", "qyl.genai", ["traces", "metrics"],
                [
                    new("gen_ai.operation.name", "string", Required: true),
                    new("gen_ai.provider.name", "string", Required: true),
                    new("gen_ai.request.model", "string", Required: true),
                    new("gen_ai.response.model", "string"),
                    new("gen_ai.usage.input_tokens", "int"),
                    new("gen_ai.usage.output_tokens", "int"),
                    new("gen_ai.request.temperature", "double"),
                    new("gen_ai.request.max_tokens", "int"),
                    new("gen_ai.request.top_p", "double"),
                    new("gen_ai.response.finish_reasons", "string[]"),
                    new("gen_ai.response.id", "string"),
                    new("gen_ai.tool.name", "string"),
                    new("gen_ai.tool.call.id", "string"),
                    new("gen_ai.output.type", "string"),
                    new("error.type", "string"),
                ],
                [
                    new("gen_ai.client.token.usage", "histogram", "token"),
                    new("gen_ai.client.operation.duration", "histogram", "s"),
                ]),

            new("db", "qyl.db", ["traces"],
                [
                    new("db.system.name", "string", Required: true),
                    new("db.operation.name", "string", Required: true),
                    new("db.collection.name", "string"),
                    new("db.query.text", "string"),
                    new("db.namespace", "string"),
                ]),

            new("traced", "qyl.traced", ["traces"]),

            new("agent", "qyl.agent", ["traces", "metrics"],
                [
                    new("gen_ai.agent.name", "string"),
                    new("gen_ai.operation.name", "string", Required: true),
                ]),
        };

        foreach (var domain in raw)
            yield return domain with { ContractHash = ComputeHash(domain) };
    }

    /// <summary>Returns the contract hash for a given domain source name, or null if not found.</summary>
    internal static string? GetDomainHash(string sourceName)
        => Array.Find(Domains, d => string.Equals(d.Source, sourceName, StringComparison.Ordinal))
                ?.ContractHash;

    /// <summary>
    /// Deterministic 8-hex-char contract hash.
    /// Input: "{schema_version}:{domain_name}:{sorted_attribute_name:type:required,...}"
    /// Stable across restarts and deployments for the same semconv pin.
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

        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }
}

// ── Response DTOs ────────────────────────────────────────────────────────────

internal sealed record CatalogResponse(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("domains")] IReadOnlyList<CatalogDomain> Domains,
    [property: JsonPropertyName("active_subscriptions")] IReadOnlyList<SubscriptionDto> ActiveSubscriptions);

internal sealed record CatalogDomain(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("source")]  string Source,
    [property: JsonPropertyName("signals")] string[] Signals,
    [property: JsonPropertyName("trace_attributes"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CatalogAttribute>? TraceAttributes = null,
    [property: JsonPropertyName("metric_instruments"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CatalogMetricInstrument>? MetricInstruments = null,
    [property: JsonPropertyName("contract_hash"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContractHash = null);

internal sealed record CatalogAttribute(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("required"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool Required = false);

internal sealed record CatalogMetricInstrument(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("instrument")] string Instrument,
    [property: JsonPropertyName("unit")] string Unit);
