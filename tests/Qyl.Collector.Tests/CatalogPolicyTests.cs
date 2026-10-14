using System.Text.Json;
using Qyl.Collector.Ingestion;
using Qyl.Telemetry.SemanticConventions;
using Qyl.Telemetry.SemanticConventions.Incubating.Mapping;

namespace Qyl.Collector.Tests;

/// <summary>
/// The deny list has one home, SemConv's generated <see cref="RegistryPolicy"/>: the collector catalog is
/// generated from it, the policy file holds no deny section of its own, and every key the policy file names
/// for persistence is one the registry persists. A typed view can then only assert what ingest keeps.
/// </summary>
public sealed class CatalogPolicyTests
{
    [Theory]
    [InlineData("deniedExactPrefixes")]
    [InlineData("deniedExactKeys")]
    [InlineData("deniedKeyTokens")]
    [InlineData("deniedTokenExemptKeys")]
    public void The_policy_file_carries_no_deny_section(string section)
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

        // POL's five exemptions plus the five hot token counts the collector lifts into columns.
        Assert.Equal(10, CollectorSemanticAttributeCatalog.TokenExemptKeys.Count);
        Assert.True(CollectorSemanticAttributeCatalog.TokenExemptKeys.Contains("gen_ai.usage.reasoning.output_tokens"));
        Assert.False(CollectorSemanticAttributeCatalog.NotPersistedKeys.Contains("gen_ai.agent.name"));
    }

    [Fact]
    public void The_collector_persists_exactly_what_RegistryPolicy_persists()
    {
        var disagreements = Corpus()
            .Where(static key => RegistryPolicy.IsPersisted(key) != AttributeKeySets.IsPersistable(key))
            .Select(static key => $"{key}: registry {RegistryPolicy.IsPersisted(key)}, collector {AttributeKeySets.IsPersistable(key)}")
            .ToArray();

        Assert.Empty(disagreements);
    }

    [Fact]
    public void Every_registry_vendor_key_is_a_collector_vendor_pass_through() =>
        Assert.All(RegistryPolicy.VendorKeys, static key => Assert.True(AttributeMapping.IsVendorPassThrough(key), key));

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

    /// <summary>Every key either side names, the template children of the denied ones, and keys that carry a denied token.</summary>
    private static IEnumerable<string> Corpus() =>
        RegistryPolicy.NotPersistedKeys
            .Concat(RegistryPolicy.NotPersistedKeys.Select(static key => key + ".child"))
            .Concat(RegistryPolicy.NotPersistedPrefixes.Select(static prefix => prefix + "email"))
            .Concat(RegistryPolicy.TokenExemptKeys)
            .Concat(RegistryPolicy.VendorKeys)
            .Concat(RegistryPolicy.DeniedKeyTokens.Select(static token => "custom." + token))
            .Concat(CollectorSemanticAttributeCatalog.SpanAttributeAllowList)
            .Concat(CollectorSemanticAttributeCatalog.LogAttributeAllowList)
            .Concat(CollectorSemanticAttributeCatalog.MetricAttributeAllowList)
            .Concat(CollectorSemanticAttributeCatalog.ResourceAttributeAllowList)
            .Concat(["gen_ai.agent.name", "qyl.app.order.id", "qyl.app.order.message", "cicd.pipeline.run.url.full"])
            .Distinct(StringComparer.Ordinal);

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
