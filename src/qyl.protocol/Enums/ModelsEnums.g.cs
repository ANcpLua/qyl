// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2372950+00:00
//     Enumeration types for Qyl.Models
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ApiVersions>))]
public enum ApiVersions
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("2025-12-01")]
    _20251201 = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("2026-01-15")]
    _20260115 = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("2026-01-26")]
    _20260126 = 2,
}

/// <summary>DORA performance levels</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DoraPerformanceLevel>))]
public enum DoraPerformanceLevel
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("elite")]
    Elite = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("high")]
    High = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("medium")]
    Medium = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("low")]
    Low = 3,
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HealthStatus>))]
public enum HealthStatus
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("healthy")]
    Healthy = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("degraded")]
    Degraded = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("unhealthy")]
    Unhealthy = 2,
}

