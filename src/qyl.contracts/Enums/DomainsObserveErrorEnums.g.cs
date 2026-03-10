// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2390700+00:00
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
    [System.Text.Json.Serialization.JsonStringEnumMemberName("client")]
    Client = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("server")]
    Server = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("network")]
    Network = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("timeout")]
    Timeout = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("validation")]
    Validation = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("authentication")]
    Authentication = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("authorization")]
    Authorization = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("rate_limit")]
    RateLimit = 7,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("not_found")]
    NotFound = 8,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("conflict")]
    Conflict = 9,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("internal")]
    Internal = 10,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("external")]
    External = 11,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("database")]
    Database = 12,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("configuration")]
    Configuration = 13,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("unknown")]
    Unknown = 14,
}

/// <summary>Error tracking status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ErrorStatus>))]
public enum ErrorStatus
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("new")]
    New = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("acknowledged")]
    Acknowledged = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("in_progress")]
    InProgress = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("resolved")]
    Resolved = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ignored")]
    Ignored = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("regressed")]
    Regressed = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("wont_fix")]
    WontFix = 6,
}

/// <summary>Error trend</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ErrorTrend>))]
public enum ErrorTrend
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("increasing")]
    Increasing = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("decreasing")]
    Decreasing = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("stable")]
    Stable = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("spike")]
    Spike = 3,
}

/// <summary>Temporal relationship between errors</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TemporalRelationship>))]
public enum TemporalRelationship
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("concurrent")]
    Concurrent = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("precedes")]
    Precedes = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("follows")]
    Follows = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("unrelated")]
    Unrelated = 3,
}

