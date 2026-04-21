// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-04-21T00:08:47.9407670+00:00
//     Models for Qyl.Models
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Models;

/// <summary>Deployment creation request</summary>
public sealed record DeploymentCreate
{
    /// <summary>Service name</summary>
    [JsonPropertyName("service_name")]
    public required string ServiceName { get; init; }

    /// <summary>Service version</summary>
    [JsonPropertyName("service_version")]
    public required global::Qyl.Common.SemVer ServiceVersion { get; init; }

    /// <summary>Environment</summary>
    [JsonPropertyName("environment")]
    public required global::Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }

    /// <summary>Strategy</summary>
    [JsonPropertyName("strategy")]
    public required global::Qyl.Domains.Ops.Deployment.DeploymentStrategy Strategy { get; init; }

    /// <summary>Deployed by</summary>
    [JsonPropertyName("deployed_by")]
    public string? DeployedBy { get; init; }

    /// <summary>Git commit SHA</summary>
    [JsonPropertyName("git_commit")]
    public string? GitCommit { get; init; }

    /// <summary>Git branch</summary>
    [JsonPropertyName("git_branch")]
    public string? GitBranch { get; init; }

}

/// <summary>Deployment update request</summary>
public sealed record DeploymentUpdate
{
    /// <summary>New status</summary>
    [JsonPropertyName("status")]
    public global::Qyl.Domains.Ops.Deployment.DeploymentStatus? Status { get; init; }

    /// <summary>Healthy replicas</summary>
    [JsonPropertyName("healthy_replicas")]
    public int? HealthyReplicas { get; init; }

    /// <summary>Error message</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

}

/// <summary>DORA metrics response</summary>
public sealed record DoraMetrics
{
    /// <summary>Deployment frequency (per day)</summary>
    [JsonPropertyName("deployment_frequency")]
    public required double DeploymentFrequency { get; init; }

    /// <summary>Lead time for changes (hours)</summary>
    [JsonPropertyName("lead_time_hours")]
    public required double LeadTimeHours { get; init; }

    /// <summary>Change failure rate</summary>
    [JsonPropertyName("change_failure_rate")]
    public required double ChangeFailureRate { get; init; }

    /// <summary>Mean time to recovery (hours)</summary>
    [JsonPropertyName("mttr_hours")]
    public required double MttrHours { get; init; }

    /// <summary>Performance level</summary>
    [JsonPropertyName("performance_level")]
    public required global::Qyl.Models.DoraPerformanceLevel PerformanceLevel { get; init; }

}

/// <summary>Error update request</summary>
public sealed record ErrorUpdate
{
    /// <summary>New status</summary>
    [JsonPropertyName("status")]
    public global::Qyl.Domains.Observe.Error.ErrorStatus? Status { get; init; }

    /// <summary>Assignee</summary>
    [JsonPropertyName("assigned_to")]
    public string? AssignedTo { get; init; }

    /// <summary>Issue URL</summary>
    [JsonPropertyName("issue_url")]
    public global::Qyl.Common.UrlString? IssueUrl { get; init; }

}

/// <summary>Generation job creation request</summary>
public sealed record GenerationJobCreateRequest
{
    /// <summary>Workspace ID</summary>
    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    /// <summary>Profile ID</summary>
    [JsonPropertyName("profile_id")]
    public required string ProfileId { get; init; }

    /// <summary>Job type</summary>
    [JsonPropertyName("job_type")]
    public required global::Qyl.Domains.GenerationJobType JobType { get; init; }

}

/// <summary>Generation profile creation request</summary>
public sealed record GenerationProfileCreateRequest
{
    /// <summary>Profile name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Target framework</summary>
    [JsonPropertyName("target_framework")]
    public required string TargetFramework { get; init; }

    /// <summary>Profile description</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Feature flags</summary>
    [JsonPropertyName("features_json")]
    public string? FeaturesJson { get; init; }

}

/// <summary>Save generation selections request</summary>
public sealed record GenerationSelectionSaveRequest
{
    /// <summary>Workspace ID</summary>
    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    /// <summary>Profile ID</summary>
    [JsonPropertyName("profile_id")]
    public required string ProfileId { get; init; }

    /// <summary>Selected semconv keys</summary>
    [JsonPropertyName("selected_keys_json")]
    public required string SelectedKeysJson { get; init; }

}

/// <summary>Handshake start request</summary>
public sealed record HandshakeStartRequest
{
    /// <summary>PKCE code challenge</summary>
    [JsonPropertyName("code_challenge")]
    public required string CodeChallenge { get; init; }

    /// <summary>Client identifier</summary>
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

}

/// <summary>Handshake verification request</summary>
public sealed record HandshakeVerifyRequest
{
    /// <summary>PKCE code verifier</summary>
    [JsonPropertyName("code_verifier")]
    public required string CodeVerifier { get; init; }

    /// <summary>Authorization code</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

}

/// <summary>Handshake verification response</summary>
public sealed record HandshakeVerifyResponse
{
    /// <summary>Access token</summary>
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    /// <summary>Token expiration</summary>
    [JsonPropertyName("expires_at")]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Workspace ID</summary>
    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

}

/// <summary>Issue update request</summary>
public sealed record IssueUpdateRequest
{
    /// <summary>New status</summary>
    [JsonPropertyName("status")]
    public global::Qyl.Domains.IssueStatus? Status { get; init; }

    /// <summary>New priority</summary>
    [JsonPropertyName("priority")]
    public global::Qyl.Domains.IssuePriority? Priority { get; init; }

    /// <summary>Assignee</summary>
    [JsonPropertyName("assigned_to")]
    public string? AssignedTo { get; init; }

}

/// <summary>Log aggregation bucket</summary>
public sealed record LogAggregationBucket
{
    /// <summary>Bucket key (group by value)</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>Aggregated value</summary>
    [JsonPropertyName("value")]
    public required double Value { get; init; }

    /// <summary>Document count</summary>
    [JsonPropertyName("count")]
    public required long Count { get; init; }

    /// <summary>Timestamp (for time series)</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

}

/// <summary>Log aggregation request</summary>
public sealed record LogAggregationRequest
{
    /// <summary>Query filters</summary>
    [JsonPropertyName("query")]
    public global::Qyl.Domains.Observe.Log.LogQuery? Query { get; init; }

    /// <summary>Aggregation specification</summary>
    [JsonPropertyName("aggregation")]
    public required global::Qyl.Domains.Observe.Log.LogAggregation Aggregation { get; init; }

}

/// <summary>Log aggregation response</summary>
public sealed record LogAggregationResponse
{
    /// <summary>Aggregation results</summary>
    [JsonPropertyName("results")]
    public required IReadOnlyList<global::Qyl.Models.LogAggregationBucket> Results { get; init; }

    /// <summary>Total matching logs</summary>
    [JsonPropertyName("total_count")]
    public required long TotalCount { get; init; }

}

/// <summary>Metric data point</summary>
public sealed record MetricDataPoint
{
    /// <summary>Timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Value</summary>
    [JsonPropertyName("value")]
    public required double Value { get; init; }

}

/// <summary>Metric metadata</summary>
public sealed record MetricMetadata
{
    /// <summary>Metric name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Metric description</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Metric unit</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    /// <summary>Metric type</summary>
    [JsonPropertyName("type")]
    public required global::Qyl.OTel.Enums.MetricType Type { get; init; }

    /// <summary>Available label keys</summary>
    [JsonPropertyName("label_keys")]
    public required IReadOnlyList<string> LabelKeys { get; init; }

    /// <summary>Services reporting this metric</summary>
    [JsonPropertyName("services")]
    public required IReadOnlyList<string> Services { get; init; }

}

/// <summary>Metric query request</summary>
public sealed record MetricQueryRequest
{
    /// <summary>Metric name</summary>
    [JsonPropertyName("metric_name")]
    public required string MetricName { get; init; }

    /// <summary>Label filters</summary>
    [JsonPropertyName("filters")]
    public object? Filters { get; init; }

    /// <summary>Start time</summary>
    [JsonPropertyName("start_time")]
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>End time</summary>
    [JsonPropertyName("end_time")]
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Step interval</summary>
    [JsonPropertyName("step")]
    public global::Qyl.Common.Pagination.TimeBucket? Step { get; init; }

    /// <summary>Aggregation function</summary>
    [JsonPropertyName("aggregation")]
    public global::Qyl.OTel.Metrics.AggregationFunction? Aggregation { get; init; }

    /// <summary>Group by labels</summary>
    [JsonPropertyName("group_by")]
    public IReadOnlyList<string>? GroupBy { get; init; }

}

/// <summary>Metric query response</summary>
public sealed record MetricQueryResponse
{
    /// <summary>Metric name</summary>
    [JsonPropertyName("metric_name")]
    public required string MetricName { get; init; }

    /// <summary>Time series data</summary>
    [JsonPropertyName("series")]
    public required IReadOnlyList<global::Qyl.Models.MetricTimeSeries> Series { get; init; }

}

/// <summary>Metric time series</summary>
public sealed record MetricTimeSeries
{
    /// <summary>Labels</summary>
    [JsonPropertyName("labels")]
    public required object Labels { get; init; }

    /// <summary>Data points</summary>
    [JsonPropertyName("points")]
    public required IReadOnlyList<global::Qyl.Models.MetricDataPoint> Points { get; init; }

}

/// <summary>Operation information</summary>
public sealed record OperationInfo
{
    /// <summary>Operation name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Span kind</summary>
    [JsonPropertyName("span_kind")]
    public required global::Qyl.OTel.Enums.SpanKind SpanKind { get; init; }

    /// <summary>Request count</summary>
    [JsonPropertyName("request_count")]
    public required long RequestCount { get; init; }

    /// <summary>Error count</summary>
    [JsonPropertyName("error_count")]
    public required long ErrorCount { get; init; }

    /// <summary>Average duration in milliseconds</summary>
    [JsonPropertyName("avg_duration_ms")]
    public required double AvgDurationMs { get; init; }

    /// <summary>P99 duration in milliseconds</summary>
    [JsonPropertyName("p99_duration_ms")]
    public required double P99DurationMs { get; init; }

}

/// <summary>Project creation request</summary>
public sealed record ProjectCreateRequest
{
    /// <summary>Project name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Project slug (URL-safe)</summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    /// <summary>Project description</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

}

/// <summary>Service details</summary>
public sealed record ServiceDetails
{
    /// <summary>Service name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Service namespace</summary>
    [JsonPropertyName("namespace_name")]
    public string? NamespaceName { get; init; }

    /// <summary>Service version</summary>
    [JsonPropertyName("version")]
    public global::Qyl.Common.SemVer? Version { get; init; }

    /// <summary>Instance count</summary>
    [JsonPropertyName("instance_count")]
    public required int InstanceCount { get; init; }

    /// <summary>Last seen</summary>
    [JsonPropertyName("last_seen")]
    public required DateTimeOffset LastSeen { get; init; }

    /// <summary>Resource attributes</summary>
    [JsonPropertyName("resource_attributes")]
    public required IReadOnlyList<global::Qyl.Common.Attribute> ResourceAttributes { get; init; }

    /// <summary>Instrumentation libraries</summary>
    [JsonPropertyName("instrumentation_libraries")]
    public required IReadOnlyList<global::Qyl.Common.InstrumentationScope> InstrumentationLibraries { get; init; }

    /// <summary>Request rate (per second)</summary>
    [JsonPropertyName("request_rate")]
    public required double RequestRate { get; init; }

    /// <summary>Error rate</summary>
    [JsonPropertyName("error_rate")]
    public required double ErrorRate { get; init; }

    /// <summary>Average latency in milliseconds</summary>
    [JsonPropertyName("avg_latency_ms")]
    public required double AvgLatencyMs { get; init; }

    /// <summary>P99 latency in milliseconds</summary>
    [JsonPropertyName("p99_latency_ms")]
    public required double P99LatencyMs { get; init; }

}

/// <summary>Service information</summary>
public sealed record ServiceInfo
{
    /// <summary>Service name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Service namespace</summary>
    [JsonPropertyName("namespace_name")]
    public string? NamespaceName { get; init; }

    /// <summary>Service version</summary>
    [JsonPropertyName("version")]
    public global::Qyl.Common.SemVer? Version { get; init; }

    /// <summary>Instance count</summary>
    [JsonPropertyName("instance_count")]
    public required int InstanceCount { get; init; }

    /// <summary>Last seen</summary>
    [JsonPropertyName("last_seen")]
    public required DateTimeOffset LastSeen { get; init; }

}

/// <summary>Trace search query</summary>
public sealed record TraceQuery
{
    /// <summary>Free text search</summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>Service name filter</summary>
    [JsonPropertyName("service_name")]
    public string? ServiceName { get; init; }

    /// <summary>Operation name filter</summary>
    [JsonPropertyName("operation_name")]
    public string? OperationName { get; init; }

    /// <summary>Minimum duration in milliseconds</summary>
    [JsonPropertyName("min_duration_ms")]
    public long? MinDurationMs { get; init; }

    /// <summary>Maximum duration in milliseconds</summary>
    [JsonPropertyName("max_duration_ms")]
    public long? MaxDurationMs { get; init; }

    /// <summary>Status filter</summary>
    [JsonPropertyName("status")]
    public global::Qyl.OTel.Enums.SpanStatusCode? Status { get; init; }

    /// <summary>Time range start</summary>
    [JsonPropertyName("start_time")]
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Time range end</summary>
    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>Tag filters</summary>
    [JsonPropertyName("tags")]
    public object? Tags { get; init; }

    /// <summary>Page size</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>Cursor</summary>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }

}

