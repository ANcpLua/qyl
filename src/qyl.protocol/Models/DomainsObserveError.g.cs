// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9054270+00:00
//     Models for Qyl.Domains.Observe.Error
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Observe.Error;

/// <summary>Correlated error</summary>
public sealed record CorrelatedError
{
    /// <summary>Error ID</summary>
    [JsonPropertyName("error_id")]
    public required string ErrorId { get; init; }

    /// <summary>Error type</summary>
    [JsonPropertyName("error_type")]
    public required string ErrorType { get; init; }

    /// <summary>Correlation strength</summary>
    [JsonPropertyName("correlation_strength")]
    public required global::Qyl.Common.Ratio CorrelationStrength { get; init; }

    /// <summary>Temporal relationship</summary>
    [JsonPropertyName("temporal_relationship")]
    public required global::Qyl.Domains.Observe.Error.TemporalRelationship TemporalRelationship { get; init; }

}

/// <summary>Error stats by category</summary>
public sealed record ErrorCategoryStats
{
    /// <summary>Category</summary>
    [JsonPropertyName("category")]
    public required global::Qyl.Domains.Observe.Error.ErrorCategory Category { get; init; }

    /// <summary>Count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required global::Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>Error correlation result</summary>
public sealed record ErrorCorrelation
{
    /// <summary>Error ID</summary>
    [JsonPropertyName("error_id")]
    public required string ErrorId { get; init; }

    /// <summary>Correlated errors</summary>
    [JsonPropertyName("correlated_errors")]
    public required IReadOnlyList<global::Qyl.Domains.Observe.Error.CorrelatedError> CorrelatedErrors { get; init; }

    /// <summary>Potential root cause</summary>
    [JsonPropertyName("root_cause")]
    public string? RootCause { get; init; }

    /// <summary>Common attributes</summary>
    [JsonPropertyName("common_attributes")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? CommonAttributes { get; init; }

}

/// <summary>Error entity for tracking and analysis</summary>
public sealed record ErrorEntity
{
    /// <summary>Error ID</summary>
    [JsonPropertyName("error_id")]
    public required string ErrorId { get; init; }

    /// <summary>Error type (class name or code)</summary>
    [JsonPropertyName("error.type")]
    public required string ErrorType { get; init; }

    /// <summary>Error message</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Error category</summary>
    [JsonPropertyName("category")]
    public required global::Qyl.Domains.Observe.Error.ErrorCategory Category { get; init; }

    /// <summary>Fingerprint for grouping</summary>
    [JsonPropertyName("fingerprint")]
    public required string Fingerprint { get; init; }

    /// <summary>First occurrence</summary>
    [JsonPropertyName("first_seen")]
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>Last occurrence</summary>
    [JsonPropertyName("last_seen")]
    public required DateTimeOffset LastSeen { get; init; }

    /// <summary>Occurrence count</summary>
    [JsonPropertyName("occurrence_count")]
    public required global::Qyl.Common.Count OccurrenceCount { get; init; }

    /// <summary>Affected users count</summary>
    [JsonPropertyName("affected_users")]
    public global::Qyl.Common.Count? AffectedUsers { get; init; }

    /// <summary>Affected services</summary>
    [JsonPropertyName("affected_services")]
    public IReadOnlyList<string>? AffectedServices { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.Observe.Error.ErrorStatus Status { get; init; }

    /// <summary>Assigned to</summary>
    [JsonPropertyName("assigned_to")]
    public string? AssignedTo { get; init; }

    /// <summary>Issue tracker URL</summary>
    [JsonPropertyName("issue_url")]
    public global::Qyl.Common.UrlString? IssueUrl { get; init; }

    /// <summary>Sample trace IDs</summary>
    [JsonPropertyName("sample_traces")]
    public IReadOnlyList<global::Qyl.Common.TraceId>? SampleTraces { get; init; }

}

/// <summary>Error stats by service</summary>
public sealed record ErrorServiceStats
{
    /// <summary>Service name</summary>
    [JsonPropertyName("service_name")]
    public required string ServiceName { get; init; }

    /// <summary>Error count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Error rate</summary>
    [JsonPropertyName("error_rate")]
    public required global::Qyl.Common.Ratio ErrorRate { get; init; }

    /// <summary>Top error type</summary>
    [JsonPropertyName("top_error_type")]
    public required string TopErrorType { get; init; }

}

/// <summary>Error statistics</summary>
public sealed record ErrorStats
{
    /// <summary>Total error count</summary>
    [JsonPropertyName("total_count")]
    public required global::Qyl.Common.Count TotalCount { get; init; }

    /// <summary>Unique error types</summary>
    [JsonPropertyName("unique_types")]
    public required int UniqueTypes { get; init; }

    /// <summary>Error rate</summary>
    [JsonPropertyName("error_rate")]
    public required global::Qyl.Common.Ratio ErrorRate { get; init; }

    /// <summary>Errors by category</summary>
    [JsonPropertyName("by_category")]
    public required IReadOnlyList<global::Qyl.Domains.Observe.Error.ErrorCategoryStats> ByCategory { get; init; }

    /// <summary>Errors by service</summary>
    [JsonPropertyName("by_service")]
    public IReadOnlyList<global::Qyl.Domains.Observe.Error.ErrorServiceStats>? ByService { get; init; }

    /// <summary>Top errors</summary>
    [JsonPropertyName("top_errors")]
    public required IReadOnlyList<global::Qyl.Domains.Observe.Error.ErrorTypeStats> TopErrors { get; init; }

    /// <summary>Trend</summary>
    [JsonPropertyName("trend")]
    public required global::Qyl.Domains.Observe.Error.ErrorTrend Trend { get; init; }

}

/// <summary>Error stats by type</summary>
public sealed record ErrorTypeStats
{
    /// <summary>Error type</summary>
    [JsonPropertyName("error_type")]
    public required string ErrorType { get; init; }

    /// <summary>Count</summary>
    [JsonPropertyName("count")]
    public required global::Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required global::Qyl.Common.Percentage Percentage { get; init; }

    /// <summary>Affected users</summary>
    [JsonPropertyName("affected_users")]
    public global::Qyl.Common.Count? AffectedUsers { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.Observe.Error.ErrorStatus Status { get; init; }

}

