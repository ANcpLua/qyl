using System.Text.Json;
using Qyl.Collector.Ingestion;
using Qyl.Telemetry.SemanticConventions;

namespace Qyl.Collector.Tests;

/// <summary>
/// The deny list has one home, SemConv's generated <see cref="RegistryPolicy"/>: the collector catalog is
/// generated from it, the policy file holds no deny section and no exception of its own, and every key the
/// policy file names for persistence is one the registry persists. A typed view can then only assert what ingest keeps.
/// </summary>
public sealed class CatalogPolicyTests
{
    private const string AppPrefix = "qyl.app.";

    [Theory]
    [InlineData("deniedExactPrefixes")]
    [InlineData("deniedExactKeys")]
    [InlineData("deniedKeyTokens")]
    [InlineData("deniedTokenExemptKeys")]
    [InlineData("safeHttpSpanHeaderAttributeKeys")]
    public void The_policy_file_carries_no_deny_section_and_no_exception(string section)
    {
        using var policy = PolicyFile();

        Assert.False(policy.RootElement.TryGetProperty(section, out _), $"{section} belongs to RegistryPolicy");
    }

    [Fact]
    public void The_generated_catalog_is_RegistryPolicy()
    {
        Assert.Equal(Sorted(RegistryPolicy.NotPersistedKeys), Sorted(CollectorSemanticAttributeCatalog.NotPersistedKeys));
        Assert.Equal(Sorted(RegistryPolicy.NotPersistedPrefixes), Sorted(CollectorSemanticAttributeCatalog.NotPersistedPrefixes));
        Assert.Equal(Sorted(RegistryPolicy.DeniedKeyTokens), Sorted(CollectorSemanticAttributeCatalog.DeniedKeyTokens));
        Assert.Equal(Sorted(RegistryPolicy.TokenExemptKeys), Sorted(CollectorSemanticAttributeCatalog.TokenExemptKeys));
        Assert.Equal(Sorted(RegistryPolicy.VendorKeys), Sorted(CollectorSemanticAttributeCatalog.VendorKeys));

        // POL's five exemptions plus the five hot token counts the collector lifts into columns.
        // SemanticConventions 10.0.1: plus the ten plain token counts (gen_ai.request.max_tokens, gen_ai.usage.{text,audio,image}.*_tokens).
        Assert.Equal(20, CollectorSemanticAttributeCatalog.TokenExemptKeys.Count);
        Assert.Contains("gen_ai.request.max_tokens", (IReadOnlySet<string>)CollectorSemanticAttributeCatalog.TokenExemptKeys);
        Assert.Contains("gen_ai.usage.reasoning.output_tokens", (IReadOnlySet<string>)CollectorSemanticAttributeCatalog.TokenExemptKeys);
        Assert.DoesNotContain("gen_ai.agent.name", (IReadOnlySet<string>)CollectorSemanticAttributeCatalog.NotPersistedKeys);
    }

    /// <summary>
    /// <see cref="AttributeKeySets.IsPersistable"/> is <see cref="RegistryPolicy.IsPersisted"/> in every spelling, stricter
    /// in three ways only: a denied key or prefix matches in any case, <c>baggage.*</c> is never persisted, and
    /// <c>qyl.app.&lt;key&gt;</c> is denied when <c>&lt;key&gt;</c> is. No boundary keeps a key around it.
    /// </summary>
    [Fact]
    public void The_collector_persists_exactly_what_RegistryPolicy_persists()
    {
        var corpus = Corpus().ToArray();

        var disagreements = corpus
            .Where(static key => Expected(key) != AttributeKeySets.IsPersistable(key))
            .Select(static key => $"{key}: expected {Expected(key)}, collector {AttributeKeySets.IsPersistable(key)}")
            .ToArray();
        var bypasses = corpus
            .Where(static key => !Expected(key) &&
                (AttributeKeySets.IsSafeSpanAttribute(key) ||
                 AttributeKeySets.IsSafeLogAttribute(key) ||
                 AttributeKeySets.IsSafeMetricAttribute(key) ||
                 AttributeKeySets.IsSafeResourceAttribute(key) ||
                 AttributeKeySets.IsSafeEntityReferencedResourceAttribute(key)))
            .ToArray();

        Assert.Empty(disagreements);
        Assert.Empty(bypasses);
    }

    [Fact]
    public void Every_key_the_policy_file_names_for_persistence_is_one_the_registry_persists()
    {
        using var policy = PolicyFile();
        var root = policy.RootElement;
        var named = root.GetProperty("developmentAttributeAllowList").EnumerateObject()
            .SelectMany(static signal => signal.Value.EnumerateArray())
            .Concat(root.GetProperty("qylResourceAttributeAllowList").EnumerateArray())
            .Concat(root.GetProperty("projectIdResourceKeys").EnumerateArray())
            .Concat(root.GetProperty("spanHotAttributeKeys").EnumerateArray())
            .Select(static key => key.GetString()!)
            .ToArray();

        Assert.NotEmpty(named);
        Assert.All(named, static key => Assert.True(RegistryPolicy.IsPersisted(key), key));
    }

    private static bool Expected(string key) =>
        RegistryPolicy.IsPersisted(key) &&
        !DeniedKeyOrPrefixInAnyCase(key) &&
        !key.StartsWith("baggage.", StringComparison.OrdinalIgnoreCase) &&
        !(key.StartsWith(AppPrefix, StringComparison.OrdinalIgnoreCase) && DeniedKeyOrPrefixInAnyCase(key[AppPrefix.Length..]));

    private static bool DeniedKeyOrPrefixInAnyCase(string key) =>
        RegistryPolicy.NotPersistedKeys.Any(denied =>
            key.Equals(denied, StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith(denied + ".", StringComparison.OrdinalIgnoreCase)) ||
        RegistryPolicy.NotPersistedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every key either side names, the template children of the denied ones, keys that carry a denied token, the
    /// header keys the collector once exempted, and <c>qyl.app.</c> over every denial; each also upper-cased and
    /// under <c>baggage.</c>.
    /// </summary>
    private static IEnumerable<string> Corpus()
    {
        var denied = RegistryPolicy.NotPersistedKeys
            .Concat(RegistryPolicy.NotPersistedKeys.Select(static key => key + ".child"))
            .Concat(RegistryPolicy.NotPersistedPrefixes.Select(static prefix => prefix + "email"))
            .ToArray();
        var named = denied
            .Concat(denied.Select(static key => AppPrefix + key))
            .Concat(RegistryPolicy.TokenExemptKeys)
            .Concat(RegistryPolicy.VendorKeys)
            .Concat(RegistryPolicy.DeniedKeyTokens.Select(static token => "custom." + token))
            .Concat(CollectorSemanticAttributeCatalog.SpanAttributeAllowList)
            .Concat(CollectorSemanticAttributeCatalog.LogAttributeAllowList)
            .Concat(CollectorSemanticAttributeCatalog.MetricAttributeAllowList)
            .Concat(CollectorSemanticAttributeCatalog.ResourceAttributeAllowList)
            .Concat(CollectorSemanticAttributeCatalog.QylResourceAttributeAllowList)
            .Concat([
                "http.request.header.accept", "http.request.header.content_type", "http.request.header.mcp_method",
                "http.request.header.mcp_name", "http.request.header.mcp_protocol_version", "http.response.header.content_type",
                "gen_ai.agent.name", "qyl.app.order.id", "qyl.app.order.message", "cicd.pipeline.run.url.full"
            ])
            .ToArray();

        return named
            .Concat(named.Select(static key => key.ToUpperInvariant()))
            .Concat(named.Select(static key => "baggage." + key))
            .Distinct(StringComparer.Ordinal);
    }

    private static string[] Sorted(IEnumerable<string> values) => [.. values.Order(StringComparer.Ordinal)];

    private static JsonDocument PolicyFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "qyl.slnx")))
                return JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "eng", "config", "collector-semantic-policy.json")));
        }

        throw new InvalidOperationException($"No qyl.slnx above {AppContext.BaseDirectory}.");
    }
}
