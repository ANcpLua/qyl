// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9038180+00:00
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
    [System.Runtime.Serialization.EnumMember(Value = "0")]
    _0 = 0,
    [System.Runtime.Serialization.EnumMember(Value = "1")]
    _1 = 1,
    [System.Runtime.Serialization.EnumMember(Value = "2")]
    _2 = 2,
}

/// <summary>Data point flags</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DataPointFlags>))]
public enum DataPointFlags
{
    [System.Runtime.Serialization.EnumMember(Value = "0")]
    _0 = 0,
    [System.Runtime.Serialization.EnumMember(Value = "1")]
    _1 = 1,
}

/// <summary>Metric data type</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<MetricType>))]
public enum MetricType
{
    [System.Runtime.Serialization.EnumMember(Value = "gauge")]
    Gauge = 0,
    [System.Runtime.Serialization.EnumMember(Value = "sum")]
    Sum = 1,
    [System.Runtime.Serialization.EnumMember(Value = "histogram")]
    Histogram = 2,
    [System.Runtime.Serialization.EnumMember(Value = "exponential_histogram")]
    ExponentialHistogram = 3,
    [System.Runtime.Serialization.EnumMember(Value = "summary")]
    Summary = 4,
}

/// <summary>Log severity number following OTel specification (1-24)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SeverityNumber>))]
public enum SeverityNumber
{
    [System.Runtime.Serialization.EnumMember(Value = "0")]
    _0 = 0,
    [System.Runtime.Serialization.EnumMember(Value = "1")]
    _1 = 1,
    [System.Runtime.Serialization.EnumMember(Value = "2")]
    _2 = 2,
    [System.Runtime.Serialization.EnumMember(Value = "3")]
    _3 = 3,
    [System.Runtime.Serialization.EnumMember(Value = "4")]
    _4 = 4,
    [System.Runtime.Serialization.EnumMember(Value = "5")]
    _5 = 5,
    [System.Runtime.Serialization.EnumMember(Value = "6")]
    _6 = 6,
    [System.Runtime.Serialization.EnumMember(Value = "7")]
    _7 = 7,
    [System.Runtime.Serialization.EnumMember(Value = "8")]
    _8 = 8,
    [System.Runtime.Serialization.EnumMember(Value = "9")]
    _9 = 9,
    [System.Runtime.Serialization.EnumMember(Value = "10")]
    _10 = 10,
    [System.Runtime.Serialization.EnumMember(Value = "11")]
    _11 = 11,
    [System.Runtime.Serialization.EnumMember(Value = "12")]
    _12 = 12,
    [System.Runtime.Serialization.EnumMember(Value = "13")]
    _13 = 13,
    [System.Runtime.Serialization.EnumMember(Value = "14")]
    _14 = 14,
    [System.Runtime.Serialization.EnumMember(Value = "15")]
    _15 = 15,
    [System.Runtime.Serialization.EnumMember(Value = "16")]
    _16 = 16,
    [System.Runtime.Serialization.EnumMember(Value = "17")]
    _17 = 17,
    [System.Runtime.Serialization.EnumMember(Value = "18")]
    _18 = 18,
    [System.Runtime.Serialization.EnumMember(Value = "19")]
    _19 = 19,
    [System.Runtime.Serialization.EnumMember(Value = "20")]
    _20 = 20,
    [System.Runtime.Serialization.EnumMember(Value = "21")]
    _21 = 21,
    [System.Runtime.Serialization.EnumMember(Value = "22")]
    _22 = 22,
    [System.Runtime.Serialization.EnumMember(Value = "23")]
    _23 = 23,
    [System.Runtime.Serialization.EnumMember(Value = "24")]
    _24 = 24,
}

/// <summary>Log severity text (human-readable)</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SeverityText>))]
public enum SeverityText
{
    [System.Runtime.Serialization.EnumMember(Value = "TRACE")]
    TRACE = 0,
    [System.Runtime.Serialization.EnumMember(Value = "DEBUG")]
    DEBUG = 1,
    [System.Runtime.Serialization.EnumMember(Value = "INFO")]
    INFO = 2,
    [System.Runtime.Serialization.EnumMember(Value = "WARN")]
    WARN = 3,
    [System.Runtime.Serialization.EnumMember(Value = "ERROR")]
    ERROR = 4,
    [System.Runtime.Serialization.EnumMember(Value = "FATAL")]
    FATAL = 5,
}

/// <summary>Span kind describing the relationship between spans</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SpanKind>))]
public enum SpanKind
{
    [System.Runtime.Serialization.EnumMember(Value = "0")]
    _0 = 0,
    [System.Runtime.Serialization.EnumMember(Value = "1")]
    _1 = 1,
    [System.Runtime.Serialization.EnumMember(Value = "2")]
    _2 = 2,
    [System.Runtime.Serialization.EnumMember(Value = "3")]
    _3 = 3,
    [System.Runtime.Serialization.EnumMember(Value = "4")]
    _4 = 4,
    [System.Runtime.Serialization.EnumMember(Value = "5")]
    _5 = 5,
}

/// <summary>Span status code</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SpanStatusCode>))]
public enum SpanStatusCode
{
    [System.Runtime.Serialization.EnumMember(Value = "0")]
    _0 = 0,
    [System.Runtime.Serialization.EnumMember(Value = "1")]
    _1 = 1,
    [System.Runtime.Serialization.EnumMember(Value = "2")]
    _2 = 2,
}

/// <summary>Telemetry SDK language</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TelemetrySdkLanguage>))]
public enum TelemetrySdkLanguage
{
    [System.Runtime.Serialization.EnumMember(Value = "cpp")]
    Cpp = 0,
    [System.Runtime.Serialization.EnumMember(Value = "dotnet")]
    Dotnet = 1,
    [System.Runtime.Serialization.EnumMember(Value = "erlang")]
    Erlang = 2,
    [System.Runtime.Serialization.EnumMember(Value = "go")]
    Go = 3,
    [System.Runtime.Serialization.EnumMember(Value = "java")]
    Java = 4,
    [System.Runtime.Serialization.EnumMember(Value = "nodejs")]
    Nodejs = 5,
    [System.Runtime.Serialization.EnumMember(Value = "php")]
    Php = 6,
    [System.Runtime.Serialization.EnumMember(Value = "python")]
    Python = 7,
    [System.Runtime.Serialization.EnumMember(Value = "ruby")]
    Ruby = 8,
    [System.Runtime.Serialization.EnumMember(Value = "rust")]
    Rust = 9,
    [System.Runtime.Serialization.EnumMember(Value = "swift")]
    Swift = 10,
    [System.Runtime.Serialization.EnumMember(Value = "webjs")]
    Webjs = 11,
}

