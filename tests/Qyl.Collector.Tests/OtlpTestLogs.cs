using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Qyl.Collector.Tests;

/// <summary>One-record OTLP log exports, shaped the way an SDK exporter sends them.</summary>
internal static class OtlpTestLogs
{
    private const ulong Now = CollectorApiTestHost.FixtureUnixNano - 120_000_000_000;

    /// <summary>
    /// One log record of service <paramref name="serviceName"/> on the scope <paramref name="scopeName"/>, carrying
    /// <paramref name="attributes"/> in wire order.
    /// </summary>
    internal static ExportLogsServiceRequest Request(
        string scopeName,
        IEnumerable<KeyValue> attributes,
        string serviceName = "app-service")
    {
        var record = new LogRecord { TimeUnixNano = Now, EventName = "work" };
        record.Attributes.Add(attributes);

        return new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new ResourceLogs
                {
                    Resource = new Resource { Attributes = { OtlpTestSpans.String("service.name", serviceName) } },
                    ScopeLogs = { new ScopeLogs { Scope = new InstrumentationScope { Name = scopeName }, LogRecords = { record } } }
                }
            }
        };
    }
}
