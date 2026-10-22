using OpenTelemetry.Proto.Common.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class GenAiUsageProjectionTests
{
    private const string GenAiScope = "Experimental.Microsoft.Extensions.AI";
    private const string InputTokensKey = "gen_ai.usage.input_tokens";

    [TestMethod]
    public void SpanInputTokens_IntValue_IsTheTokenColumn()
    {
        var row = SpanRow([OtlpTestSpans.Int(InputTokensKey, 120)]);

        Assert.AreEqual(120L, row.GenAiInputTokens);
    }

    [TestMethod]
    public void SpanInputTokens_NumericString_IsNull()
    {
        var row = SpanRow([OtlpTestSpans.String(InputTokensKey, "120")]);

        Assert.IsNull(row.GenAiInputTokens);
    }

    [TestMethod]
    public void SpanAttributes_NumericStringInputTokens_KeepsTheString()
    {
        var row = SpanRow([OtlpTestSpans.String(InputTokensKey, "120")]);

        Assert.Contains($"\"{InputTokensKey}\":\"120\"", row.AttributesJson ?? string.Empty, StringComparison.Ordinal);
    }

    private static SpanStorageRow SpanRow(KeyValue[] attributes) =>
        IngestionStorageMapper.ToSpanStorageRows(OtlpConverter.ConvertTraceRequest(OtlpTestSpans.Request(GenAiScope, attributes)))[0];
}
