using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests;

public sealed class DeprecatedAttributeNormalizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Proven_producer_keys_are_normalized_before_capture_and_canonical_values_win(
        bool canonicalFirst)
    {
        var legacy = new[]
        {
            StringAttribute("gen_ai.system", "legacy-provider"),
            IntAttribute("gen_ai.usage.prompt_tokens", 10),
            IntAttribute("gen_ai.usage.completion_tokens", 30),
            StringAttribute("agents.tool.call_id", "legacy-call"),
            StringAttribute("db.system", "legacy-db")
        };
        var canonical = new[]
        {
            StringAttribute(CollectorSemanticAttributeCatalog.GenAiProviderName, "canonical-provider"),
            IntAttribute(CollectorSemanticAttributeCatalog.GenAiInputTokens, 20),
            IntAttribute(CollectorSemanticAttributeCatalog.GenAiOutputTokens, 40),
            StringAttribute(CollectorSemanticAttributeCatalog.GenAiToolCallId, "canonical-call"),
            StringAttribute(CollectorSemanticAttributeCatalog.DbSystemName, "canonical-db")
        };
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(new byte[16]),
            SpanId = ByteString.CopyFrom(new byte[8]),
            Name = "compatibility-boundary",
            StartTimeUnixNano = 1,
            EndTimeUnixNano = 2
        };
        span.Attributes.Add(canonicalFirst ? canonical : legacy);
        span.Attributes.Add(canonicalFirst ? legacy : canonical);

        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource(),
                    ScopeSpans = { new ScopeSpans { Spans = { span } } }
                }
            }
        };

        var attributes = Assert.Single(OtlpConverter.ConvertTraceRequest(request).Spans).Attributes;
        Assert.Equal("canonical-provider", attributes[CollectorSemanticAttributeCatalog.GenAiProviderName].AsString());
        Assert.Equal(20, attributes[CollectorSemanticAttributeCatalog.GenAiInputTokens].AsInt64());
        Assert.Equal(40, attributes[CollectorSemanticAttributeCatalog.GenAiOutputTokens].AsInt64());
        Assert.Equal("canonical-db", attributes[CollectorSemanticAttributeCatalog.DbSystemName].AsString());
        Assert.True(DeprecatedAttributeNormalizer.TryNormalize("agents.tool.call_id", out var toolCallKey));
        Assert.Equal(CollectorSemanticAttributeCatalog.GenAiToolCallId, toolCallKey);
        Assert.DoesNotContain(CollectorSemanticAttributeCatalog.GenAiToolCallId, attributes.Keys);
        Assert.DoesNotContain("gen_ai.system", attributes.Keys);
        Assert.DoesNotContain("gen_ai.usage.prompt_tokens", attributes.Keys);
        Assert.DoesNotContain("gen_ai.usage.completion_tokens", attributes.Keys);
        Assert.DoesNotContain("agents.tool.call_id", attributes.Keys);
        Assert.DoesNotContain("db.system", attributes.Keys);
    }

    private static KeyValue StringAttribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static KeyValue IntAttribute(string key, long value) =>
        new() { Key = key, Value = new AnyValue { IntValue = value } };
}
