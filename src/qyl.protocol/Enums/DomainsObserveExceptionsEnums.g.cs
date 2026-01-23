// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9036750+00:00
//     Enumeration types for Qyl.Domains.Observe.Exceptions
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Domains.Observe.Exceptions;

/// <summary>Exception status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ExceptionStatus>))]
public enum ExceptionStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "new")]
    New = 0,
    [System.Runtime.Serialization.EnumMember(Value = "investigating")]
    Investigating = 1,
    [System.Runtime.Serialization.EnumMember(Value = "in_progress")]
    InProgress = 2,
    [System.Runtime.Serialization.EnumMember(Value = "resolved")]
    Resolved = 3,
    [System.Runtime.Serialization.EnumMember(Value = "ignored")]
    Ignored = 4,
    [System.Runtime.Serialization.EnumMember(Value = "regressed")]
    Regressed = 5,
}

/// <summary>Exception trend</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ExceptionTrend>))]
public enum ExceptionTrend
{
    [System.Runtime.Serialization.EnumMember(Value = "up")]
    Up = 0,
    [System.Runtime.Serialization.EnumMember(Value = "down")]
    Down = 1,
    [System.Runtime.Serialization.EnumMember(Value = "stable")]
    Stable = 2,
}

