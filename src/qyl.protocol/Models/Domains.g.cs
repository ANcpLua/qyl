// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-02-26T23:57:51.4116240+00:00
//     Models for Qyl.Domains
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Domains;

/// <summary>Triggered alert instance</summary>
public sealed record AlertFiringEntity
{
    /// <summary>Firing ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Source rule</summary>
    [JsonPropertyName("rule_id")]
    public required string RuleId { get; init; }

    /// <summary>Dedup fingerprint</summary>
    [JsonPropertyName("fingerprint")]
    public required string Fingerprint { get; init; }

    /// <summary>Alert severity</summary>
    [JsonPropertyName("severity")]
    public required global::Qyl.Domains.AlertSeverity Severity { get; init; }

    /// <summary>Alert title</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Alert message</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Measured value that triggered the alert</summary>
    [JsonPropertyName("trigger_value")]
    public double? TriggerValue { get; init; }

    /// <summary>Threshold value</summary>
    [JsonPropertyName("threshold_value")]
    public double? ThresholdValue { get; init; }

    /// <summary>Additional context</summary>
    [JsonPropertyName("context_json")]
    public string? ContextJson { get; init; }

    /// <summary>Firing status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.AlertFiringStatus Status { get; init; }

    /// <summary>Acknowledgment timestamp</summary>
    [JsonPropertyName("acknowledged_at")]
    public DateTimeOffset? AcknowledgedAt { get; init; }

    /// <summary>Acknowledged by</summary>
    [JsonPropertyName("acknowledged_by")]
    public string? AcknowledgedBy { get; init; }

    /// <summary>Resolution timestamp</summary>
    [JsonPropertyName("resolved_at")]
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>Firing timestamp</summary>
    [JsonPropertyName("fired_at")]
    public required DateTimeOffset FiredAt { get; init; }

    /// <summary>Dedup key for suppressing duplicates</summary>
    [JsonPropertyName("dedup_key")]
    public string? DedupKey { get; init; }

}

/// <summary>Alert rule definition</summary>
public sealed record AlertRuleEntity
{
    /// <summary>Rule ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Owning project</summary>
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    /// <summary>Rule name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Rule description</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Rule type</summary>
    [JsonPropertyName("rule_type")]
    public required global::Qyl.Domains.AlertRuleType RuleType { get; init; }

    /// <summary>Condition definition</summary>
    [JsonPropertyName("condition_json")]
    public required string ConditionJson { get; init; }

    /// <summary>Threshold definition</summary>
    [JsonPropertyName("threshold_json")]
    public string? ThresholdJson { get; init; }

    /// <summary>Target type for evaluation</summary>
    [JsonPropertyName("target_type")]
    public required string TargetType { get; init; }

    /// <summary>Target filter</summary>
    [JsonPropertyName("target_filter_json")]
    public string? TargetFilterJson { get; init; }

    /// <summary>Alert severity</summary>
    [JsonPropertyName("severity")]
    public required global::Qyl.Domains.AlertSeverity Severity { get; init; }

    /// <summary>Cooldown between firings in seconds</summary>
    [JsonPropertyName("cooldown_seconds")]
    public required int CooldownSeconds { get; init; }

    /// <summary>Notification channels</summary>
    [JsonPropertyName("notification_channels_json")]
    public string? NotificationChannelsJson { get; init; }

    /// <summary>Whether rule is enabled</summary>
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    /// <summary>Last trigger timestamp</summary>
    [JsonPropertyName("last_triggered_at")]
    public DateTimeOffset? LastTriggeredAt { get; init; }

    /// <summary>Total trigger count</summary>
    [JsonPropertyName("trigger_count")]
    public required long TriggerCount { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last update timestamp</summary>
    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; init; }

}

/// <summary>Pre-error context breadcrumb</summary>
public sealed record ErrorBreadcrumbEntity
{
    /// <summary>Breadcrumb ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Parent event</summary>
    [JsonPropertyName("event_id")]
    public required string EventId { get; init; }

    /// <summary>Breadcrumb type</summary>
    [JsonPropertyName("breadcrumb_type")]
    public required global::Qyl.Domains.BreadcrumbType BreadcrumbType { get; init; }

    /// <summary>Category (e.g. http, db, ui)</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>Breadcrumb message</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Severity level</summary>
    [JsonPropertyName("level")]
    public required string Level { get; init; }

    /// <summary>Breadcrumb data</summary>
    [JsonPropertyName("data_json")]
    public string? DataJson { get; init; }

    /// <summary>Breadcrumb timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Error issue aggregate with lifecycle tracking</summary>
public sealed record ErrorIssueEntity
{
    /// <summary>Issue ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Owning project</summary>
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    /// <summary>Error fingerprint for grouping</summary>
    [JsonPropertyName("fingerprint")]
    public required string Fingerprint { get; init; }

    /// <summary>Issue title</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Culprit (function/module causing the error)</summary>
    [JsonPropertyName("culprit")]
    public string? Culprit { get; init; }

    /// <summary>Error type (exception class name or code)</summary>
    [JsonPropertyName("error_type")]
    public required string ErrorType { get; init; }

    /// <summary>Error category</summary>
    [JsonPropertyName("category")]
    public required string Category { get; init; }

    /// <summary>Severity level</summary>
    [JsonPropertyName("level")]
    public required global::Qyl.Domains.IssueLevel Level { get; init; }

    /// <summary>Platform (csharp, javascript, python, etc.)</summary>
    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    /// <summary>First occurrence</summary>
    [JsonPropertyName("first_seen_at")]
    public required DateTimeOffset FirstSeenAt { get; init; }

    /// <summary>Last occurrence</summary>
    [JsonPropertyName("last_seen_at")]
    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>Total occurrence count</summary>
    [JsonPropertyName("occurrence_count")]
    public required long OccurrenceCount { get; init; }

    /// <summary>Affected unique users count</summary>
    [JsonPropertyName("affected_users_count")]
    public required int AffectedUsersCount { get; init; }

    /// <summary>Issue status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.IssueStatus Status { get; init; }

    /// <summary>Issue substatus</summary>
    [JsonPropertyName("substatus")]
    public string? Substatus { get; init; }

    /// <summary>Priority level</summary>
    [JsonPropertyName("priority")]
    public required global::Qyl.Domains.IssuePriority Priority { get; init; }

    /// <summary>Assigned team member</summary>
    [JsonPropertyName("assigned_to")]
    public string? AssignedTo { get; init; }

    /// <summary>Resolution timestamp</summary>
    [JsonPropertyName("resolved_at")]
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>Resolved by</summary>
    [JsonPropertyName("resolved_by")]
    public string? ResolvedBy { get; init; }

    /// <summary>Number of regressions</summary>
    [JsonPropertyName("regression_count")]
    public required int RegressionCount { get; init; }

    /// <summary>Last associated release</summary>
    [JsonPropertyName("last_release")]
    public string? LastRelease { get; init; }

    /// <summary>Issue tags</summary>
    [JsonPropertyName("tags_json")]
    public string? TagsJson { get; init; }

    /// <summary>Issue metadata</summary>
    [JsonPropertyName("metadata_json")]
    public string? MetadataJson { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last update timestamp</summary>
    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; init; }

}

/// <summary>Individual error occurrence linked to an issue</summary>
public sealed record ErrorIssueEventEntity
{
    /// <summary>Event ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Parent issue</summary>
    [JsonPropertyName("issue_id")]
    public required string IssueId { get; init; }

    /// <summary>Associated trace ID</summary>
    [JsonPropertyName("trace_id")]
    public string? TraceId { get; init; }

    /// <summary>Associated span ID</summary>
    [JsonPropertyName("span_id")]
    public string? SpanId { get; init; }

    /// <summary>Error message</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Stack trace</summary>
    [JsonPropertyName("stack_trace")]
    public string? StackTrace { get; init; }

    /// <summary>Parsed stack frames</summary>
    [JsonPropertyName("stack_frames_json")]
    public string? StackFramesJson { get; init; }

    /// <summary>Environment (dev, staging, prod)</summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; init; }

    /// <summary>Release version</summary>
    [JsonPropertyName("release_version")]
    public string? ReleaseVersion { get; init; }

    /// <summary>Affected user ID</summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>Client IP address</summary>
    [JsonPropertyName("user_ip")]
    public string? UserIp { get; init; }

    /// <summary>Request URL</summary>
    [JsonPropertyName("request_url")]
    public string? RequestUrl { get; init; }

    /// <summary>HTTP request method</summary>
    [JsonPropertyName("request_method")]
    public string? RequestMethod { get; init; }

    /// <summary>Browser info</summary>
    [JsonPropertyName("browser")]
    public string? Browser { get; init; }

    /// <summary>Operating system</summary>
    [JsonPropertyName("os")]
    public string? Os { get; init; }

    /// <summary>Device info</summary>
    [JsonPropertyName("device")]
    public string? Device { get; init; }

    /// <summary>Runtime name</summary>
    [JsonPropertyName("runtime")]
    public string? Runtime { get; init; }

    /// <summary>Runtime version</summary>
    [JsonPropertyName("runtime_version")]
    public string? RuntimeVersion { get; init; }

    /// <summary>Additional context data</summary>
    [JsonPropertyName("context_json")]
    public string? ContextJson { get; init; }

    /// <summary>Event tags</summary>
    [JsonPropertyName("tags_json")]
    public string? TagsJson { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>AI-assisted fix attempt</summary>
public sealed record FixRunEntity
{
    /// <summary>Fix run ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Target issue</summary>
    [JsonPropertyName("issue_id")]
    public required string IssueId { get; init; }

    /// <summary>Triggering alert firing</summary>
    [JsonPropertyName("alert_firing_id")]
    public string? AlertFiringId { get; init; }

    /// <summary>What triggered the fix</summary>
    [JsonPropertyName("trigger_type")]
    public required global::Qyl.Domains.FixTriggerType TriggerType { get; init; }

    /// <summary>Fix strategy</summary>
    [JsonPropertyName("strategy")]
    public required string Strategy { get; init; }

    /// <summary>AI model used</summary>
    [JsonPropertyName("model_name")]
    public string? ModelName { get; init; }

    /// <summary>AI provider</summary>
    [JsonPropertyName("model_provider")]
    public string? ModelProvider { get; init; }

    /// <summary>Fix run status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.FixRunStatus Status { get; init; }

    /// <summary>Error message if failed</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    /// <summary>Tokens consumed</summary>
    [JsonPropertyName("tokens_used")]
    public int? TokensUsed { get; init; }

    /// <summary>Duration in milliseconds</summary>
    [JsonPropertyName("duration_ms")]
    public int? DurationMs { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Start timestamp</summary>
    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Completion timestamp</summary>
    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; init; }

}

/// <summary>Code generation job entry</summary>
public sealed record GenerationJobEntity
{
    /// <summary>Job ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Workspace</summary>
    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    /// <summary>Profile</summary>
    [JsonPropertyName("profile_id")]
    public required string ProfileId { get; init; }

    /// <summary>Job type</summary>
    [JsonPropertyName("job_type")]
    public required global::Qyl.Domains.GenerationJobType JobType { get; init; }

    /// <summary>Job status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.JobStatus Status { get; init; }

    /// <summary>Priority (higher = more urgent)</summary>
    [JsonPropertyName("priority")]
    public required int Priority { get; init; }

    /// <summary>Hash of inputs for dedup</summary>
    [JsonPropertyName("input_hash")]
    public string? InputHash { get; init; }

    /// <summary>Local path where output was written</summary>
    [JsonPropertyName("output_path")]
    public string? OutputPath { get; init; }

    /// <summary>Hash of generated output</summary>
    [JsonPropertyName("output_hash")]
    public string? OutputHash { get; init; }

    /// <summary>Error message if failed</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    /// <summary>Queue timestamp</summary>
    [JsonPropertyName("queued_at")]
    public required DateTimeOffset QueuedAt { get; init; }

    /// <summary>Start timestamp</summary>
    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Completion timestamp</summary>
    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Duration in milliseconds</summary>
    [JsonPropertyName("duration_ms")]
    public int? DurationMs { get; init; }

}

/// <summary>Named instrumentation profile for code generation</summary>
public sealed record GenerationProfileEntity
{
    /// <summary>Profile ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Owning project</summary>
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    /// <summary>Profile name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Profile description</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Target framework (e.g. net10.0)</summary>
    [JsonPropertyName("target_framework")]
    public required string TargetFramework { get; init; }

    /// <summary>Target language</summary>
    [JsonPropertyName("target_language")]
    public required string TargetLanguage { get; init; }

    /// <summary>Semantic conventions version</summary>
    [JsonPropertyName("semconv_version")]
    public required string SemconvVersion { get; init; }

    /// <summary>Enabled features/modules</summary>
    [JsonPropertyName("features_json")]
    public required string FeaturesJson { get; init; }

    /// <summary>Template customizations</summary>
    [JsonPropertyName("template_overrides_json")]
    public string? TemplateOverridesJson { get; init; }

    /// <summary>Whether this is the default profile</summary>
    [JsonPropertyName("is_default")]
    public required bool IsDefault { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last update timestamp</summary>
    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; init; }

}

/// <summary>Selected semconv/feature per workspace for code generation</summary>
public sealed record GenerationSelectionEntity
{
    /// <summary>Selection ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Workspace</summary>
    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    /// <summary>Profile</summary>
    [JsonPropertyName("profile_id")]
    public required string ProfileId { get; init; }

    /// <summary>Selection type (semconv_group, feature, custom_attribute)</summary>
    [JsonPropertyName("selection_type")]
    public required string SelectionType { get; init; }

    /// <summary>Selection key (e.g. http, db, genai)</summary>
    [JsonPropertyName("selection_key")]
    public required string SelectionKey { get; init; }

    /// <summary>Whether enabled</summary>
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    /// <summary>Selection-specific configuration</summary>
    [JsonPropertyName("config_json")]
    public string? ConfigJson { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last update timestamp</summary>
    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; init; }

}

/// <summary>Browser-local handshake session for workspace verification</summary>
public sealed record HandshakeSessionEntity
{
    /// <summary>Session ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Target workspace</summary>
    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    /// <summary>PKCE-style challenge</summary>
    [JsonPropertyName("challenge")]
    public required string Challenge { get; init; }

    /// <summary>Challenge method</summary>
    [JsonPropertyName("challenge_method")]
    public required string ChallengeMethod { get; init; }

    /// <summary>Browser fingerprint</summary>
    [JsonPropertyName("browser_fingerprint")]
    public string? BrowserFingerprint { get; init; }

    /// <summary>Origin URL</summary>
    [JsonPropertyName("origin_url")]
    public string? OriginUrl { get; init; }

    /// <summary>Handshake state</summary>
    [JsonPropertyName("state")]
    public required global::Qyl.Domains.HandshakeState State { get; init; }

    /// <summary>Verification timestamp</summary>
    [JsonPropertyName("verified_at")]
    public DateTimeOffset? VerifiedAt { get; init; }

    /// <summary>Expiration timestamp</summary>
    [JsonPropertyName("expires_at")]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

}

/// <summary>Project registry: top-level organizational unit</summary>
public sealed record ProjectEntity
{
    /// <summary>Project ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Project name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>URL-safe slug (unique)</summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    /// <summary>Project description</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last update timestamp</summary>
    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Archive timestamp (null if active)</summary>
    [JsonPropertyName("archived_at")]
    public DateTimeOffset? ArchivedAt { get; init; }

}

/// <summary>Environment row per project (dev, staging, prod)</summary>
public sealed record ProjectEnvironmentEntity
{
    /// <summary>Environment ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Owning project</summary>
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    /// <summary>Environment name (dev, staging, prod)</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Display name for UI</summary>
    [JsonPropertyName("display_name")]
    public required string DisplayName { get; init; }

    /// <summary>Hex color for UI</summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }

    /// <summary>Sort order for display</summary>
    [JsonPropertyName("sort_order")]
    public required int SortOrder { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

}

/// <summary>Unified search request</summary>
public sealed record SearchRequest
{
    /// <summary>Search query text</summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>Entity type filters</summary>
    [JsonPropertyName("entity_types")]
    public IReadOnlyList<global::Qyl.Domains.SearchEntityType>? EntityTypes { get; init; }

    /// <summary>Project scope</summary>
    [JsonPropertyName("project_id")]
    public string? ProjectId { get; init; }

    /// <summary>Maximum results</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>Cursor for pagination</summary>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }

}

/// <summary>Unified search response</summary>
public sealed record SearchResponse
{
    /// <summary>Search results</summary>
    [JsonPropertyName("results")]
    public required IReadOnlyList<global::Qyl.Domains.SearchResult> Results { get; init; }

    /// <summary>Total matching documents</summary>
    [JsonPropertyName("total_count")]
    public required long TotalCount { get; init; }

    /// <summary>Query execution time in ms</summary>
    [JsonPropertyName("duration_ms")]
    public required int DurationMs { get; init; }

    /// <summary>Next page cursor</summary>
    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }

    /// <summary>Search suggestions</summary>
    [JsonPropertyName("suggestions")]
    public IReadOnlyList<string>? Suggestions { get; init; }

}

/// <summary>Individual search result</summary>
public sealed record SearchResult
{
    /// <summary>Document ID</summary>
    [JsonPropertyName("document_id")]
    public required string DocumentId { get; init; }

    /// <summary>Entity type</summary>
    [JsonPropertyName("entity_type")]
    public required global::Qyl.Domains.SearchEntityType EntityType { get; init; }

    /// <summary>Entity ID</summary>
    [JsonPropertyName("entity_id")]
    public required string EntityId { get; init; }

    /// <summary>Result title</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Result snippet with highlights</summary>
    [JsonPropertyName("snippet")]
    public string? Snippet { get; init; }

    /// <summary>Relevance score</summary>
    [JsonPropertyName("score")]
    public required double Score { get; init; }

    /// <summary>Link to entity</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

}

/// <summary>Append-only workflow event</summary>
public sealed record WorkflowEventEntity
{
    /// <summary>Event ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Parent run</summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>Source node (null for run-level events)</summary>
    [JsonPropertyName("node_id")]
    public string? NodeId { get; init; }

    /// <summary>Event type</summary>
    [JsonPropertyName("event_type")]
    public required string EventType { get; init; }

    /// <summary>Event name</summary>
    [JsonPropertyName("event_name")]
    public required string EventName { get; init; }

    /// <summary>Event payload</summary>
    [JsonPropertyName("payload_json")]
    public string? PayloadJson { get; init; }

    /// <summary>Monotonic sequence number</summary>
    [JsonPropertyName("sequence_number")]
    public required long SequenceNumber { get; init; }

    /// <summary>Event source identifier</summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>Correlation ID</summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}

/// <summary>Individual DAG node execution</summary>
public sealed record WorkflowNodeEntity
{
    /// <summary>Execution ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Parent run</summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>Node definition ID</summary>
    [JsonPropertyName("node_id")]
    public required string NodeId { get; init; }

    /// <summary>Node type</summary>
    [JsonPropertyName("node_type")]
    public required global::Qyl.Domains.WorkflowNodeType NodeType { get; init; }

    /// <summary>Node name</summary>
    [JsonPropertyName("node_name")]
    public required string NodeName { get; init; }

    /// <summary>Attempt number (1-based)</summary>
    [JsonPropertyName("attempt")]
    public required int Attempt { get; init; }

    /// <summary>Node input data</summary>
    [JsonPropertyName("input_json")]
    public string? InputJson { get; init; }

    /// <summary>Node output data</summary>
    [JsonPropertyName("output_json")]
    public string? OutputJson { get; init; }

    /// <summary>Node status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.WorkflowRunStatus Status { get; init; }

    /// <summary>Error message if failed</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    /// <summary>Retry count</summary>
    [JsonPropertyName("retry_count")]
    public required int RetryCount { get; init; }

    /// <summary>Maximum retries allowed</summary>
    [JsonPropertyName("max_retries")]
    public required int MaxRetries { get; init; }

    /// <summary>Timeout in milliseconds</summary>
    [JsonPropertyName("timeout_ms")]
    public int? TimeoutMs { get; init; }

    /// <summary>Start timestamp</summary>
    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Completion timestamp</summary>
    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Duration in milliseconds</summary>
    [JsonPropertyName("duration_ms")]
    public int? DurationMs { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

}

/// <summary>Top-level workflow execution</summary>
public sealed record WorkflowRunEntity
{
    /// <summary>Run ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Workflow definition ID</summary>
    [JsonPropertyName("workflow_id")]
    public required string WorkflowId { get; init; }

    /// <summary>Workflow definition version</summary>
    [JsonPropertyName("workflow_version")]
    public required int WorkflowVersion { get; init; }

    /// <summary>Owning project</summary>
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    /// <summary>Trigger type</summary>
    [JsonPropertyName("trigger_type")]
    public required global::Qyl.Domains.WorkflowTriggerType TriggerType { get; init; }

    /// <summary>Trigger source identifier</summary>
    [JsonPropertyName("trigger_source")]
    public string? TriggerSource { get; init; }

    /// <summary>Run input data</summary>
    [JsonPropertyName("input_json")]
    public string? InputJson { get; init; }

    /// <summary>Run output data</summary>
    [JsonPropertyName("output_json")]
    public string? OutputJson { get; init; }

    /// <summary>Run status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.WorkflowRunStatus Status { get; init; }

    /// <summary>Error message if failed</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    /// <summary>Parent run ID for sub-workflows</summary>
    [JsonPropertyName("parent_run_id")]
    public string? ParentRunId { get; init; }

    /// <summary>Correlation ID for tracing</summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; init; }

    /// <summary>Start timestamp</summary>
    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Completion timestamp</summary>
    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Duration in milliseconds</summary>
    [JsonPropertyName("duration_ms")]
    public int? DurationMs { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

}

/// <summary>Workspace envelope: the local-first workspace unit</summary>
public sealed record WorkspaceEnvelopeEntity
{
    /// <summary>Workspace ID</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Owning project</summary>
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    /// <summary>Environment</summary>
    [JsonPropertyName("environment_id")]
    public required string EnvironmentId { get; init; }

    /// <summary>Host node</summary>
    [JsonPropertyName("node_id")]
    public required string NodeId { get; init; }

    /// <summary>Workspace name</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Local filesystem root path</summary>
    [JsonPropertyName("root_path")]
    public required string RootPath { get; init; }

    /// <summary>Last heartbeat timestamp</summary>
    [JsonPropertyName("heartbeat_at")]
    public DateTimeOffset? HeartbeatAt { get; init; }

    /// <summary>Heartbeat interval in seconds</summary>
    [JsonPropertyName("heartbeat_interval_seconds")]
    public required int HeartbeatIntervalSeconds { get; init; }

    /// <summary>Workspace status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.WorkspaceStatus Status { get; init; }

    /// <summary>Workspace-level configuration overrides</summary>
    [JsonPropertyName("config_json")]
    public string? ConfigJson { get; init; }

    /// <summary>Creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last update timestamp</summary>
    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; init; }

}

