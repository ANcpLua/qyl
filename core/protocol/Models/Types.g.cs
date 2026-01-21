// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    /Users/ancplua/qyl/core/openapi/openapi.yaml
//     Generated: 2026-01-16T09:00:34.9370760+00:00
//     Models for Qyl
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl;

/// <summary>Key-value attribute pair following OTel conventions</summary>
public sealed record Attribute
{
    /// <summary>Attribute key (dot-separated namespace)</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>Attribute value</summary>
    [JsonPropertyName("value")]
    public required Qyl.Common.AttributeValue Value { get; init; }

}

/// <summary>Attribute filter</summary>
public sealed record AttributeFilter
{
    /// <summary>Attribute key</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>Filter operator</summary>
    [JsonPropertyName("operator")]
    public required Qyl.Domains.Observe.Log.FilterOperator Operator { get; init; }

    /// <summary>Filter value</summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }

}

/// <summary>Precise source code location for debugging and tracing</summary>
public sealed record CodeLocation
{
    /// <summary>Source file path</summary>
    [JsonPropertyName("filepath")]
    public required string Filepath { get; init; }

    /// <summary>Line number (1-indexed)</summary>
    [JsonPropertyName("line_number")]
    public required int LineNumber { get; init; }

    /// <summary>Column number (1-indexed)</summary>
    [JsonPropertyName("column_number")]
    public int? ColumnNumber { get; init; }

    /// <summary>Function/method name</summary>
    [JsonPropertyName("function_name")]
    public string? FunctionName { get; init; }

    /// <summary>Class/type name</summary>
    [JsonPropertyName("class_name")]
    public string? ClassName { get; init; }

    /// <summary>Namespace/module</summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; init; }

}

/// <summary>Conflict - resource state conflict (409)</summary>
public sealed record ConflictError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>The conflicting resource ID</summary>
    [JsonPropertyName("conflicting_resource")]
    public string? ConflictingResource { get; init; }

}

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
    public required Qyl.Common.Ratio CorrelationStrength { get; init; }

    /// <summary>Temporal relationship</summary>
    [JsonPropertyName("temporal_relationship")]
    public required Qyl.Domains.Observe.Error.TemporalRelationship TemporalRelationship { get; init; }

}

/// <summary>Base data point with common fields</summary>
public sealed record DataPointBase
{
    /// <summary>Start timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("start_time_unix_nano")]
    public required long StartTimeUnixNano { get; init; }

    /// <summary>End timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("time_unix_nano")]
    public required long TimeUnixNano { get; init; }

    /// <summary>Data point attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Data point flags</summary>
    [JsonPropertyName("flags")]
    public Qyl.OTel.Enums.DataPointFlags? Flags { get; init; }

}

/// <summary>Deployment creation request</summary>
public sealed record DeploymentCreate
{
    /// <summary>Service name</summary>
    [JsonPropertyName("service_name")]
    public required string ServiceName { get; init; }

    /// <summary>Service version</summary>
    [JsonPropertyName("service_version")]
    public required Qyl.Common.SemVer ServiceVersion { get; init; }

    /// <summary>Environment</summary>
    [JsonPropertyName("environment")]
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }

    /// <summary>Strategy</summary>
    [JsonPropertyName("strategy")]
    public required Qyl.Domains.Ops.Deployment.DeploymentStrategy Strategy { get; init; }

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

/// <summary>Complete deployment record</summary>
public sealed record DeploymentEntity
{
    /// <summary>Deployment ID</summary>
    [JsonPropertyName("deployment.id")]
    public required string DeploymentId { get; init; }

    /// <summary>Service name</summary>
    [JsonPropertyName("service.name")]
    public required string ServiceName { get; init; }

    /// <summary>Service version</summary>
    [JsonPropertyName("service.version")]
    public required Qyl.Common.SemVer ServiceVersion { get; init; }

    /// <summary>Environment</summary>
    [JsonPropertyName("environment")]
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required Qyl.Domains.Ops.Deployment.DeploymentStatus Status { get; init; }

    /// <summary>Strategy</summary>
    [JsonPropertyName("strategy")]
    public required Qyl.Domains.Ops.Deployment.DeploymentStrategy Strategy { get; init; }

    /// <summary>Start time</summary>
    [JsonPropertyName("start_time")]
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>End time</summary>
    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>Duration in seconds</summary>
    [JsonPropertyName("duration_s")]
    public Qyl.Common.DurationS? DurationS { get; init; }

    /// <summary>Deployed by (user/system)</summary>
    [JsonPropertyName("deployed_by")]
    public string? DeployedBy { get; init; }

    /// <summary>Git commit SHA</summary>
    [JsonPropertyName("git_commit")]
    public string? GitCommit { get; init; }

    /// <summary>Git branch</summary>
    [JsonPropertyName("git_branch")]
    public string? GitBranch { get; init; }

    /// <summary>Previous version</summary>
    [JsonPropertyName("previous_version")]
    public Qyl.Common.SemVer? PreviousVersion { get; init; }

    /// <summary>Rollback target (if rolled back)</summary>
    [JsonPropertyName("rollback_target")]
    public string? RollbackTarget { get; init; }

    /// <summary>Replica count</summary>
    [JsonPropertyName("replica_count")]
    public int? ReplicaCount { get; init; }

    /// <summary>Healthy replica count</summary>
    [JsonPropertyName("healthy_replicas")]
    public int? HealthyReplicas { get; init; }

    /// <summary>Error message (if failed)</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

}

/// <summary>Deployment lifecycle event</summary>
public sealed record DeploymentEvent
{
    /// <summary>Event name</summary>
    [JsonPropertyName("event.name")]
    public required Qyl.Domains.Ops.Deployment.DeploymentEventName EventName { get; init; }

    /// <summary>Deployment ID</summary>
    [JsonPropertyName("deployment.id")]
    public required string DeploymentId { get; init; }

    /// <summary>Service name</summary>
    [JsonPropertyName("service.name")]
    public required string ServiceName { get; init; }

    /// <summary>Environment</summary>
    [JsonPropertyName("deployment.environment.name")]
    public required Qyl.Domains.Ops.Deployment.DeploymentEnvironment DeploymentEnvironmentName { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required Qyl.Domains.Ops.Deployment.DeploymentStatus Status { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Deployment stream event</summary>
public sealed record DeploymentStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Deployment event data</summary>
    [JsonPropertyName("data")]
    public required Qyl.Domains.Ops.Deployment.DeploymentEvent Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Deployment update request</summary>
public sealed record DeploymentUpdate
{
    /// <summary>New status</summary>
    [JsonPropertyName("status")]
    public Qyl.Domains.Ops.Deployment.DeploymentStatus? Status { get; init; }

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
    public required Qyl.Common.Ratio ChangeFailureRate { get; init; }

    /// <summary>Mean time to recovery (hours)</summary>
    [JsonPropertyName("mttr_hours")]
    public required double MttrHours { get; init; }

    /// <summary>Performance level</summary>
    [JsonPropertyName("performance_level")]
    public required DoraPerformanceLevel PerformanceLevel { get; init; }

}

/// <summary>Enriched exception with parsed stack trace</summary>
public sealed record EnrichedException
{
    /// <summary>Exception type/class name</summary>
    [JsonPropertyName("exception_type")]
    public required string ExceptionType { get; init; }

    /// <summary>Exception message</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Parsed stack trace</summary>
    [JsonPropertyName("stack_trace")]
    public Qyl.Domains.AI.Code.StackTrace? StackTrace { get; init; }

    /// <summary>Exception cause/inner exception</summary>
    [JsonPropertyName("cause")]
    public Qyl.Domains.Observe.Exceptions.EnrichedException? Cause { get; init; }

    /// <summary>Additional exception data</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<Qyl.Common.Attribute>? Data { get; init; }

    /// <summary>Exception fingerprint (for grouping)</summary>
    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; init; }

    /// <summary>First occurrence timestamp</summary>
    [JsonPropertyName("first_seen")]
    public DateTimeOffset? FirstSeen { get; init; }

    /// <summary>Last occurrence timestamp</summary>
    [JsonPropertyName("last_seen")]
    public DateTimeOffset? LastSeen { get; init; }

    /// <summary>Occurrence count</summary>
    [JsonPropertyName("occurrence_count")]
    public Qyl.Common.Count? OccurrenceCount { get; init; }

    /// <summary>Affected users count</summary>
    [JsonPropertyName("affected_users")]
    public Qyl.Common.Count? AffectedUsers { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public Qyl.Domains.Observe.Exceptions.ExceptionStatus? Status { get; init; }

}

/// <summary>Error stats by category</summary>
public sealed record ErrorCategoryStats
{
    /// <summary>Category</summary>
    [JsonPropertyName("category")]
    public required Qyl.Domains.Observe.Error.ErrorCategory Category { get; init; }

    /// <summary>Count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>Error correlation result</summary>
public sealed record ErrorCorrelation
{
    /// <summary>Error ID</summary>
    [JsonPropertyName("error_id")]
    public required string ErrorId { get; init; }

    /// <summary>Correlated errors</summary>
    [JsonPropertyName("correlated_errors")]
    public required IReadOnlyList<Qyl.Domains.Observe.Error.CorrelatedError> CorrelatedErrors { get; init; }

    /// <summary>Potential root cause</summary>
    [JsonPropertyName("root_cause")]
    public string? RootCause { get; init; }

    /// <summary>Common attributes</summary>
    [JsonPropertyName("common_attributes")]
    public IReadOnlyList<Qyl.Common.Attribute>? CommonAttributes { get; init; }

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
    public required Qyl.Domains.Observe.Error.ErrorCategory Category { get; init; }

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
    public required Qyl.Common.Count OccurrenceCount { get; init; }

    /// <summary>Affected users count</summary>
    [JsonPropertyName("affected_users")]
    public Qyl.Common.Count? AffectedUsers { get; init; }

    /// <summary>Affected services</summary>
    [JsonPropertyName("affected_services")]
    public IReadOnlyList<string>? AffectedServices { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required Qyl.Domains.Observe.Error.ErrorStatus Status { get; init; }

    /// <summary>Assigned to</summary>
    [JsonPropertyName("assigned_to")]
    public string? AssignedTo { get; init; }

    /// <summary>Issue tracker URL</summary>
    [JsonPropertyName("issue_url")]
    public Qyl.Common.UrlString? IssueUrl { get; init; }

    /// <summary>Sample trace IDs</summary>
    [JsonPropertyName("sample_traces")]
    public IReadOnlyList<Qyl.Common.TraceId>? SampleTraces { get; init; }

}

/// <summary>Error stats by service</summary>
public sealed record ErrorServiceStats
{
    /// <summary>Service name</summary>
    [JsonPropertyName("service_name")]
    public required string ServiceName { get; init; }

    /// <summary>Error count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Error rate</summary>
    [JsonPropertyName("error_rate")]
    public required Qyl.Common.Ratio ErrorRate { get; init; }

    /// <summary>Top error type</summary>
    [JsonPropertyName("top_error_type")]
    public required string TopErrorType { get; init; }

}

/// <summary>Error statistics</summary>
public sealed record ErrorStats
{
    /// <summary>Total error count</summary>
    [JsonPropertyName("total_count")]
    public required Qyl.Common.Count TotalCount { get; init; }

    /// <summary>Unique error types</summary>
    [JsonPropertyName("unique_types")]
    public required int UniqueTypes { get; init; }

    /// <summary>Error rate</summary>
    [JsonPropertyName("error_rate")]
    public required Qyl.Common.Ratio ErrorRate { get; init; }

    /// <summary>Errors by category</summary>
    [JsonPropertyName("by_category")]
    public required IReadOnlyList<Qyl.Domains.Observe.Error.ErrorCategoryStats> ByCategory { get; init; }

    /// <summary>Errors by service</summary>
    [JsonPropertyName("by_service")]
    public IReadOnlyList<Qyl.Domains.Observe.Error.ErrorServiceStats>? ByService { get; init; }

    /// <summary>Top errors</summary>
    [JsonPropertyName("top_errors")]
    public required IReadOnlyList<Qyl.Domains.Observe.Error.ErrorTypeStats> TopErrors { get; init; }

    /// <summary>Trend</summary>
    [JsonPropertyName("trend")]
    public required Qyl.Domains.Observe.Error.ErrorTrend Trend { get; init; }

}

/// <summary>Error stats by type</summary>
public sealed record ErrorTypeStats
{
    /// <summary>Error type</summary>
    [JsonPropertyName("error_type")]
    public required string ErrorType { get; init; }

    /// <summary>Count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required Qyl.Common.Percentage Percentage { get; init; }

    /// <summary>Affected users</summary>
    [JsonPropertyName("affected_users")]
    public Qyl.Common.Count? AffectedUsers { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required Qyl.Domains.Observe.Error.ErrorStatus Status { get; init; }

}

/// <summary>Error update request</summary>
public sealed record ErrorUpdate
{
    /// <summary>New status</summary>
    [JsonPropertyName("status")]
    public Qyl.Domains.Observe.Error.ErrorStatus? Status { get; init; }

    /// <summary>Assignee</summary>
    [JsonPropertyName("assigned_to")]
    public string? AssignedTo { get; init; }

    /// <summary>Issue URL</summary>
    [JsonPropertyName("issue_url")]
    public Qyl.Common.UrlString? IssueUrl { get; init; }

}

/// <summary>Exception event following OTel spec</summary>
public sealed record ExceptionEvent
{
    /// <summary>Event name (always 'exception')</summary>
    [JsonPropertyName("event.name")]
    public required string EventName { get; init; }

    /// <summary>Exception type/class name</summary>
    [JsonPropertyName("exception.type")]
    public required string ExceptionType { get; init; }

    /// <summary>Exception message</summary>
    [JsonPropertyName("exception.message")]
    public required string ExceptionMessage { get; init; }

    /// <summary>Exception stacktrace</summary>
    [JsonPropertyName("exception.stacktrace")]
    public string? ExceptionStacktrace { get; init; }

    /// <summary>Whether the exception escaped</summary>
    [JsonPropertyName("exception.escaped")]
    public required bool ExceptionEscaped { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Associated trace ID</summary>
    [JsonPropertyName("trace_id")]
    public Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Associated span ID</summary>
    [JsonPropertyName("span_id")]
    public Qyl.Common.SpanId? SpanId { get; init; }

}

/// <summary>Exception stats by service</summary>
public sealed record ExceptionServiceStats
{
    /// <summary>Service name</summary>
    [JsonPropertyName("service_name")]
    public required string ServiceName { get; init; }

    /// <summary>Exception count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Exception rate (per minute)</summary>
    [JsonPropertyName("rate_per_minute")]
    public required double RatePerMinute { get; init; }

}

/// <summary>Exception statistics</summary>
public sealed record ExceptionStats
{
    /// <summary>Total exception count</summary>
    [JsonPropertyName("total_count")]
    public required Qyl.Common.Count TotalCount { get; init; }

    /// <summary>Unique exception types</summary>
    [JsonPropertyName("unique_types")]
    public required int UniqueTypes { get; init; }

    /// <summary>Exceptions by type</summary>
    [JsonPropertyName("by_type")]
    public required IReadOnlyList<Qyl.Domains.Observe.Exceptions.ExceptionTypeStats> ByType { get; init; }

    /// <summary>Most affected services</summary>
    [JsonPropertyName("by_service")]
    public IReadOnlyList<Qyl.Domains.Observe.Exceptions.ExceptionServiceStats>? ByService { get; init; }

    /// <summary>Exception trend (up/down/stable)</summary>
    [JsonPropertyName("trend")]
    public Qyl.Domains.Observe.Exceptions.ExceptionTrend? Trend { get; init; }

}

/// <summary>Exception stream event</summary>
public sealed record ExceptionStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Exception data</summary>
    [JsonPropertyName("data")]
    public required Qyl.Domains.Observe.Exceptions.ExceptionEvent Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Exception stats by type</summary>
public sealed record ExceptionTypeStats
{
    /// <summary>Exception type</summary>
    [JsonPropertyName("exception_type")]
    public required string ExceptionType { get; init; }

    /// <summary>Count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required Qyl.Common.Percentage Percentage { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required Qyl.Domains.Observe.Exceptions.ExceptionStatus Status { get; init; }

}

/// <summary>Exemplar - sample trace linked to metric</summary>
public sealed record Exemplar
{
    /// <summary>Timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("time_unix_nano")]
    public required long TimeUnixNano { get; init; }

    /// <summary>Value as integer</summary>
    [JsonPropertyName("as_int")]
    public long? AsInt { get; init; }

    /// <summary>Value as double</summary>
    [JsonPropertyName("as_double")]
    public double? AsDouble { get; init; }

    /// <summary>Span ID of the exemplar</summary>
    [JsonPropertyName("span_id")]
    public Qyl.Common.SpanId? SpanId { get; init; }

    /// <summary>Trace ID of the exemplar</summary>
    [JsonPropertyName("trace_id")]
    public Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Filtered attributes</summary>
    [JsonPropertyName("filtered_attributes")]
    public IReadOnlyList<Qyl.Common.Attribute>? FilteredAttributes { get; init; }

}

/// <summary>Exponential histogram buckets</summary>
public sealed record ExponentialBuckets
{
    /// <summary>Offset of the first bucket</summary>
    [JsonPropertyName("offset")]
    public required int Offset { get; init; }

    /// <summary>Bucket counts</summary>
    [JsonPropertyName("bucket_counts")]
    public required IReadOnlyList<int> BucketCounts { get; init; }

}

/// <summary>Exponential histogram metric - distribution with exponential bucket boundaries</summary>
public sealed record ExponentialHistogramData
{
    /// <summary>Discriminator identifying this as exponential histogram metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Exponential histogram data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<Qyl.OTel.Metrics.ExponentialHistogramDataPoint> DataPoints { get; init; }

    /// <summary>Aggregation temporality</summary>
    [JsonPropertyName("aggregation_temporality")]
    public required Qyl.OTel.Enums.AggregationTemporality AggregationTemporality { get; init; }

}

/// <summary>Exponential histogram data point</summary>
public sealed record ExponentialHistogramDataPoint
{
    /// <summary>Number of values</summary>
    [JsonPropertyName("count")]
    public required long Count { get; init; }

    /// <summary>Sum of all values</summary>
    [JsonPropertyName("sum")]
    public double? Sum { get; init; }

    /// <summary>Scale factor for bucket boundaries</summary>
    [JsonPropertyName("scale")]
    public required int Scale { get; init; }

    /// <summary>Zero count</summary>
    [JsonPropertyName("zero_count")]
    public required long ZeroCount { get; init; }

    /// <summary>Zero threshold</summary>
    [JsonPropertyName("zero_threshold")]
    public double? ZeroThreshold { get; init; }

    /// <summary>Positive bucket counts</summary>
    [JsonPropertyName("positive")]
    public required Qyl.OTel.Metrics.ExponentialBuckets Positive { get; init; }

    /// <summary>Negative bucket counts</summary>
    [JsonPropertyName("negative")]
    public required Qyl.OTel.Metrics.ExponentialBuckets Negative { get; init; }

    /// <summary>Minimum value</summary>
    [JsonPropertyName("min")]
    public double? Min { get; init; }

    /// <summary>Maximum value</summary>
    [JsonPropertyName("max")]
    public double? Max { get; init; }

    /// <summary>Exemplars for the data point</summary>
    [JsonPropertyName("exemplars")]
    public IReadOnlyList<Qyl.OTel.Metrics.Exemplar>? Exemplars { get; init; }

}

/// <summary>Forbidden - insufficient permissions (403)</summary>
public sealed record ForbiddenError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Required permission that is missing</summary>
    [JsonPropertyName("required_permission")]
    public string? RequiredPermission { get; init; }

}

/// <summary>Gauge metric - instantaneous value at a point in time</summary>
public sealed record GaugeData
{
    /// <summary>Discriminator identifying this as gauge metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Gauge data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<Qyl.OTel.Metrics.NumberDataPoint> DataPoints { get; init; }

}

/// <summary>Health check response</summary>
public sealed record HealthResponse
{
    /// <summary>Service status</summary>
    [JsonPropertyName("status")]
    public required HealthStatus Status { get; init; }

    /// <summary>Service version</summary>
    [JsonPropertyName("version")]
    public required Qyl.Common.SemVer Version { get; init; }

    /// <summary>Uptime in seconds</summary>
    [JsonPropertyName("uptime_seconds")]
    public required long UptimeSeconds { get; init; }

    /// <summary>Component health</summary>
    [JsonPropertyName("components")]
    public object? Components { get; init; }

}

/// <summary>Heartbeat event for connection keep-alive</summary>
public sealed record HeartbeatEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Server timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Histogram metric - distribution of values in buckets</summary>
public sealed record HistogramData
{
    /// <summary>Discriminator identifying this as histogram metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Histogram data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<Qyl.OTel.Metrics.HistogramDataPoint> DataPoints { get; init; }

    /// <summary>Aggregation temporality</summary>
    [JsonPropertyName("aggregation_temporality")]
    public required Qyl.OTel.Enums.AggregationTemporality AggregationTemporality { get; init; }

}

/// <summary>Histogram data point</summary>
public sealed record HistogramDataPoint
{
    /// <summary>Number of values in the histogram</summary>
    [JsonPropertyName("count")]
    public required long Count { get; init; }

    /// <summary>Sum of all values</summary>
    [JsonPropertyName("sum")]
    public double? Sum { get; init; }

    /// <summary>Bucket counts</summary>
    [JsonPropertyName("bucket_counts")]
    public required IReadOnlyList<int> BucketCounts { get; init; }

    /// <summary>Explicit bucket boundaries</summary>
    [JsonPropertyName("explicit_bounds")]
    public required IReadOnlyList<double> ExplicitBounds { get; init; }

    /// <summary>Minimum value</summary>
    [JsonPropertyName("min")]
    public double? Min { get; init; }

    /// <summary>Maximum value</summary>
    [JsonPropertyName("max")]
    public double? Max { get; init; }

    /// <summary>Exemplars for the data point</summary>
    [JsonPropertyName("exemplars")]
    public IReadOnlyList<Qyl.OTel.Metrics.Exemplar>? Exemplars { get; init; }

}

/// <summary>Instrumentation scope identifying the library/component emitting telemetry</summary>
public sealed record InstrumentationScope
{
    /// <summary>Name of the instrumentation scope (library name)</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Version of the instrumentation scope</summary>
    [JsonPropertyName("version")]
    public Qyl.Common.SemVer? Version { get; init; }

    /// <summary>Additional attributes for the scope</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public Qyl.Common.Count? DroppedAttributesCount { get; init; }

}

/// <summary>Internal server error (500)</summary>
public sealed record InternalServerError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Error code for support reference</summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

}

/// <summary>Log aggregation request</summary>
public sealed record LogAggregation
{
    /// <summary>Group by fields</summary>
    [JsonPropertyName("group_by")]
    public required IReadOnlyList<string> GroupBy { get; init; }

    /// <summary>Aggregation function</summary>
    [JsonPropertyName("function")]
    public required Qyl.Domains.Observe.Log.AggregationFunction Function { get; init; }

    /// <summary>Field to aggregate (for non-count)</summary>
    [JsonPropertyName("field")]
    public string? Field { get; init; }

    /// <summary>Time bucket (for time series)</summary>
    [JsonPropertyName("time_bucket")]
    public Qyl.Domains.Observe.Log.TimeBucket? TimeBucket { get; init; }

    /// <summary>Top N results</summary>
    [JsonPropertyName("top_n")]
    public int? TopN { get; init; }

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
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Timestamp (for time series)</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

}

/// <summary>Log aggregation request</summary>
public sealed record LogAggregationRequest
{
    /// <summary>Query filters</summary>
    [JsonPropertyName("query")]
    public Qyl.Domains.Observe.Log.LogQuery? Query { get; init; }

    /// <summary>Aggregation specification</summary>
    [JsonPropertyName("aggregation")]
    public required Qyl.Domains.Observe.Log.LogAggregation Aggregation { get; init; }

}

/// <summary>Log aggregation response</summary>
public sealed record LogAggregationResponse
{
    /// <summary>Aggregation results</summary>
    [JsonPropertyName("results")]
    public required IReadOnlyList<LogAggregationBucket> Results { get; init; }

    /// <summary>Total matching logs</summary>
    [JsonPropertyName("total_count")]
    public required Qyl.Common.Count TotalCount { get; init; }

}

/// <summary>Log body content - can be string, structured, or bytes</summary>
public sealed record LogBody
{
}

/// <summary>Array log body</summary>
public sealed record LogBodyArray
{
    /// <summary>Array of values</summary>
    [JsonPropertyName("array_value")]
    public required IReadOnlyList<Qyl.Common.AttributeValue> ArrayValue { get; init; }

}

/// <summary>Binary log body</summary>
public sealed record LogBodyBytes
{
    /// <summary>Binary value (base64 encoded)</summary>
    [JsonPropertyName("bytes_value")]
    public required string BytesValue { get; init; }

}

/// <summary>Structured key-value log body</summary>
public sealed record LogBodyKvList
{
    /// <summary>Key-value pairs</summary>
    [JsonPropertyName("kv_list_value")]
    public required IReadOnlyList<Qyl.Common.Attribute> KvListValue { get; init; }

}

/// <summary>String log body</summary>
public sealed record LogBodyString
{
    /// <summary>String value</summary>
    [JsonPropertyName("string_value")]
    public required string StringValue { get; init; }

}

/// <summary>Log count by dimension</summary>
public sealed record LogCountByDimension
{
    /// <summary>Dimension value</summary>
    [JsonPropertyName("dimension")]
    public required string Dimension { get; init; }

    /// <summary>Log count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Error count for this dimension</summary>
    [JsonPropertyName("error_count")]
    public required Qyl.Common.Count ErrorCount { get; init; }

}

/// <summary>Log count by severity level</summary>
public sealed record LogCountBySeverity
{
    /// <summary>Severity level</summary>
    [JsonPropertyName("severity")]
    public required Qyl.OTel.Enums.SeverityText Severity { get; init; }

    /// <summary>Log count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>Detected log pattern</summary>
public sealed record LogPattern
{
    /// <summary>Pattern ID</summary>
    [JsonPropertyName("pattern_id")]
    public required string PatternId { get; init; }

    /// <summary>Pattern template</summary>
    [JsonPropertyName("template")]
    public required string Template { get; init; }

    /// <summary>Sample log message</summary>
    [JsonPropertyName("sample")]
    public required string Sample { get; init; }

    /// <summary>Occurrence count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>First seen</summary>
    [JsonPropertyName("first_seen")]
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>Last seen</summary>
    [JsonPropertyName("last_seen")]
    public required DateTimeOffset LastSeen { get; init; }

    /// <summary>Trend</summary>
    [JsonPropertyName("trend")]
    public required Qyl.Domains.Observe.Log.LogPatternTrend Trend { get; init; }

    /// <summary>Severity distribution</summary>
    [JsonPropertyName("severity_distribution")]
    public IReadOnlyList<Qyl.Domains.Observe.Log.LogSeverityStats>? SeverityDistribution { get; init; }

}

/// <summary>Log search query</summary>
public sealed record LogQuery
{
    /// <summary>Free text search</summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>Severity filter</summary>
    [JsonPropertyName("severity_min")]
    public Qyl.OTel.Enums.SeverityNumber? SeverityMin { get; init; }

    /// <summary>Service name filter</summary>
    [JsonPropertyName("service_name")]
    public string? ServiceName { get; init; }

    /// <summary>Trace ID filter</summary>
    [JsonPropertyName("trace_id")]
    public Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Span ID filter</summary>
    [JsonPropertyName("span_id")]
    public Qyl.Common.SpanId? SpanId { get; init; }

    /// <summary>Time range start</summary>
    [JsonPropertyName("time_start")]
    public DateTimeOffset? TimeStart { get; init; }

    /// <summary>Time range end</summary>
    [JsonPropertyName("time_end")]
    public DateTimeOffset? TimeEnd { get; init; }

    /// <summary>Attribute filters</summary>
    [JsonPropertyName("attribute_filters")]
    public IReadOnlyList<Qyl.Domains.Observe.Log.AttributeFilter>? AttributeFilters { get; init; }

    /// <summary>Limit</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>Order by</summary>
    [JsonPropertyName("order_by")]
    public Qyl.Domains.Observe.Log.LogOrderBy? OrderBy { get; init; }

}

/// <summary>OpenTelemetry Log Record</summary>
public sealed record LogRecord
{
    /// <summary>Timestamp when the event occurred (nanoseconds since epoch)</summary>
    [JsonPropertyName("time_unix_nano")]
    public required long TimeUnixNano { get; init; }

    /// <summary>Timestamp when the log was observed/collected (nanoseconds since epoch)</summary>
    [JsonPropertyName("observed_time_unix_nano")]
    public required long ObservedTimeUnixNano { get; init; }

    /// <summary>Severity number (1-24)</summary>
    [JsonPropertyName("severity_number")]
    public required Qyl.OTel.Enums.SeverityNumber SeverityNumber { get; init; }

    /// <summary>Severity text (DEBUG, INFO, WARN, ERROR, etc.)</summary>
    [JsonPropertyName("severity_text")]
    public Qyl.OTel.Enums.SeverityText? SeverityText { get; init; }

    /// <summary>Log body - the main content</summary>
    [JsonPropertyName("body")]
    public required Qyl.OTel.Logs.LogBody Body { get; init; }

    /// <summary>Log attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public Qyl.Common.Count? DroppedAttributesCount { get; init; }

    /// <summary>Flags (trace flags)</summary>
    [JsonPropertyName("flags")]
    public int? Flags { get; init; }

    /// <summary>Associated trace ID</summary>
    [JsonPropertyName("trace_id")]
    public Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Associated span ID</summary>
    [JsonPropertyName("span_id")]
    public Qyl.Common.SpanId? SpanId { get; init; }

    /// <summary>Resource describing the entity that produced this log</summary>
    [JsonPropertyName("resource")]
    public required Qyl.OTel.Resource.Resource Resource { get; init; }

    /// <summary>Instrumentation scope</summary>
    [JsonPropertyName("instrumentation_scope")]
    public Qyl.Common.InstrumentationScope? InstrumentationScope { get; init; }

}

/// <summary>Log stats by severity</summary>
public sealed record LogSeverityStats
{
    /// <summary>Severity number</summary>
    [JsonPropertyName("severity")]
    public required Qyl.OTel.Enums.SeverityNumber Severity { get; init; }

    /// <summary>Severity text</summary>
    [JsonPropertyName("severity_text")]
    public required string SeverityText { get; init; }

    /// <summary>Count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>Aggregated log statistics</summary>
public sealed record LogStats
{
    /// <summary>Total log count</summary>
    [JsonPropertyName("total_count")]
    public required Qyl.Common.Count TotalCount { get; init; }

    /// <summary>Log counts by severity</summary>
    [JsonPropertyName("by_severity")]
    public required IReadOnlyList<Qyl.OTel.Logs.LogCountBySeverity> BySeverity { get; init; }

    /// <summary>Log counts by service</summary>
    [JsonPropertyName("by_service")]
    public required IReadOnlyList<Qyl.OTel.Logs.LogCountByDimension> ByService { get; init; }

    /// <summary>Logs per second rate</summary>
    [JsonPropertyName("logs_per_second")]
    public required double LogsPerSecond { get; init; }

    /// <summary>Error log rate</summary>
    [JsonPropertyName("error_rate")]
    public required Qyl.Common.Ratio ErrorRate { get; init; }

}

/// <summary>Log stream event</summary>
public sealed record LogStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Log data</summary>
    [JsonPropertyName("data")]
    public required Qyl.OTel.Logs.LogRecord Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>OpenTelemetry Metric containing measurement data</summary>
public sealed record Metric
{
    /// <summary>Metric name (e.g., http.server.request.duration)</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Metric description</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Metric unit (e.g., 's', 'By', '1')</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    /// <summary>Metric data (discriminated by type)</summary>
    [JsonPropertyName("data")]
    public required Qyl.OTel.Metrics.MetricData Data { get; init; }

    /// <summary>Metric metadata attributes</summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyList<Qyl.Common.Attribute>? Metadata { get; init; }

    /// <summary>Resource describing the entity that produced this metric</summary>
    [JsonPropertyName("resource")]
    public required Qyl.OTel.Resource.Resource Resource { get; init; }

    /// <summary>Instrumentation scope</summary>
    [JsonPropertyName("instrumentation_scope")]
    public Qyl.Common.InstrumentationScope? InstrumentationScope { get; init; }

}

/// <summary>Metric data discriminated by type</summary>
public sealed record MetricData
{
    /// <summary>Metric type discriminator</summary>
    [JsonPropertyName("type")]
    public required Qyl.OTel.Enums.MetricType Type { get; init; }

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
    public required Qyl.OTel.Enums.MetricType Type { get; init; }

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
    public Qyl.Common.Pagination.TimeBucket? Step { get; init; }

    /// <summary>Aggregation function</summary>
    [JsonPropertyName("aggregation")]
    public Qyl.OTel.Metrics.AggregationFunction? Aggregation { get; init; }

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
    public required IReadOnlyList<MetricTimeSeries> Series { get; init; }

}

/// <summary>Metric stream event</summary>
public sealed record MetricStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Metric data</summary>
    [JsonPropertyName("data")]
    public required Qyl.OTel.Metrics.Metric Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Metric time series</summary>
public sealed record MetricTimeSeries
{
    /// <summary>Labels</summary>
    [JsonPropertyName("labels")]
    public required object Labels { get; init; }

    /// <summary>Data points</summary>
    [JsonPropertyName("points")]
    public required IReadOnlyList<MetricDataPoint> Points { get; init; }

}

/// <summary>Resource not found (404)</summary>
public sealed record NotFoundError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>The type of resource that was not found</summary>
    [JsonPropertyName("resource_type")]
    public string? ResourceType { get; init; }

    /// <summary>The identifier that was not found</summary>
    [JsonPropertyName("resource_id")]
    public string? ResourceId { get; init; }

}

/// <summary>Numeric data point (for Gauge and Sum)</summary>
public sealed record NumberDataPoint
{
    /// <summary>Value as integer</summary>
    [JsonPropertyName("as_int")]
    public long? AsInt { get; init; }

    /// <summary>Value as double</summary>
    [JsonPropertyName("as_double")]
    public double? AsDouble { get; init; }

    /// <summary>Exemplars for the data point</summary>
    [JsonPropertyName("exemplars")]
    public IReadOnlyList<Qyl.OTel.Metrics.Exemplar>? Exemplars { get; init; }

}

/// <summary>Operation information</summary>
public sealed record OperationInfo
{
    /// <summary>Operation name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Span kind</summary>
    [JsonPropertyName("span_kind")]
    public required Qyl.OTel.Enums.SpanKind SpanKind { get; init; }

    /// <summary>Request count</summary>
    [JsonPropertyName("request_count")]
    public required Qyl.Common.Count RequestCount { get; init; }

    /// <summary>Error count</summary>
    [JsonPropertyName("error_count")]
    public required Qyl.Common.Count ErrorCount { get; init; }

    /// <summary>Average duration in milliseconds</summary>
    [JsonPropertyName("avg_duration_ms")]
    public required double AvgDurationMs { get; init; }

    /// <summary>P99 duration in milliseconds</summary>
    [JsonPropertyName("p99_duration_ms")]
    public required double P99DurationMs { get; init; }

}

/// <summary>Pipeline run event</summary>
public sealed record PipelineRunEvent
{
    /// <summary>Event name</summary>
    [JsonPropertyName("event.name")]
    public required Qyl.Domains.Ops.Cicd.CicdEventName EventName { get; init; }

    /// <summary>Pipeline name</summary>
    [JsonPropertyName("cicd.pipeline.name")]
    public required string CicdPipelineName { get; init; }

    /// <summary>Pipeline run ID</summary>
    [JsonPropertyName("cicd.pipeline.run.id")]
    public required string CicdPipelineRunId { get; init; }

    /// <summary>Pipeline status</summary>
    [JsonPropertyName("status")]
    public required Qyl.Domains.Ops.Cicd.CicdPipelineStatus Status { get; init; }

    /// <summary>CI/CD system</summary>
    [JsonPropertyName("system")]
    public required Qyl.Domains.Ops.Cicd.CicdSystem System { get; init; }

    /// <summary>Trigger type</summary>
    [JsonPropertyName("trigger_type")]
    public Qyl.Domains.Ops.Cicd.CicdTriggerType? TriggerType { get; init; }

    /// <summary>Git branch</summary>
    [JsonPropertyName("vcs.repository.ref.name")]
    public string? VcsRepositoryRefName { get; init; }

    /// <summary>Git commit SHA</summary>
    [JsonPropertyName("vcs.repository.ref.revision")]
    public string? VcsRepositoryRefRevision { get; init; }

    /// <summary>Duration in seconds</summary>
    [JsonPropertyName("duration_s")]
    public Qyl.Common.DurationS? DurationS { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Pipeline statistics</summary>
public sealed record PipelineStats
{
    /// <summary>Total runs</summary>
    [JsonPropertyName("total_runs")]
    public required Qyl.Common.Count TotalRuns { get; init; }

    /// <summary>Success rate</summary>
    [JsonPropertyName("success_rate")]
    public required Qyl.Common.Ratio SuccessRate { get; init; }

    /// <summary>Average duration in seconds</summary>
    [JsonPropertyName("avg_duration_seconds")]
    public required double AvgDurationSeconds { get; init; }

    /// <summary>P95 duration in seconds</summary>
    [JsonPropertyName("p95_duration_seconds")]
    public required double P95DurationSeconds { get; init; }

    /// <summary>Runs by status</summary>
    [JsonPropertyName("by_status")]
    public required IReadOnlyList<PipelineStatusStats> ByStatus { get; init; }

}

/// <summary>Pipeline status statistics</summary>
public sealed record PipelineStatusStats
{
    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required Qyl.Domains.Ops.Cicd.CicdPipelineStatus Status { get; init; }

    /// <summary>Count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage</summary>
    [JsonPropertyName("percentage")]
    public required Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>RFC 7807 Problem Details for HTTP APIs</summary>
public sealed record ProblemDetails
{
    /// <summary>A URI reference identifying the problem type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>A short, human-readable summary of the problem type</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>The HTTP status code (informational only, actual code set by subtype)</summary>
    [JsonPropertyName("status")]
    public required int Status { get; init; }

    /// <summary>A human-readable explanation specific to this occurrence</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    /// <summary>A URI reference identifying the specific occurrence</summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    /// <summary>Timestamp of the error</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

}

/// <summary>Quantile value for summary</summary>
public sealed record QuantileValue
{
    /// <summary>Quantile (0.0 to 1.0)</summary>
    [JsonPropertyName("quantile")]
    public required double Quantile { get; init; }

    /// <summary>Value at this quantile</summary>
    [JsonPropertyName("value")]
    public required double Value { get; init; }

}

/// <summary>Rate limited - too many requests (429)</summary>
public sealed record RateLimitError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

}

/// <summary>Resource describes the entity producing telemetry</summary>
public sealed record Resource
{
    /// <summary>Service name (required)</summary>
    [JsonPropertyName("service.name")]
    public required string ServiceName { get; init; }

    /// <summary>Service namespace for grouping</summary>
    [JsonPropertyName("service.namespace")]
    public string? ServiceNamespace { get; init; }

    /// <summary>Service instance ID (unique per instance)</summary>
    [JsonPropertyName("service.instance.id")]
    public string? ServiceInstanceId { get; init; }

    /// <summary>Service version</summary>
    [JsonPropertyName("service.version")]
    public Qyl.Common.SemVer? ServiceVersion { get; init; }

    /// <summary>Telemetry SDK name</summary>
    [JsonPropertyName("telemetry.sdk.name")]
    public string? TelemetrySdkName { get; init; }

    /// <summary>Telemetry SDK language</summary>
    [JsonPropertyName("telemetry.sdk.language")]
    public Qyl.OTel.Enums.TelemetrySdkLanguage? TelemetrySdkLanguage { get; init; }

    /// <summary>Telemetry SDK version</summary>
    [JsonPropertyName("telemetry.sdk.version")]
    public Qyl.Common.SemVer? TelemetrySdkVersion { get; init; }

    /// <summary>Auto-instrumentation agent name</summary>
    [JsonPropertyName("telemetry.auto.version")]
    public Qyl.Common.SemVer? TelemetryAutoVersion { get; init; }

    /// <summary>Deployment environment (e.g., production, staging)</summary>
    [JsonPropertyName("deployment.environment.name")]
    public string? DeploymentEnvironmentName { get; init; }

    /// <summary>Cloud provider</summary>
    [JsonPropertyName("cloud.provider")]
    public Qyl.OTel.Resource.CloudProvider? CloudProvider { get; init; }

    /// <summary>Cloud region</summary>
    [JsonPropertyName("cloud.region")]
    public string? CloudRegion { get; init; }

    /// <summary>Cloud availability zone</summary>
    [JsonPropertyName("cloud.availability_zone")]
    public string? CloudAvailabilityZone { get; init; }

    /// <summary>Cloud account ID</summary>
    [JsonPropertyName("cloud.account.id")]
    public string? CloudAccountId { get; init; }

    /// <summary>Cloud platform (e.g., aws_ecs, gcp_cloud_run)</summary>
    [JsonPropertyName("cloud.platform")]
    public string? CloudPlatform { get; init; }

    /// <summary>Host name</summary>
    [JsonPropertyName("host.name")]
    public string? HostName { get; init; }

    /// <summary>Host ID</summary>
    [JsonPropertyName("host.id")]
    public string? HostId { get; init; }

    /// <summary>Host type (e.g., n1-standard-1)</summary>
    [JsonPropertyName("host.type")]
    public string? HostType { get; init; }

    /// <summary>Host architecture (e.g., amd64, arm64)</summary>
    [JsonPropertyName("host.arch")]
    public Qyl.OTel.Resource.HostArch? HostArch { get; init; }

    /// <summary>Operating system type</summary>
    [JsonPropertyName("os.type")]
    public Qyl.OTel.Resource.OsType? OsType { get; init; }

    /// <summary>Operating system description</summary>
    [JsonPropertyName("os.description")]
    public string? OsDescription { get; init; }

    /// <summary>Operating system version</summary>
    [JsonPropertyName("os.version")]
    public string? OsVersion { get; init; }

    /// <summary>Process ID</summary>
    [JsonPropertyName("process.pid")]
    public long? ProcessPid { get; init; }

    /// <summary>Process executable name</summary>
    [JsonPropertyName("process.executable.name")]
    public string? ProcessExecutableName { get; init; }

    /// <summary>Process command line</summary>
    [JsonPropertyName("process.command_line")]
    public string? ProcessCommandLine { get; init; }

    /// <summary>Process runtime name</summary>
    [JsonPropertyName("process.runtime.name")]
    public string? ProcessRuntimeName { get; init; }

    /// <summary>Process runtime version</summary>
    [JsonPropertyName("process.runtime.version")]
    public string? ProcessRuntimeVersion { get; init; }

    /// <summary>Container ID</summary>
    [JsonPropertyName("container.id")]
    public string? ContainerId { get; init; }

    /// <summary>Container name</summary>
    [JsonPropertyName("container.name")]
    public string? ContainerName { get; init; }

    /// <summary>Container image name</summary>
    [JsonPropertyName("container.image.name")]
    public string? ContainerImageName { get; init; }

    /// <summary>Container image tag</summary>
    [JsonPropertyName("container.image.tag")]
    public string? ContainerImageTag { get; init; }

    /// <summary>Kubernetes cluster name</summary>
    [JsonPropertyName("k8s.cluster.name")]
    public string? K8sClusterName { get; init; }

    /// <summary>Kubernetes namespace</summary>
    [JsonPropertyName("k8s.namespace.name")]
    public string? K8sNamespaceName { get; init; }

    /// <summary>Kubernetes pod name</summary>
    [JsonPropertyName("k8s.pod.name")]
    public string? K8sPodName { get; init; }

    /// <summary>Kubernetes pod UID</summary>
    [JsonPropertyName("k8s.pod.uid")]
    public string? K8sPodUid { get; init; }

    /// <summary>Kubernetes deployment name</summary>
    [JsonPropertyName("k8s.deployment.name")]
    public string? K8sDeploymentName { get; init; }

    /// <summary>Additional resource attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public Qyl.Common.Count? DroppedAttributesCount { get; init; }

}

/// <summary>Service dependency map</summary>
public sealed record ServiceDependency
{
    /// <summary>Source service</summary>
    [JsonPropertyName("source_service")]
    public required string SourceService { get; init; }

    /// <summary>Target service</summary>
    [JsonPropertyName("target_service")]
    public required string TargetService { get; init; }

    /// <summary>Request count</summary>
    [JsonPropertyName("request_count")]
    public required Qyl.Common.Count RequestCount { get; init; }

    /// <summary>Error rate</summary>
    [JsonPropertyName("error_rate")]
    public required Qyl.Common.Ratio ErrorRate { get; init; }

    /// <summary>Average latency in milliseconds</summary>
    [JsonPropertyName("avg_latency_ms")]
    public required double AvgLatencyMs { get; init; }

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
    public Qyl.Common.SemVer? Version { get; init; }

    /// <summary>Instance count</summary>
    [JsonPropertyName("instance_count")]
    public required int InstanceCount { get; init; }

    /// <summary>Last seen</summary>
    [JsonPropertyName("last_seen")]
    public required DateTimeOffset LastSeen { get; init; }

    /// <summary>Resource attributes</summary>
    [JsonPropertyName("resource_attributes")]
    public required IReadOnlyList<Qyl.Common.Attribute> ResourceAttributes { get; init; }

    /// <summary>Instrumentation libraries</summary>
    [JsonPropertyName("instrumentation_libraries")]
    public required IReadOnlyList<Qyl.Common.InstrumentationScope> InstrumentationLibraries { get; init; }

    /// <summary>Request rate (per second)</summary>
    [JsonPropertyName("request_rate")]
    public required double RequestRate { get; init; }

    /// <summary>Error rate</summary>
    [JsonPropertyName("error_rate")]
    public required Qyl.Common.Ratio ErrorRate { get; init; }

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
    public Qyl.Common.SemVer? Version { get; init; }

    /// <summary>Instance count</summary>
    [JsonPropertyName("instance_count")]
    public required int InstanceCount { get; init; }

    /// <summary>Last seen</summary>
    [JsonPropertyName("last_seen")]
    public required DateTimeOffset LastSeen { get; init; }

}

/// <summary>Service unavailable (503)</summary>
public sealed record ServiceUnavailableError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Reason for unavailability</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

}

/// <summary>Session client information</summary>
public sealed record SessionClientInfo
{
    /// <summary>Client IP address</summary>
    [JsonPropertyName("ip")]
    public Qyl.Common.IpAddress? Ip { get; init; }

    /// <summary>User agent string</summary>
    [JsonPropertyName("user_agent")]
    public Qyl.Common.UserAgent? UserAgent { get; init; }

    /// <summary>Device type</summary>
    [JsonPropertyName("device_type")]
    public Qyl.Domains.Observe.Session.DeviceType? DeviceType { get; init; }

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
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>Session stats by device type</summary>
public sealed record SessionDeviceStats
{
    /// <summary>Device type</summary>
    [JsonPropertyName("device_type")]
    public required Qyl.Domains.Observe.Session.DeviceType DeviceType { get; init; }

    /// <summary>Session count</summary>
    [JsonPropertyName("count")]
    public required Qyl.Common.Count Count { get; init; }

    /// <summary>Percentage of total</summary>
    [JsonPropertyName("percentage")]
    public required Qyl.Common.Percentage Percentage { get; init; }

}

/// <summary>Complete session entity with aggregated data</summary>
public sealed record SessionEntity
{
    /// <summary>Session ID</summary>
    [JsonPropertyName("session.id")]
    public required Qyl.Common.SessionId SessionId { get; init; }

    /// <summary>User ID (if authenticated)</summary>
    [JsonPropertyName("user.id")]
    public Qyl.Common.UserId? UserId { get; init; }

    /// <summary>Session start time</summary>
    [JsonPropertyName("start_time")]
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>Session end time</summary>
    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>Session duration in milliseconds</summary>
    [JsonPropertyName("duration_ms")]
    public Qyl.Common.DurationMs? DurationMs { get; init; }

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
    public required Qyl.Domains.Observe.Session.SessionState State { get; init; }

    /// <summary>Client information</summary>
    [JsonPropertyName("client")]
    public Qyl.Domains.Observe.Session.SessionClientInfo? Client { get; init; }

    /// <summary>Location information</summary>
    [JsonPropertyName("geo")]
    public Qyl.Domains.Observe.Session.SessionGeoInfo? Geo { get; init; }

    /// <summary>GenAI usage summary</summary>
    [JsonPropertyName("genai_usage")]
    public Qyl.Domains.Observe.Session.SessionGenAiUsage? GenaiUsage { get; init; }

}

/// <summary>Session GenAI usage summary</summary>
public sealed record SessionGenAiUsage
{
    /// <summary>Total GenAI requests in session</summary>
    [JsonPropertyName("request_count")]
    public required int RequestCount { get; init; }

    /// <summary>Total input tokens consumed</summary>
    [JsonPropertyName("total_input_tokens")]
    public required Qyl.Common.TokenCount TotalInputTokens { get; init; }

    /// <summary>Total output tokens generated</summary>
    [JsonPropertyName("total_output_tokens")]
    public required Qyl.Common.TokenCount TotalOutputTokens { get; init; }

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
    public required Qyl.Common.Count ActiveSessions { get; init; }

    /// <summary>Total sessions in time range</summary>
    [JsonPropertyName("total_sessions")]
    public required Qyl.Common.Count TotalSessions { get; init; }

    /// <summary>Unique users in time range</summary>
    [JsonPropertyName("unique_users")]
    public required Qyl.Common.Count UniqueUsers { get; init; }

    /// <summary>Average session duration in milliseconds</summary>
    [JsonPropertyName("avg_duration_ms")]
    public required double AvgDurationMs { get; init; }

    /// <summary>Sessions with errors</summary>
    [JsonPropertyName("sessions_with_errors")]
    public required Qyl.Common.Count SessionsWithErrors { get; init; }

    /// <summary>Sessions with GenAI usage</summary>
    [JsonPropertyName("sessions_with_genai")]
    public required Qyl.Common.Count SessionsWithGenai { get; init; }

    /// <summary>Bounce rate (single-page sessions)</summary>
    [JsonPropertyName("bounce_rate")]
    public required Qyl.Common.Ratio BounceRate { get; init; }

    /// <summary>Sessions by device type</summary>
    [JsonPropertyName("by_device_type")]
    public IReadOnlyList<Qyl.Domains.Observe.Session.SessionDeviceStats>? ByDeviceType { get; init; }

    /// <summary>Sessions by country</summary>
    [JsonPropertyName("by_country")]
    public IReadOnlyList<Qyl.Domains.Observe.Session.SessionCountryStats>? ByCountry { get; init; }

}

/// <summary>OpenTelemetry Span representing a single operation in a distributed trace</summary>
public sealed record Span
{
    /// <summary>Unique span identifier (16 hex chars)</summary>
    [JsonPropertyName("span_id")]
    public required Qyl.Common.SpanId SpanId { get; init; }

    /// <summary>Trace identifier (32 hex chars)</summary>
    [JsonPropertyName("trace_id")]
    public required Qyl.Common.TraceId TraceId { get; init; }

    /// <summary>Parent span identifier (null for root spans)</summary>
    [JsonPropertyName("parent_span_id")]
    public Qyl.Common.SpanId? ParentSpanId { get; init; }

    /// <summary>W3C trace state</summary>
    [JsonPropertyName("trace_state")]
    public Qyl.Common.TraceState? TraceState { get; init; }

    /// <summary>Human-readable span name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Span kind</summary>
    [JsonPropertyName("kind")]
    public required Qyl.OTel.Enums.SpanKind Kind { get; init; }

    /// <summary>Start timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("start_time_unix_nano")]
    public required long StartTimeUnixNano { get; init; }

    /// <summary>End timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("end_time_unix_nano")]
    public required long EndTimeUnixNano { get; init; }

    /// <summary>Span attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public Qyl.Common.Count? DroppedAttributesCount { get; init; }

    /// <summary>Span events (logs attached to span)</summary>
    [JsonPropertyName("events")]
    public IReadOnlyList<Qyl.OTel.Traces.SpanEvent>? Events { get; init; }

    /// <summary>Dropped events count</summary>
    [JsonPropertyName("dropped_events_count")]
    public Qyl.Common.Count? DroppedEventsCount { get; init; }

    /// <summary>Links to other spans</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<Qyl.OTel.Traces.SpanLink>? Links { get; init; }

    /// <summary>Dropped links count</summary>
    [JsonPropertyName("dropped_links_count")]
    public Qyl.Common.Count? DroppedLinksCount { get; init; }

    /// <summary>Span status</summary>
    [JsonPropertyName("status")]
    public required Qyl.OTel.Traces.SpanStatus Status { get; init; }

    /// <summary>Span flags</summary>
    [JsonPropertyName("flags")]
    public int? Flags { get; init; }

    /// <summary>Resource describing the entity that produced this span</summary>
    [JsonPropertyName("resource")]
    public required Qyl.OTel.Resource.Resource Resource { get; init; }

    /// <summary>Instrumentation scope</summary>
    [JsonPropertyName("instrumentation_scope")]
    public Qyl.Common.InstrumentationScope? InstrumentationScope { get; init; }

}

/// <summary>Event occurring during a span's lifetime</summary>
public sealed record SpanEvent
{
    /// <summary>Event name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Event timestamp in nanoseconds since epoch</summary>
    [JsonPropertyName("time_unix_nano")]
    public required long TimeUnixNano { get; init; }

    /// <summary>Event attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public Qyl.Common.Count? DroppedAttributesCount { get; init; }

}

/// <summary>Link to another span (e.g., batch processing)</summary>
public sealed record SpanLink
{
    /// <summary>Linked trace ID</summary>
    [JsonPropertyName("trace_id")]
    public required Qyl.Common.TraceId TraceId { get; init; }

    /// <summary>Linked span ID</summary>
    [JsonPropertyName("span_id")]
    public required Qyl.Common.SpanId SpanId { get; init; }

    /// <summary>Trace state of the linked span</summary>
    [JsonPropertyName("trace_state")]
    public Qyl.Common.TraceState? TraceState { get; init; }

    /// <summary>Link attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public Qyl.Common.Count? DroppedAttributesCount { get; init; }

    /// <summary>Link flags</summary>
    [JsonPropertyName("flags")]
    public int? Flags { get; init; }

}

/// <summary>Span status</summary>
public sealed record SpanStatus
{
    /// <summary>Status code</summary>
    [JsonPropertyName("code")]
    public required Qyl.OTel.Enums.SpanStatusCode Code { get; init; }

    /// <summary>Status message (only for ERROR status)</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

}

/// <summary>Span stream event</summary>
public sealed record SpanStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Span data</summary>
    [JsonPropertyName("data")]
    public required Qyl.OTel.Traces.Span Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Single frame in a call stack</summary>
public sealed record StackFrame
{
    /// <summary>Frame index (0 = top of stack)</summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>Source location</summary>
    [JsonPropertyName("location")]
    public required Qyl.Domains.AI.Code.CodeLocation Location { get; init; }

    /// <summary>Whether this is user code (not library/framework)</summary>
    [JsonPropertyName("is_user_code")]
    public bool? IsUserCode { get; init; }

    /// <summary>Assembly/module name</summary>
    [JsonPropertyName("module_name")]
    public string? ModuleName { get; init; }

    /// <summary>Assembly/module version</summary>
    [JsonPropertyName("module_version")]
    public Qyl.Common.SemVer? ModuleVersion { get; init; }

    /// <summary>Native/managed indicator</summary>
    [JsonPropertyName("is_native")]
    public bool? IsNative { get; init; }

}

/// <summary>Full stack trace</summary>
public sealed record StackTrace
{
    /// <summary>Stack frames (top to bottom)</summary>
    [JsonPropertyName("frames")]
    public required IReadOnlyList<Qyl.Domains.AI.Code.StackFrame> Frames { get; init; }

    /// <summary>Whether the stack was truncated</summary>
    [JsonPropertyName("truncated")]
    public bool? Truncated { get; init; }

    /// <summary>Total frame count before truncation</summary>
    [JsonPropertyName("total_frames")]
    public int? TotalFrames { get; init; }

}

/// <summary>Stream subscription request</summary>
public sealed record StreamSubscription
{
    /// <summary>Event types to subscribe to</summary>
    [JsonPropertyName("event_types")]
    public required IReadOnlyList<Streaming.StreamEventType> EventTypes { get; init; }

    /// <summary>Service name filter</summary>
    [JsonPropertyName("service_name")]
    public string? ServiceName { get; init; }

    /// <summary>Trace ID filter (for specific trace)</summary>
    [JsonPropertyName("trace_id")]
    public Qyl.Common.TraceId? TraceId { get; init; }

    /// <summary>Minimum severity for logs (1-24)</summary>
    [JsonPropertyName("min_severity")]
    public int? MinSeverity { get; init; }

    /// <summary>Attribute filters</summary>
    [JsonPropertyName("filters")]
    public object? Filters { get; init; }

    /// <summary>Sample rate (0.0-1.0)</summary>
    [JsonPropertyName("sample_rate")]
    public double? SampleRate { get; init; }

}

/// <summary>Sum metric - cumulative or delta counter</summary>
public sealed record SumData
{
    /// <summary>Discriminator identifying this as sum metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Sum data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<Qyl.OTel.Metrics.NumberDataPoint> DataPoints { get; init; }

    /// <summary>Whether the sum is monotonically increasing</summary>
    [JsonPropertyName("is_monotonic")]
    public required bool IsMonotonic { get; init; }

    /// <summary>Aggregation temporality</summary>
    [JsonPropertyName("aggregation_temporality")]
    public required Qyl.OTel.Enums.AggregationTemporality AggregationTemporality { get; init; }

}

/// <summary>Summary metric - pre-aggregated quantile distribution</summary>
public sealed record SummaryData
{
    /// <summary>Discriminator identifying this as summary metric data</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Summary data points</summary>
    [JsonPropertyName("data_points")]
    public required IReadOnlyList<Qyl.OTel.Metrics.SummaryDataPoint> DataPoints { get; init; }

}

/// <summary>Summary data point</summary>
public sealed record SummaryDataPoint
{
    /// <summary>Number of values</summary>
    [JsonPropertyName("count")]
    public required long Count { get; init; }

    /// <summary>Sum of all values</summary>
    [JsonPropertyName("sum")]
    public required double Sum { get; init; }

    /// <summary>Quantile values</summary>
    [JsonPropertyName("quantile_values")]
    public required IReadOnlyList<Qyl.OTel.Metrics.QuantileValue> QuantileValues { get; init; }

}

/// <summary>Tail-based sampling configuration for streaming</summary>
public sealed record TailSamplingConfig
{
    /// <summary>Enable tail-based sampling</summary>
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    /// <summary>Sample error traces</summary>
    [JsonPropertyName("sample_errors")]
    public required bool SampleErrors { get; init; }

    /// <summary>Sample slow traces (above threshold)</summary>
    [JsonPropertyName("sample_slow")]
    public required bool SampleSlow { get; init; }

    /// <summary>Slow trace threshold in milliseconds</summary>
    [JsonPropertyName("slow_threshold_ms")]
    public long? SlowThresholdMs { get; init; }

    /// <summary>Random sample rate for remaining traces</summary>
    [JsonPropertyName("random_rate")]
    public required double RandomRate { get; init; }

}

/// <summary>Complete trace containing all related spans</summary>
public sealed record Trace
{
    /// <summary>Trace identifier</summary>
    [JsonPropertyName("trace_id")]
    public required Qyl.Common.TraceId TraceId { get; init; }

    /// <summary>All spans in this trace</summary>
    [JsonPropertyName("spans")]
    public required IReadOnlyList<Qyl.OTel.Traces.Span> Spans { get; init; }

    /// <summary>Root span of the trace</summary>
    [JsonPropertyName("root_span")]
    public Qyl.OTel.Traces.Span? RootSpan { get; init; }

    /// <summary>Total span count</summary>
    [JsonPropertyName("span_count")]
    public required int SpanCount { get; init; }

    /// <summary>Trace duration in nanoseconds</summary>
    [JsonPropertyName("duration_ns")]
    public required Qyl.Common.DurationNs DurationNs { get; init; }

    /// <summary>Trace start time</summary>
    [JsonPropertyName("start_time")]
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>Trace end time</summary>
    [JsonPropertyName("end_time")]
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Services involved in this trace</summary>
    [JsonPropertyName("services")]
    public required IReadOnlyList<string> Services { get; init; }

    /// <summary>Whether trace contains errors</summary>
    [JsonPropertyName("has_error")]
    public required bool HasError { get; init; }

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
    public Qyl.OTel.Enums.SpanStatusCode? Status { get; init; }

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

/// <summary>Trace stream event</summary>
public sealed record TraceStreamEvent
{
    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Trace data</summary>
    [JsonPropertyName("data")]
    public required Qyl.OTel.Traces.Trace Data { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Unauthorized - authentication required (401)</summary>
public sealed record UnauthorizedError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

}

/// <summary>Bad request - validation failed (400)</summary>
public sealed record ValidationError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>List of validation errors</summary>
    [JsonPropertyName("errors")]
    public required IReadOnlyList<Qyl.Common.Errors.ValidationErrorDetail> Errors { get; init; }

}

/// <summary>Individual validation error detail</summary>
public sealed record ValidationErrorDetail
{
    /// <summary>The field/property that failed validation</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    /// <summary>The error message</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>The validation rule that failed</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>The rejected value (if safe to include)</summary>
    [JsonPropertyName("rejected_value")]
    public string? RejectedValue { get; init; }

}

