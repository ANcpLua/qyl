using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class HeaderPrivacyBoundaryTests
{
    /// <summary>
    /// The registry denies every <c>http.request.header.&lt;name&gt;</c> and <c>http.response.header.&lt;name&gt;</c>, and
    /// the collector names no exception: <c>mcp_name</c> carries the resource URIs and prompt names the registry denies
    /// as attributes of their own, and a header value is whatever the caller chose to send.
    /// </summary>
    [TestMethod]
    public void No_http_header_is_persisted_on_spans()
    {
        var attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal)
        {
            ["http.request.method"] = OtlpAttributeValue.FromString("POST"),
            ["http.request.header.accept"] = OtlpAttributeValue.FromString("application/json"),
            ["http.request.header.content_type"] = OtlpAttributeValue.FromString("application/json"),
            ["http.request.header.mcp_method"] = OtlpAttributeValue.FromString("resources/read"),
            ["http.request.header.mcp_name"] = OtlpAttributeValue.FromString("file:///home/alice/notes.md"),
            ["http.request.header.mcp_protocol_version"] = OtlpAttributeValue.FromString("2025-06-18"),
            ["http.response.header.content_type"] = OtlpAttributeValue.FromString("text/event-stream"),
            ["http.request.header.x_tool_controlled"] =
                OtlpAttributeValue.FromString("arbitrary-tool-secret"),
            ["http.request.header.authorization"] =
                OtlpAttributeValue.FromString("Bearer credential"),
            ["http.response.header.set_cookie"] =
                OtlpAttributeValue.FromString("session=credential")
        };

        var json = Assert.IsExactInstanceOfType<string>(PersistedAttributePolicy.SerializeSpanAttributes(attributes));

        Assert.AreEqual("""{"http.request.method":"POST"}""", json);
    }

    [TestMethod]
    [DataRow("http.request.header.x_any_name")]
    [DataRow("http.request.header.mcp-param-innocent")]
    [DataRow("http.request.header.mcp_param_innocent")]
    [DataRow("http.request.header.mcp%2dparam%2dinnocent")]
    [DataRow("http.response.header.x_any_name")]
    [DataRow("http.request.header.mcp_name")]
    [DataRow("HTTP.REQUEST.HEADER.MCP_NAME")]
    public void Headers_are_rejected_from_every_boundary(string key)
    {
        Assert.IsFalse(AttributeKeySets.IsSafeSpanAttribute(key));
        Assert.IsFalse(AttributeKeySets.IsSafeLogAttribute(key));
        Assert.IsFalse(AttributeKeySets.IsSafeMetricAttribute(key));
        Assert.IsFalse(AttributeKeySets.IsSafeResourceAttribute(key));
        Assert.IsFalse(AttributeKeySets.IsSafeEntityReferencedResourceAttribute(key));
    }
}
