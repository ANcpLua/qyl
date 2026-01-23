// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9038660+00:00
//     Enumeration types for Qyl.OTel.Metrics
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.OTel.Metrics;

/// <summary>Aggregation functions for metrics</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AggregationFunction>))]
public enum AggregationFunction
{
    [System.Runtime.Serialization.EnumMember(Value = "sum")]
    Sum = 0,
    [System.Runtime.Serialization.EnumMember(Value = "avg")]
    Avg = 1,
    [System.Runtime.Serialization.EnumMember(Value = "min")]
    Min = 2,
    [System.Runtime.Serialization.EnumMember(Value = "max")]
    Max = 3,
    [System.Runtime.Serialization.EnumMember(Value = "count")]
    Count = 4,
    [System.Runtime.Serialization.EnumMember(Value = "last")]
    Last = 5,
    [System.Runtime.Serialization.EnumMember(Value = "rate")]
    Rate = 6,
    [System.Runtime.Serialization.EnumMember(Value = "increase")]
    Increase = 7,
}

