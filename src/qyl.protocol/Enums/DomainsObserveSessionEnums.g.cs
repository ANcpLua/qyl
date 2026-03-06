// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2392020+00:00
//     Enumeration types for Qyl.Domains.Observe.Session
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Domains.Observe.Session;

/// <summary>Device types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DeviceType>))]
public enum DeviceType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("desktop")]
    Desktop = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("mobile")]
    Mobile = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("tablet")]
    Tablet = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("tv")]
    Tv = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("console")]
    Console = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("wearable")]
    Wearable = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("iot")]
    Iot = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("bot")]
    Bot = 7,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("unknown")]
    Unknown = 8,
}

/// <summary>Session states</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SessionState>))]
public enum SessionState
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("active")]
    Active = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("idle")]
    Idle = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ended")]
    Ended = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("timed_out")]
    TimedOut = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("invalidated")]
    Invalidated = 4,
}

