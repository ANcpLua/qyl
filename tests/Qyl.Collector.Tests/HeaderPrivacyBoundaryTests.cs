using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class HeaderPrivacyBoundaryTests
{
    [Fact]
    public void Only_explicitly_safe_http_headers_can_be_persisted_on_spans()
    {
        var attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal)
        {
            ["http.request.method"] = OtlpAttributeValue.FromString("POST"),
            ["http.request.header.accept"] = OtlpAttributeValue.FromString("application/json"),
            ["http.request.header.mcp_method"] = OtlpAttributeValue.FromString("tools/call"),
            ["http.request.header.mcp_name"] = OtlpAttributeValue.FromString("lookup"),
            ["http.request.header.x_tool_controlled"] =
                OtlpAttributeValue.FromString("arbitrary-tool-secret"),
            ["http.request.header.mcp_param_innocent"] =
                OtlpAttributeValue.FromString("c2Vuc2l0aXZlLXRvb2wtYXJndW1lbnQ="),
            ["http.request.header.authorization"] =
                OtlpAttributeValue.FromString("Bearer credential"),
            ["http.request.header.cookie"] = OtlpAttributeValue.FromString("session=credential"),
            ["http.response.header.set_cookie"] =
                OtlpAttributeValue.FromString("session=credential")
        };

        var json = Assert.IsType<string>(PersistedAttributePolicy.SerializeSpanAttributes(attributes));

        Assert.Contains("\"http.request.method\":\"POST\"", json, StringComparison.Ordinal);
        Assert.Contains("\"http.request.header.accept\":\"application/json\"", json, StringComparison.Ordinal);
        Assert.Contains("\"http.request.header.mcp_method\":\"tools/call\"", json, StringComparison.Ordinal);
        Assert.Contains("\"http.request.header.mcp_name\":\"lookup\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("x_tool_controlled", json, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp_param", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("arbitrary-tool-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("c2Vuc2l0aXZlLXRvb2wtYXJndW1lbnQ=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("credential", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http.request.header.x_any_name")]
    [InlineData("http.request.header.mcp-param-innocent")]
    [InlineData("http.request.header.mcp_param_innocent")]
    [InlineData("http.request.header.mcp%2dparam%2dinnocent")]
    [InlineData("http.response.header.x_any_name")]
    public void Dynamically_named_headers_are_rejected_from_every_non_span_boundary(string key)
    {
        Assert.False(AttributeKeySets.IsSafeLogAttribute(key));
        Assert.False(AttributeKeySets.IsSafeResourceAttribute(key));
        Assert.False(AttributeKeySets.IsSafeEntityReferencedResourceAttribute(key));
    }
}
