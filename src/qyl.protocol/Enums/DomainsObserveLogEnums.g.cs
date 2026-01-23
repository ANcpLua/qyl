// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9036920+00:00
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
    [System.Runtime.Serialization.EnumMember(Value = "count")]
    Count = 0,
    [System.Runtime.Serialization.EnumMember(Value = "sum")]
    Sum = 1,
    [System.Runtime.Serialization.EnumMember(Value = "avg")]
    Avg = 2,
    [System.Runtime.Serialization.EnumMember(Value = "min")]
    Min = 3,
    [System.Runtime.Serialization.EnumMember(Value = "max")]
    Max = 4,
    [System.Runtime.Serialization.EnumMember(Value = "p50")]
    P50 = 5,
    [System.Runtime.Serialization.EnumMember(Value = "p90")]
    P90 = 6,
    [System.Runtime.Serialization.EnumMember(Value = "p95")]
    P95 = 7,
    [System.Runtime.Serialization.EnumMember(Value = "p99")]
    P99 = 8,
    [System.Runtime.Serialization.EnumMember(Value = "count_distinct")]
    CountDistinct = 9,
}

/// <summary>Filter operators</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<FilterOperator>))]
public enum FilterOperator
{
    [System.Runtime.Serialization.EnumMember(Value = "eq")]
    Eq = 0,
    [System.Runtime.Serialization.EnumMember(Value = "neq")]
    Neq = 1,
    [System.Runtime.Serialization.EnumMember(Value = "contains")]
    Contains = 2,
    [System.Runtime.Serialization.EnumMember(Value = "starts_with")]
    StartsWith = 3,
    [System.Runtime.Serialization.EnumMember(Value = "ends_with")]
    EndsWith = 4,
    [System.Runtime.Serialization.EnumMember(Value = "regex")]
    Regex = 5,
    [System.Runtime.Serialization.EnumMember(Value = "gt")]
    Gt = 6,
    [System.Runtime.Serialization.EnumMember(Value = "gte")]
    Gte = 7,
    [System.Runtime.Serialization.EnumMember(Value = "lt")]
    Lt = 8,
    [System.Runtime.Serialization.EnumMember(Value = "lte")]
    Lte = 9,
    [System.Runtime.Serialization.EnumMember(Value = "in")]
    In = 10,
    [System.Runtime.Serialization.EnumMember(Value = "not_in")]
    NotIn = 11,
    [System.Runtime.Serialization.EnumMember(Value = "exists")]
    Exists = 12,
    [System.Runtime.Serialization.EnumMember(Value = "not_exists")]
    NotExists = 13,
}

/// <summary>Log ordering options</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LogOrderBy>))]
public enum LogOrderBy
{
    [System.Runtime.Serialization.EnumMember(Value = "timestamp_asc")]
    TimestampAsc = 0,
    [System.Runtime.Serialization.EnumMember(Value = "timestamp_desc")]
    TimestampDesc = 1,
    [System.Runtime.Serialization.EnumMember(Value = "severity_asc")]
    SeverityAsc = 2,
    [System.Runtime.Serialization.EnumMember(Value = "severity_desc")]
    SeverityDesc = 3,
}

/// <summary>Log pattern trend</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LogPatternTrend>))]
public enum LogPatternTrend
{
    [System.Runtime.Serialization.EnumMember(Value = "increasing")]
    Increasing = 0,
    [System.Runtime.Serialization.EnumMember(Value = "decreasing")]
    Decreasing = 1,
    [System.Runtime.Serialization.EnumMember(Value = "stable")]
    Stable = 2,
    [System.Runtime.Serialization.EnumMember(Value = "new")]
    New = 3,
    [System.Runtime.Serialization.EnumMember(Value = "spike")]
    Spike = 4,
}

/// <summary>Time bucket sizes</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TimeBucket>))]
public enum TimeBucket
{
    [System.Runtime.Serialization.EnumMember(Value = "1s")]
    _1s = 0,
    [System.Runtime.Serialization.EnumMember(Value = "10s")]
    _10s = 1,
    [System.Runtime.Serialization.EnumMember(Value = "30s")]
    _30s = 2,
    [System.Runtime.Serialization.EnumMember(Value = "1m")]
    _1m = 3,
    [System.Runtime.Serialization.EnumMember(Value = "5m")]
    _5m = 4,
    [System.Runtime.Serialization.EnumMember(Value = "15m")]
    _15m = 5,
    [System.Runtime.Serialization.EnumMember(Value = "30m")]
    _30m = 6,
    [System.Runtime.Serialization.EnumMember(Value = "1h")]
    _1h = 7,
    [System.Runtime.Serialization.EnumMember(Value = "6h")]
    _6h = 8,
    [System.Runtime.Serialization.EnumMember(Value = "12h")]
    _12h = 9,
    [System.Runtime.Serialization.EnumMember(Value = "1d")]
    _1d = 10,
    [System.Runtime.Serialization.EnumMember(Value = "1w")]
    _1w = 11,
}

