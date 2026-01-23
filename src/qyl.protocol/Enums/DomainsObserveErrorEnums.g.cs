// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9036350+00:00
//     Enumeration types for Qyl.Domains.Observe.Error
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Domains.Observe.Error;

/// <summary>High-level error categories</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ErrorCategory>))]
public enum ErrorCategory
{
    [System.Runtime.Serialization.EnumMember(Value = "client")]
    Client = 0,
    [System.Runtime.Serialization.EnumMember(Value = "server")]
    Server = 1,
    [System.Runtime.Serialization.EnumMember(Value = "network")]
    Network = 2,
    [System.Runtime.Serialization.EnumMember(Value = "timeout")]
    Timeout = 3,
    [System.Runtime.Serialization.EnumMember(Value = "validation")]
    Validation = 4,
    [System.Runtime.Serialization.EnumMember(Value = "authentication")]
    Authentication = 5,
    [System.Runtime.Serialization.EnumMember(Value = "authorization")]
    Authorization = 6,
    [System.Runtime.Serialization.EnumMember(Value = "rate_limit")]
    RateLimit = 7,
    [System.Runtime.Serialization.EnumMember(Value = "not_found")]
    NotFound = 8,
    [System.Runtime.Serialization.EnumMember(Value = "conflict")]
    Conflict = 9,
    [System.Runtime.Serialization.EnumMember(Value = "internal")]
    Internal = 10,
    [System.Runtime.Serialization.EnumMember(Value = "external")]
    External = 11,
    [System.Runtime.Serialization.EnumMember(Value = "database")]
    Database = 12,
    [System.Runtime.Serialization.EnumMember(Value = "configuration")]
    Configuration = 13,
    [System.Runtime.Serialization.EnumMember(Value = "unknown")]
    Unknown = 14,
}

/// <summary>Error tracking status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ErrorStatus>))]
public enum ErrorStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "new")]
    New = 0,
    [System.Runtime.Serialization.EnumMember(Value = "acknowledged")]
    Acknowledged = 1,
    [System.Runtime.Serialization.EnumMember(Value = "in_progress")]
    InProgress = 2,
    [System.Runtime.Serialization.EnumMember(Value = "resolved")]
    Resolved = 3,
    [System.Runtime.Serialization.EnumMember(Value = "ignored")]
    Ignored = 4,
    [System.Runtime.Serialization.EnumMember(Value = "regressed")]
    Regressed = 5,
    [System.Runtime.Serialization.EnumMember(Value = "wont_fix")]
    WontFix = 6,
}

/// <summary>Error trend</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ErrorTrend>))]
public enum ErrorTrend
{
    [System.Runtime.Serialization.EnumMember(Value = "increasing")]
    Increasing = 0,
    [System.Runtime.Serialization.EnumMember(Value = "decreasing")]
    Decreasing = 1,
    [System.Runtime.Serialization.EnumMember(Value = "stable")]
    Stable = 2,
    [System.Runtime.Serialization.EnumMember(Value = "spike")]
    Spike = 3,
}

/// <summary>Temporal relationship between errors</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TemporalRelationship>))]
public enum TemporalRelationship
{
    [System.Runtime.Serialization.EnumMember(Value = "concurrent")]
    Concurrent = 0,
    [System.Runtime.Serialization.EnumMember(Value = "precedes")]
    Precedes = 1,
    [System.Runtime.Serialization.EnumMember(Value = "follows")]
    Follows = 2,
    [System.Runtime.Serialization.EnumMember(Value = "unrelated")]
    Unrelated = 3,
}

