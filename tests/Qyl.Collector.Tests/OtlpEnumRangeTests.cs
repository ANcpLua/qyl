using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Proto.Logs.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;
using OtlpStatus = OpenTelemetry.Proto.Trace.V1.Status;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class OtlpEnumRangeTests
{
    private const string Scope = "app";
    private const SeverityNumber SeverityBeyondFatal4 = (SeverityNumber)25;
    private const SeverityNumber NegativeSeverity = (SeverityNumber)(-3);
    private const OtlpSpan.Types.SpanKind UndefinedSpanKind = (OtlpSpan.Types.SpanKind)9;
    private const OtlpStatus.Types.StatusCode UndefinedStatusCode = (OtlpStatus.Types.StatusCode)7;

    [TestMethod]
    public void LogSeverity_Fatal4_IsKept()
    {
        var row = LogRows(LogBatch(SeverityNumber.Fatal4))[0];

        Assert.AreEqual((byte)24, row.SeverityNumber);
    }

    [TestMethod]
    [DataRow(SeverityBeyondFatal4)]
    [DataRow(NegativeSeverity)]
    public void LogSeverity_OutOfRange_IsStoredAsUnspecified(SeverityNumber severity)
    {
        var row = LogRows(LogBatch(severity))[0];

        Assert.AreEqual((byte)0, row.SeverityNumber);
    }

    [TestMethod]
    public void LogSeverity_OutOfRange_IsNotRejected()
    {
        var batch = LogBatch(SeverityBeyondFatal4);

        Assert.AreEqual(0, batch.RejectedLogRecords);
    }

    [TestMethod]
    public void LogSeverity_OutOfRange_WarnsInRejectionMessage()
    {
        var batch = LogBatch(SeverityBeyondFatal4);

        Assert.Contains("severity_number", batch.RejectionMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [TestMethod]
    public void LogSeverity_InRange_HasNoRejectionMessage()
    {
        var batch = LogBatch(SeverityNumber.Warn);

        Assert.IsNull(batch.RejectionMessage);
    }

    [TestMethod]
    public void SpanKind_Undefined_IsStoredAsUnspecified()
    {
        var row = SpanRows(SpanBatch(UndefinedSpanKind))[0];

        Assert.AreEqual((byte)0, row.Kind);
    }

    [TestMethod]
    public void SpanKind_Undefined_WarnsInRejectionMessage()
    {
        var batch = SpanBatch(UndefinedSpanKind);

        Assert.Contains("kind", batch.RejectionMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SpanKind_Undefined_IsNotRejected()
    {
        var batch = SpanBatch(UndefinedSpanKind);

        Assert.AreEqual(0, batch.RejectedSpans);
    }

    [TestMethod]
    public void SpanKind_Consumer_IsKept()
    {
        var row = SpanRows(SpanBatch(OtlpSpan.Types.SpanKind.Consumer))[0];

        Assert.AreEqual((byte)5, row.Kind);
    }

    [TestMethod]
    public void SpanStatusCode_Undefined_IsStoredAsUnset()
    {
        var row = SpanRows(SpanBatch(status: new OtlpStatus { Code = UndefinedStatusCode }))[0];

        Assert.AreEqual((byte)0, row.StatusCode);
    }

    [TestMethod]
    public void SpanStatusCode_Undefined_WarnsInRejectionMessage()
    {
        var batch = SpanBatch(status: new OtlpStatus { Code = UndefinedStatusCode });

        Assert.Contains("status", batch.RejectionMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SpanStatusCode_Error_IsKept()
    {
        var row = SpanRows(SpanBatch(status: new OtlpStatus { Code = OtlpStatus.Types.StatusCode.Error }))[0];

        Assert.AreEqual((byte)2, row.StatusCode);
    }

    private static LogIngestionBatch LogBatch(SeverityNumber severity) =>
        OtlpConverter.ConvertLogs(
            OtlpTestLogs.Request(Scope, [], severity: severity),
            new FakeTimeProvider(CollectorApiTestHost.FixtureUtcNow));

    private static List<LogStorageRow> LogRows(LogIngestionBatch batch) => IngestionStorageMapper.ToLogStorageRows(batch);

    private static TraceIngestionBatch SpanBatch(
        OtlpSpan.Types.SpanKind kind = OtlpSpan.Types.SpanKind.Internal,
        OtlpStatus? status = null) =>
        OtlpConverter.ConvertTraceRequest(OtlpTestSpans.Request(Scope, [], kind: kind, status: status));

    private static List<SpanStorageRow> SpanRows(TraceIngestionBatch batch) => IngestionStorageMapper.ToSpanStorageRows(batch);
}
