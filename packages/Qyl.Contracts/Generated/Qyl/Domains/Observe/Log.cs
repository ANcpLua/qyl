#nullable enable

namespace Qyl.Domains.Observe.Log;

public sealed class LogAttributes
{
    public string? RecordUid { get; init; }
    public string? FileName { get; init; }
    public string? FilePath { get; init; }
    public string? FileNameResolved { get; init; }
    public string? FilePathResolved { get; init; }
    public Qyl.Domains.Observe.Log.LogIostream? Iostream { get; init; }
}

public sealed class EventAttributes
{
    public required string Name { get; init; }
    public Qyl.Domains.Observe.Log.EventDomain? Domain { get; init; }
}

public sealed class SeverityMapping
{
    public required string Text { get; init; }
    public required Qyl.OTel.Enums.SeverityNumber Number { get; init; }
}

public sealed class LogQuery
{
    public string? Query { get; init; }
    public Qyl.OTel.Enums.SeverityNumber? SeverityMin { get; init; }
    public string? ServiceName { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public DateTimeOffset? TimeStart { get; init; }
    public DateTimeOffset? TimeEnd { get; init; }
    public IReadOnlyList<Qyl.Domains.Observe.Log.AttributeFilter>? AttributeFilters { get; init; }
    public int? Limit { get; init; }
    public Qyl.Domains.Observe.Log.LogOrderBy? OrderBy { get; init; }
}

public sealed class AttributeFilter
{
    public required string Key { get; init; }
    public required Qyl.Domains.Observe.Log.FilterOperator Operator { get; init; }
    public required string Value { get; init; }
}

public sealed class LogAggregation
{
    public required IReadOnlyList<string> GroupBy { get; init; }
    public required Qyl.Domains.Observe.Log.AggregationFunction Function { get; init; }
    public string? Field { get; init; }
    public Qyl.Domains.Observe.Log.TimeBucket? TimeBucket { get; init; }
    public int? TopN { get; init; }
}

public sealed class LogStats
{
    public required long TotalCount { get; init; }
    public required IReadOnlyList<Qyl.Domains.Observe.Log.LogSeverityStats> BySeverity { get; init; }
    public IReadOnlyList<Qyl.Domains.Observe.Log.LogServiceStats>? ByService { get; init; }
    public required double RatePerSecond { get; init; }
    public required double ErrorRate { get; init; }
}

public sealed class LogSeverityStats
{
    public required Qyl.OTel.Enums.SeverityNumber Severity { get; init; }
    public required string SeverityText { get; init; }
    public required long Count { get; init; }
    public required double Percentage { get; init; }
}

public sealed class LogServiceStats
{
    public required string ServiceName { get; init; }
    public required long Count { get; init; }
    public required long ErrorCount { get; init; }
    public required double RatePerSecond { get; init; }
}

public sealed class LogPattern
{
    public required string PatternId { get; init; }
    public required string Template { get; init; }
    public required string Sample { get; init; }
    public required long Count { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required Qyl.Domains.Observe.Log.LogPatternTrend Trend { get; init; }
    public IReadOnlyList<Qyl.Domains.Observe.Log.LogSeverityStats>? SeverityDistribution { get; init; }
}

public enum LogIostream
{
    Stdout,
    Stderr
}

public enum EventDomain
{
    Browser,
    Device,
    K8s,
    User,
    Session,
    App,
    FeatureFlag,
    Deployment,
    Cicd
}

public enum FilterOperator
{
    Eq,
    Neq,
    Contains,
    StartsWith,
    EndsWith,
    Regex,
    Gt,
    Gte,
    Lt,
    Lte,
    In,
    NotIn,
    Exists,
    NotExists
}

public enum LogOrderBy
{
    TimestampAsc,
    TimestampDesc,
    SeverityAsc,
    SeverityDesc
}

public enum AggregationFunction
{
    Count,
    Sum,
    Avg,
    Min,
    Max,
    P50,
    P90,
    P95,
    P99,
    CountDistinct
}

public enum TimeBucket
{
    S1,
    S10,
    S30,
    M1,
    M5,
    M15,
    M30,
    H1,
    H6,
    H12,
    D1,
    W1
}

public enum LogPatternTrend
{
    Increasing,
    Decreasing,
    Stable,
    New,
    Spike
}

public enum StructuredLogFormat
{
    Json,
    Logfmt,
    Clf,
    Combined,
    Syslog,
    W3c,
    Csv,
    Plaintext
}
