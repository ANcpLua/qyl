// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9055830+00:00
//     Models for Qyl.Domains.Observe.Session
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Session;

/// <summary>Session client information</summary>
public sealed record SessionClientInfo
{
    /// <summary>Client IP address</summary>
    [JsonPropertyName("ip")]
    public global::Qyl.Common.IpAddress? Ip { get; init; }

    /// <summary>User agent string</summary>
    [JsonPropertyName("user_agent")]
    public global::Qyl.Common.UserAgent? UserAgent { get; init; }

    /// <summary>Device type</summary>
    [JsonPropertyName("device_type")]
    public global::Qyl.Domains.Observe.Session.DeviceType? DeviceType { get; init; }

    /// <summary>Operating system</summary>
    [JsonPropertyName("os")]
    public string? Os { get; init; }

    /// <summary>Browser name</summary>
    [JsonPropertyName("browser")]
    public string? Browser { get; init; }

    /// <summary>Browser version</summary>
    [JsonPropertyName("browser_version")]
    public string? BrowserVersion { get; init; }

}

/// <summary>Session stats by country</summary>
public sealed record SessionCountryStats
{
    /// <summary>Country code</summary>
    [JsonPropertyName("country_code")]
    public required string CountryCode { get; init; }

    /// <summary>Country name</summary>
    [JsonPropertyName("country_name")]
    public required string CountryName { get; init; }

    /// <summary>Session count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required global::Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>Session stats by device type</summary>
public sealed record SessionDeviceStats
{
    /// <summary>Device type</summary>
    [JsonPropertyName("device_type")]
    public required global::Qyl.Domains.Observe.Session.DeviceType DeviceType { get; init; }

    /// <summary>Session count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required global::Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>Complete session entity with aggregated data</summary>
public sealed record SessionEntity
{
    /// <summary>Session ID</summary>
    [JsonPropertyName("session.id")]
    public required global::Qyl.Common.SessionId SessionId { get; init; }

    /// <summary>User ID (if authenticated)</summary>
    [JsonPropertyName("user.id")]
    public global::Qyl.Common.UserId? UserId { get; init; }

    /// <summary>Session start time</summary>
    [JsonPropertyName("start_time")]
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>Session end time</summary>
    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>Session duration in milliseconds</summary>
    [JsonPropertyName("duration_ms")]
    public global::Qyl.Common.DurationMs? DurationMs { get; init; }

    /// <summary>Total trace count in session</summary>
    [JsonPropertyName("trace_count")]
    public required int TraceCount { get; init; }

    /// <summary>Total span count in session</summary>
    [JsonPropertyName("span_count")]
    public required int SpanCount { get; init; }

    /// <summary>Total error count in session</summary>
    [JsonPropertyName("error_count")]
    public required int ErrorCount { get; init; }

    /// <summary>Session state</summary>
    [JsonPropertyName("state")]
    public required global::Qyl.Domains.Observe.Session.SessionState State { get; init; }

    /// <summary>Client information</summary>
    [JsonPropertyName("client")]
    public global::Qyl.Domains.Observe.Session.SessionClientInfo? Client { get; init; }

    /// <summary>Location information</summary>
    [JsonPropertyName("geo")]
    public global::Qyl.Domains.Observe.Session.SessionGeoInfo? Geo { get; init; }

    /// <summary>GenAI usage summary</summary>
    [JsonPropertyName("genai_usage")]
    public global::Qyl.Domains.Observe.Session.SessionGenAiUsage? GenaiUsage { get; init; }

}

/// <summary>Session GenAI usage summary</summary>
public sealed record SessionGenAiUsage
{
    /// <summary>Total GenAI requests in session</summary>
    [JsonPropertyName("request_count")]
    public required int RequestCount { get; init; }

    /// <summary>Total input tokens consumed</summary>
    [JsonPropertyName("total_input_tokens")]
    public required global::Qyl.Common.TokenCount TotalInputTokens { get; init; }

    /// <summary>Total output tokens generated</summary>
    [JsonPropertyName("total_output_tokens")]
    public required global::Qyl.Common.TokenCount TotalOutputTokens { get; init; }

    /// <summary>Models used in session</summary>
    [JsonPropertyName("models_used")]
    public required IReadOnlyList<string> ModelsUsed { get; init; }

    /// <summary>Providers used in session</summary>
    [JsonPropertyName("providers_used")]
    public required IReadOnlyList<string> ProvidersUsed { get; init; }

    /// <summary>Estimated cost in USD</summary>
    [JsonPropertyName("estimated_cost_usd")]
    public double? EstimatedCostUsd { get; init; }

}

/// <summary>Session geographic information</summary>
public sealed record SessionGeoInfo
{
    /// <summary>Country code (ISO 3166-1 alpha-2)</summary>
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; init; }

    /// <summary>Country name</summary>
    [JsonPropertyName("country_name")]
    public string? CountryName { get; init; }

    /// <summary>Region/state</summary>
    [JsonPropertyName("region")]
    public string? Region { get; init; }

    /// <summary>City</summary>
    [JsonPropertyName("city")]
    public string? City { get; init; }

    /// <summary>Postal code</summary>
    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; init; }

    /// <summary>Timezone</summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }

}

/// <summary>Aggregated session statistics</summary>
public sealed record SessionStats
{
    /// <summary>Active sessions count</summary>
    [JsonPropertyName("active_sessions")]
    public required global::Qyl.Common.Count ActiveSessions { get; init; }

    /// <summary>Total sessions in time range</summary>
    [JsonPropertyName("total_sessions")]
    public required global::Qyl.Common.Count TotalSessions { get; init; }

    /// <summary>Unique users in time range</summary>
    [JsonPropertyName("unique_users")]
    public required global::Qyl.Common.Count UniqueUsers { get; init; }

    /// <summary>Average session duration in milliseconds</summary>
    [JsonPropertyName("avg_duration_ms")]
    public required double AvgDurationMs { get; init; }

    /// <summary>Sessions with errors</summary>
    [JsonPropertyName("sessions_with_errors")]
    public required global::Qyl.Common.Count SessionsWithErrors { get; init; }

    /// <summary>Sessions with GenAI usage</summary>
    [JsonPropertyName("sessions_with_genai")]
    public required global::Qyl.Common.Count SessionsWithGenai { get; init; }

    /// <summary>Bounce rate (single-page sessions)</summary>
    [JsonPropertyName("bounce_rate")]
    public required global::Qyl.Common.Ratio BounceRate { get; init; }

    /// <summary>Sessions by device type</summary>
    [JsonPropertyName("by_device_type")]
    public IReadOnlyList<global::Qyl.Domains.Observe.Session.SessionDeviceStats>? ByDeviceType { get; init; }

    /// <summary>Sessions by country</summary>
    [JsonPropertyName("by_country")]
    public IReadOnlyList<global::Qyl.Domains.Observe.Session.SessionCountryStats>? ByCountry { get; init; }

}

