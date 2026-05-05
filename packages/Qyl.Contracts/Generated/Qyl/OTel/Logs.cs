#nullable enable

namespace Qyl.OTel.Logs;

public sealed class LogRecord
{
    public required long TimeUnixNano { get; init; }
    public required long ObservedTimeUnixNano { get; init; }
    public required Qyl.OTel.Enums.SeverityNumber SeverityNumber { get; init; }
    public Qyl.OTel.Enums.SeverityText? SeverityText { get; init; }
    public required object Body { get; init; }
    public IReadOnlyList<Qyl.Common.Attribute>? Attributes { get; init; }
    public long? DroppedAttributesCount { get; init; }
    public int? Flags { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public required Qyl.OTel.Resource.Resource Resource { get; init; }
    public Qyl.Common.InstrumentationScope? InstrumentationScope { get; init; }
}

public sealed class LogBodyString
{
    public required string StringValue { get; init; }
}

public sealed class LogBodyKvList
{
    public required IReadOnlyList<Qyl.Common.Attribute> KvListValue { get; init; }
}

public sealed class LogBodyArray
{
    public required IReadOnlyList<object> ArrayValue { get; init; }
}

public sealed class LogBodyBytes
{
    public required ReadOnlyMemory<byte> BytesValue { get; init; }
}

public sealed class LogQueryFilters
{
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public Qyl.OTel.Enums.SeverityNumber? MinSeverity { get; init; }
    public Qyl.OTel.Enums.SeverityNumber? MaxSeverity { get; init; }
    public Qyl.OTel.Enums.SeverityText? SeverityText { get; init; }
    public string? ServiceName { get; init; }
    public string? Search { get; init; }
    public string? Attribute { get; init; }
}

public sealed class LogStats
{
    public required long TotalCount { get; init; }
    public required IReadOnlyList<Qyl.OTel.Logs.LogCountBySeverity> BySeverity { get; init; }
    public required IReadOnlyList<Qyl.OTel.Logs.LogCountByDimension> ByService { get; init; }
    public required double LogsPerSecond { get; init; }
    public required double ErrorRate { get; init; }
}

public sealed class LogCountBySeverity
{
    public required Qyl.OTel.Enums.SeverityText Severity { get; init; }
    public required long Count { get; init; }
    public required double Percentage { get; init; }
}

public sealed class LogCountByDimension
{
    public required string Dimension { get; init; }
    public required long Count { get; init; }
    public required long ErrorCount { get; init; }
}

public sealed class LogTemplate
{
    public required string Template { get; init; }
    public required IReadOnlyList<Qyl.Common.Attribute> Parameters { get; init; }
    public string? RenderedMessage { get; init; }
}
