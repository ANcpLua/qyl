// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2393690+00:00
//     Enumeration types for Qyl.OTel.Enums
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.OTel.Enums;

/// <summary>Aggregation temporality for metrics</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AggregationTemporality>))]
public enum AggregationTemporality
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("0")]
    _0 = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1")]
    _1 = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("2")]
    _2 = 2,
}

/// <summary>Data point flags</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DataPointFlags>))]
public enum DataPointFlags
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("0")]
    _0 = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1")]
    _1 = 1,
}

/// <summary>Metric data type</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<MetricType>))]
public enum MetricType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("gauge")]
    Gauge = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("sum")]
    Sum = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("histogram")]
    Histogram = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("exponential_histogram")]
    ExponentialHistogram = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("summary")]
    Summary = 4,
}

/// <summary>Log severity number following OTel specification (1-24)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SeverityNumber>))]
public enum SeverityNumber
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("0")]
    _0 = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1")]
    _1 = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("2")]
    _2 = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("3")]
    _3 = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("4")]
    _4 = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("5")]
    _5 = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("6")]
    _6 = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("7")]
    _7 = 7,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("8")]
    _8 = 8,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("9")]
    _9 = 9,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("10")]
    _10 = 10,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("11")]
    _11 = 11,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("12")]
    _12 = 12,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("13")]
    _13 = 13,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("14")]
    _14 = 14,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("15")]
    _15 = 15,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("16")]
    _16 = 16,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("17")]
    _17 = 17,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("18")]
    _18 = 18,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("19")]
    _19 = 19,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("20")]
    _20 = 20,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("21")]
    _21 = 21,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("22")]
    _22 = 22,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("23")]
    _23 = 23,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("24")]
    _24 = 24,
}

/// <summary>Log severity text (human-readable)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SeverityText>))]
public enum SeverityText
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("TRACE")]
    TRACE = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("DEBUG")]
    DEBUG = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("INFO")]
    INFO = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("WARN")]
    WARN = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ERROR")]
    ERROR = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("FATAL")]
    FATAL = 5,
}

/// <summary>Span kind describing the relationship between spans</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SpanKind>))]
public enum SpanKind
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("0")]
    _0 = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1")]
    _1 = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("2")]
    _2 = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("3")]
    _3 = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("4")]
    _4 = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("5")]
    _5 = 5,
}

/// <summary>Span status code</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SpanStatusCode>))]
public enum SpanStatusCode
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("0")]
    _0 = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1")]
    _1 = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("2")]
    _2 = 2,
}

/// <summary>Telemetry SDK language</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TelemetrySdkLanguage>))]
public enum TelemetrySdkLanguage
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("cpp")]
    Cpp = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("dotnet")]
    Dotnet = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("erlang")]
    Erlang = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("go")]
    Go = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("java")]
    Java = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("nodejs")]
    Nodejs = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("php")]
    Php = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("python")]
    Python = 7,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ruby")]
    Ruby = 8,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("rust")]
    Rust = 9,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("swift")]
    Swift = 10,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("webjs")]
    Webjs = 11,
}

