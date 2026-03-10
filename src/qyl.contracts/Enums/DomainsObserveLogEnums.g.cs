// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2391370+00:00
//     Enumeration types for Qyl.Domains.Observe.Log
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Domains.Observe.Log;

/// <summary>Aggregation functions</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AggregationFunction>))]
public enum AggregationFunction
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("count")]
    Count = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("sum")]
    Sum = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("avg")]
    Avg = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("min")]
    Min = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("max")]
    Max = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("p50")]
    P50 = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("p90")]
    P90 = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("p95")]
    P95 = 7,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("p99")]
    P99 = 8,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("count_distinct")]
    CountDistinct = 9,
}

/// <summary>Filter operators</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<FilterOperator>))]
public enum FilterOperator
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("eq")]
    Eq = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("neq")]
    Neq = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("contains")]
    Contains = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("starts_with")]
    StartsWith = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ends_with")]
    EndsWith = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("regex")]
    Regex = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("gt")]
    Gt = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("gte")]
    Gte = 7,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("lt")]
    Lt = 8,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("lte")]
    Lte = 9,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("in")]
    In = 10,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("not_in")]
    NotIn = 11,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("exists")]
    Exists = 12,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("not_exists")]
    NotExists = 13,
}

/// <summary>Log ordering options</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LogOrderBy>))]
public enum LogOrderBy
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("timestamp_asc")]
    TimestampAsc = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("timestamp_desc")]
    TimestampDesc = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("severity_asc")]
    SeverityAsc = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("severity_desc")]
    SeverityDesc = 3,
}

/// <summary>Log pattern trend</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LogPatternTrend>))]
public enum LogPatternTrend
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("increasing")]
    Increasing = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("decreasing")]
    Decreasing = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("stable")]
    Stable = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("new")]
    New = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("spike")]
    Spike = 4,
}

/// <summary>Time bucket sizes</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TimeBucket>))]
public enum TimeBucket
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1s")]
    _1s = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("10s")]
    _10s = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("30s")]
    _30s = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1m")]
    _1m = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("5m")]
    _5m = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("15m")]
    _15m = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("30m")]
    _30m = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1h")]
    _1h = 7,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("6h")]
    _6h = 8,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("12h")]
    _12h = 9,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1d")]
    _1d = 10,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1w")]
    _1w = 11,
}

