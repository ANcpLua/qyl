// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9036140+00:00
//     Enumeration types for Qyl.Common.Pagination
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Common.Pagination;

/// <summary>Time bucket size for aggregations</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TimeBucket>))]
public enum TimeBucket
{
    [System.Runtime.Serialization.EnumMember(Value = "1m")]
    _1m = 0,
    [System.Runtime.Serialization.EnumMember(Value = "5m")]
    _5m = 1,
    [System.Runtime.Serialization.EnumMember(Value = "15m")]
    _15m = 2,
    [System.Runtime.Serialization.EnumMember(Value = "1h")]
    _1h = 3,
    [System.Runtime.Serialization.EnumMember(Value = "1d")]
    _1d = 4,
    [System.Runtime.Serialization.EnumMember(Value = "1w")]
    _1w = 5,
    [System.Runtime.Serialization.EnumMember(Value = "auto")]
    Auto = 6,
}

