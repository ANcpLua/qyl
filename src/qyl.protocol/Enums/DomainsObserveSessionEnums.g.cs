// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9037310+00:00
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
    [System.Runtime.Serialization.EnumMember(Value = "desktop")]
    Desktop = 0,
    [System.Runtime.Serialization.EnumMember(Value = "mobile")]
    Mobile = 1,
    [System.Runtime.Serialization.EnumMember(Value = "tablet")]
    Tablet = 2,
    [System.Runtime.Serialization.EnumMember(Value = "tv")]
    Tv = 3,
    [System.Runtime.Serialization.EnumMember(Value = "console")]
    Console = 4,
    [System.Runtime.Serialization.EnumMember(Value = "wearable")]
    Wearable = 5,
    [System.Runtime.Serialization.EnumMember(Value = "iot")]
    Iot = 6,
    [System.Runtime.Serialization.EnumMember(Value = "bot")]
    Bot = 7,
    [System.Runtime.Serialization.EnumMember(Value = "unknown")]
    Unknown = 8,
}

/// <summary>Session states</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SessionState>))]
public enum SessionState
{
    [System.Runtime.Serialization.EnumMember(Value = "active")]
    Active = 0,
    [System.Runtime.Serialization.EnumMember(Value = "idle")]
    Idle = 1,
    [System.Runtime.Serialization.EnumMember(Value = "ended")]
    Ended = 2,
    [System.Runtime.Serialization.EnumMember(Value = "timed_out")]
    TimedOut = 3,
    [System.Runtime.Serialization.EnumMember(Value = "invalidated")]
    Invalidated = 4,
}

