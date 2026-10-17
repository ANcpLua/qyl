using System.Text.Json;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using Qyl.Telemetry.SemanticConventions;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class HeaderPrivacyBoundaryTests
{
    private static readonly string[] s_retainedValues = ["first", "second"];

    [TestMethod]
    [DataRow("http.request.header", true)]
    [DataRow("http.response.header", true)]
    [DataRow("rpc.request.metadata", false)]
    [DataRow("rpc.response.metadata", false)]
    public void Captured_fields_use_the_registry_rule_at_ingestion_and_storage(string root, bool http)
    {
        string[] allowed = ["x-request-id", "x_request_id", "x.request.id", "x-message-id", "content-type",
            "x-ratelimit-remaining", "x-tenant-id", "x-demo-res-md", "mcp-param-innocent", "key", "x-auth"];
        string[] denied = ["authorization", "proxy-authorization", "cookie", "set-cookie", "x-api-key",
            "www-authenticate", "proxy-authenticate", "x-auth-token", "x-token-count", "x-secret", "x-password",
            "x-passwd", "x-apikey", "x-api_key", "x-credential", "x-private_key", "x-private-key", "x-privatekey",
            "x-client_secret", "x-jwt", "", "bad name", "bad:name", "bad/name", "naïve"];
        var attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        foreach (var suffix in allowed)
        {
            Check(root + "." + suffix, true);
            Check((root + "." + suffix).ToUpperInvariant(), true);
            attributes[root + "." + suffix] = OtlpAttributeValue.FromArray(
                [OtlpAttributeValue.FromString("first"), OtlpAttributeValue.FromString("second")]);
        }
        foreach (var suffix in denied)
        {
            Check(root + "." + suffix, false);
            Check((root + "." + suffix).ToUpperInvariant(), false);
            attributes[root + "." + suffix] = OtlpAttributeValue.FromArray([OtlpAttributeValue.FromString("opaque-denied")]);
        }
        Check(root, false);
        Check(root + ".x+feature", http);
        using var stored = JsonDocument.Parse(PersistedAttributePolicy.SerializeAttributes(attributes)!);
        Assert.HasCount(allowed.Length, stored.RootElement.EnumerateObject());
        foreach (var suffix in allowed)
            Assert.AreSequenceEqual(s_retainedValues, stored.RootElement.GetProperty(root + "." + suffix).EnumerateArray().Select(static value => value.GetString()!));
    }

    private static void Check(string key, bool expected)
    {
        Assert.AreEqual(expected, RegistryPolicy.IsPersisted(key), key);
        Assert.IsTrue(CollectorSemanticAttributeCatalog.TryGetCapturedFieldPersistence(key, out var generated), key);
        Assert.AreEqual(expected, generated, key);
        Assert.AreEqual(expected, AttributeKeySets.IsPersistable(key), key);
        Assert.AreEqual(expected, AttributeKeySets.ShouldCaptureSpanAttribute(key), key);
    }
}
