using System.Text.Json;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

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
            "x-ratelimit-remaining", "x-tenant-id", "x-demo-res-md", "mcp-param-innocent", "key", "x-auth",
            "authorization", "proxy-authorization", "cookie", "set-cookie", "x-api-key",
            "www-authenticate", "proxy-authenticate", "x-auth-token", "x-token-count", "x-secret", "x-password",
            "x-passwd", "x-apikey", "x-api_key", "x-credential", "x-private_key", "x-private-key", "x-privatekey",
            "x-client_secret", "x-jwt", "trace-bin"];
        string[] denied = ["", "bad name", "bad:name", "bad/name", "naïve"];
        var attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        foreach (var suffix in allowed)
        {
            CapturedFieldAssertions.HasPersistencePolicy(root + "." + suffix, true);
            CapturedFieldAssertions.HasPersistencePolicy((root + "." + suffix).ToUpperInvariant(), true);
            attributes[root + "." + suffix] = OtlpAttributeValue.FromArray(
                [OtlpAttributeValue.FromString("first"), OtlpAttributeValue.FromString("second")]);
        }
        foreach (var suffix in denied)
        {
            CapturedFieldAssertions.HasPersistencePolicy(root + "." + suffix, false);
            CapturedFieldAssertions.HasPersistencePolicy((root + "." + suffix).ToUpperInvariant(), false);
            attributes[root + "." + suffix] = OtlpAttributeValue.FromArray([OtlpAttributeValue.FromString("opaque-denied")]);
        }
        CapturedFieldAssertions.HasPersistencePolicy(root, false);
        CapturedFieldAssertions.HasPersistencePolicy(root + ".x+feature", http);
        using var stored = JsonDocument.Parse(PersistedAttributePolicy.SerializeAttributes(attributes)!);
        Assert.HasCount(allowed.Length, stored.RootElement.EnumerateObject());
        foreach (var suffix in allowed)
            CapturedFieldAssertions.HasStringArray(stored.RootElement.GetProperty(root + "." + suffix), s_retainedValues);
    }
}
