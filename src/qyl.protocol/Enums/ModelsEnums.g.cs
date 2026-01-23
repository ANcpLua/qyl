// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9030490+00:00
//     Enumeration types for Qyl.Models
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ApiVersions>))]
public enum ApiVersions
{
    [System.Runtime.Serialization.EnumMember(Value = "2024-01-01")]
    _20240101 = 0,
    [System.Runtime.Serialization.EnumMember(Value = "2024-06-01")]
    _20240601 = 1,
    [System.Runtime.Serialization.EnumMember(Value = "2025-01-01")]
    _20250101 = 2,
}

/// <summary>DORA performance levels</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DoraPerformanceLevel>))]
public enum DoraPerformanceLevel
{
    [System.Runtime.Serialization.EnumMember(Value = "elite")]
    Elite = 0,
    [System.Runtime.Serialization.EnumMember(Value = "high")]
    High = 1,
    [System.Runtime.Serialization.EnumMember(Value = "medium")]
    Medium = 2,
    [System.Runtime.Serialization.EnumMember(Value = "low")]
    Low = 3,
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HealthStatus>))]
public enum HealthStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "healthy")]
    Healthy = 0,
    [System.Runtime.Serialization.EnumMember(Value = "degraded")]
    Degraded = 1,
    [System.Runtime.Serialization.EnumMember(Value = "unhealthy")]
    Unhealthy = 2,
}

