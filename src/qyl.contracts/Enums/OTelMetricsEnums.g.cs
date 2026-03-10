// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2394510+00:00
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
    [System.Text.Json.Serialization.JsonStringEnumMemberName("sum")]
    Sum = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("avg")]
    Avg = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("min")]
    Min = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("max")]
    Max = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("count")]
    Count = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("last")]
    Last = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("rate")]
    Rate = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("increase")]
    Increase = 7,
}

