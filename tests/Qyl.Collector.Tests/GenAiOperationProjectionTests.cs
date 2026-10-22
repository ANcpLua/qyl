using OpenTelemetry.Proto.Common.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class GenAiOperationProjectionTests
{
    private const string GenAiScope = "Experimental.Microsoft.Extensions.AI";
    private const string OperationNameKey = "gen_ai.operation.name";
    private const string FetchResponse = "fetch_response";

    [TestMethod]
    public void SpanOperationName_FetchResponse_IsTheOperationColumn()
    {
        var row = SpanRow([OtlpTestSpans.String(OperationNameKey, FetchResponse)]);

        Assert.AreEqual(FetchResponse, row.GenAiOperationName);
    }

    [TestMethod]
    public void SpanAttributes_FetchResponse_KeepsOperationName()
    {
        var row = SpanRow([OtlpTestSpans.String(OperationNameKey, FetchResponse)]);

        Assert.Contains($"\"{OperationNameKey}\":\"{FetchResponse}\"", row.AttributesJson ?? string.Empty, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SpanOperationName_UpperCaseFetchResponse_IsCanonicalLowerCase()
    {
        var row = SpanRow([OtlpTestSpans.String(OperationNameKey, "FETCH_RESPONSE")]);

        Assert.AreEqual(FetchResponse, row.GenAiOperationName);
    }

    [TestMethod]
    public void SpanOperationName_UnknownValue_IsNull()
    {
        var row = SpanRow([OtlpTestSpans.String(OperationNameKey, "teleport")]);

        Assert.IsNull(row.GenAiOperationName);
    }

    private static SpanStorageRow SpanRow(KeyValue[] attributes) =>
        IngestionStorageMapper.ToSpanStorageRows(OtlpConverter.ConvertTraceRequest(OtlpTestSpans.Request(GenAiScope, attributes)))[0];
}
