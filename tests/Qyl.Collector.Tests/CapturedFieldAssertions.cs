using System.Text.Json;
using Qyl.Collector.Ingestion;
using Qyl.Telemetry.SemanticConventions;

namespace Qyl.Collector.Tests;

internal static class CapturedFieldAssertions
{
    public static void HasPersistencePolicy(string key, bool expected)
    {
        Assert.AreEqual(expected, RegistryPolicy.IsPersisted(key), key);
        Assert.IsTrue(CollectorSemanticAttributeCatalog.TryGetCapturedFieldPersistence(key, out var generated), key);
        Assert.AreEqual(expected, generated, key);
        Assert.AreEqual(expected, AttributeKeySets.IsPersistable(key), key);
        Assert.AreEqual(expected, AttributeKeySets.ShouldCaptureSpanAttribute(key), key);
    }

    public static void HasStringArray(JsonElement actual, string[] expected)
    {
        Assert.AreEqual(JsonValueKind.Array, actual.ValueKind);
        Assert.AreSequenceEqual(expected, actual.EnumerateArray().Select(static item => item.GetString()));
    }
}
