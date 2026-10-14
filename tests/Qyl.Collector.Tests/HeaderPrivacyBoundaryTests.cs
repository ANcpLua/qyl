using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class HeaderPrivacyBoundaryTests
{
    /// <summary>
    /// The registry denies every <c>http.request.header.&lt;name&gt;</c> and <c>http.response.header.&lt;name&gt;</c>, and
    /// the collector names no exception: <c>mcp_name</c> carries the resource URIs and prompt names the registry denies
    /// as attributes of their own, and a header value is whatever the caller chose to send.
    /// </summary>
    [Fact]
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

        var json = Assert.IsType<string>(PersistedAttributePolicy.SerializeSpanAttributes(attributes));

        Assert.Equal("""{"http.request.method":"POST"}""", json);
    }

    [Theory]
    [InlineData("http.request.header.x_any_name")]
    [InlineData("http.request.header.mcp-param-innocent")]
    [InlineData("http.request.header.mcp_param_innocent")]
    [InlineData("http.request.header.mcp%2dparam%2dinnocent")]
    [InlineData("http.response.header.x_any_name")]
    [InlineData("http.request.header.mcp_name")]
    [InlineData("HTTP.REQUEST.HEADER.MCP_NAME")]
    public void Headers_are_rejected_from_every_boundary(string key)
    {
        Assert.False(AttributeKeySets.IsSafeSpanAttribute(key));
        Assert.False(AttributeKeySets.IsSafeLogAttribute(key));
        Assert.False(AttributeKeySets.IsSafeMetricAttribute(key));
        Assert.False(AttributeKeySets.IsSafeResourceAttribute(key));
        Assert.False(AttributeKeySets.IsSafeEntityReferencedResourceAttribute(key));
    }
}
