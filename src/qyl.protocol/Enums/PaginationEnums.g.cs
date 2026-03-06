// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2388020+00:00
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
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1m")]
    _1m = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("5m")]
    _5m = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("15m")]
    _15m = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1h")]
    _1h = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1d")]
    _1d = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("1w")]
    _1w = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("auto")]
    Auto = 6,
}

