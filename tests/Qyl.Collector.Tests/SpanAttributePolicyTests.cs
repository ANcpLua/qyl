using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests;

public sealed class SpanAttributePolicyTests
{
    [Theory]
    [InlineData("graphql.operation.name")]
    [InlineData("graphql.operation.type")]
    public void GraphQlOperationAttributes_AreCaptured(string key)
    {
        Assert.True(AttributeKeySets.IsSafeSpanAttribute(key));
        Assert.True(AttributeKeySets.ShouldCaptureSpanAttribute(key));
    }

    [Fact]
    public void GraphQlDocument_IsDeniedAsPayload()
    {
        Assert.False(AttributeKeySets.IsSafeSpanAttribute("graphql.document"));
        Assert.False(AttributeKeySets.ShouldCaptureSpanAttribute("graphql.document"));
    }
}
